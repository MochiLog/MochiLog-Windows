using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Makaretu.Dns;

namespace MochiLog_Windows.Services;

public sealed record PairingInvitation(Guid HostId, Guid SessionId, string Model,
    string Code, string Url);

internal sealed class PairingSession
{
    public required PairingInvitation Invitation { get; init; }
    public required ConnectedDevice Device { get; init; }
    public required X25519PrivateKeyParameters PrivateKey { get; init; }
    public DateTimeOffset ExpiresAt { get; init; } = DateTimeOffset.UtcNow.AddMinutes(3);
    public byte[]? PublicKey { get; set; }
    public byte[]? Secret { get; set; }
    public Guid PhysicalDeviceId { get; set; }
    public int Attempts { get; set; }
    public bool Confirmed { get; set; }
}

public sealed class TransferServer : IDisposable
{
    #if MOCHILOG_PROTOCOL_TEST
    public const int Port = 54566;
    #else
    public const int Port = 54556;
    #endif
    private const int MaximumLogBytes = 64 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly CompanionState _state;
    private readonly Dictionary<Guid, DateTimeOffset> _nonces = [];
    private PairingSession? _session;
    private TcpListener? _listener;
    private CancellationTokenSource? _lifetime;
    private ServiceDiscovery? _discovery;
    private string? _advertisedAddresses;
    public event Action<string>? StatusChanged;
    public event Action<PairedPhone>? PhoneConfirmed;
    public event Action<PairedPhone>? PhoneAddressChanged;
    public Func<PairedPhone, byte[]>? SupportReport { get; set; }

    public TransferServer(CompanionState state) => _state = state;

    public void Start()
    {
        if (_listener is not null) return;
        _lifetime = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, Port);
        _listener.Start(20);
        _ = AcceptLoopAsync(_lifetime.Token);
        NetworkChange.NetworkAddressChanged += NetworkAddressChanged;
        AdvertiseCurrentAddresses();
        StatusChanged?.Invoke($"Encrypted transfer server listening on port {Port}.");
    }

    private void NetworkAddressChanged(object? sender, EventArgs args) =>
        Task.Run(AdvertiseCurrentAddresses);

    private void AdvertiseCurrentAddresses()
    {
        lock (_gate)
        {
            var addresses = LanAddresses();
            var tailnet = TailnetAddress();
            var signature = string.Join(",", addresses) + "|" + tailnet;
            if (_advertisedAddresses == signature) return;
            try
            {
                _discovery?.Dispose();
                _discovery = new ServiceDiscovery();
                var profile = new ServiceProfile(Upper(_state.HostId), "_mochilog._tcp", Port,
                    addresses.Select(IPAddress.Parse));
                profile.AddProperty("v", "1");
                profile.AddProperty("port", Port.ToString());
                profile.AddProperty("ipv4", string.Join(",", addresses));
                if (tailnet is not null)
                {
                    profile.AddProperty("tailnet", tailnet);
                    profile.AddProperty("tailnetPort", Port.ToString());
                }
                _discovery.Advertise(profile);
                _discovery.Announce(profile);
                _advertisedAddresses = signature;
                StatusChanged?.Invoke("Network address changed; local discovery was updated.");
            }
            catch (Exception error) { StatusChanged?.Invoke("Local discovery: " + error.Message); }
        }
    }

    public PairingInvitation BeginPairing(ConnectedDevice device)
    {
        var privateKey = new X25519PrivateKeyParameters(new SecureRandom());
        var publicKey = new byte[32];
        privateKey.GeneratePublicKey().Encode(publicKey, 0);
        var host = _state.HostId;
        var sessionId = Guid.NewGuid();
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
        var addresses = LanAddresses();
        var tailnet = TailnetAddress();
        var fields = new Dictionary<string, string>
        {
            ["v"] = "3", ["platform"] = "windows", ["host"] = Upper(host), ["session"] = Upper(sessionId),
            ["model"] = device.Model, ["public"] = Convert.ToBase64String(publicKey),
            ["ipv4"] = string.Join(",", addresses), ["port"] = Port.ToString()
        };
        var previouslyPaired = _state.Phones.FirstOrDefault(phone => phone.Udid == device.Udid);
        if (previouslyPaired is not null)
            fields["device"] = Upper(previouslyPaired.PhysicalDeviceId);
        if (tailnet is not null)
        {
            fields["tailnet"] = tailnet;
            fields["tailnetPort"] = Port.ToString();
        }
        var url = "mochilog-mac://pair?" + string.Join("&", fields.Select(field =>
            Uri.EscapeDataString(field.Key) + "=" + Uri.EscapeDataString(field.Value)));
        var invitation = new PairingInvitation(host, sessionId, device.Model, code, url);
        lock (_gate)
        {
            _session = new PairingSession { Invitation = invitation, Device = device,
                PrivateKey = privateKey };
        }
        return invitation;
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(token);
                _ = HandleAsync(client, token);
            }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (Exception error) { StatusChanged?.Invoke("Listener: " + error.Message); }
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken lifetime)
    {
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            try
            {
                var stream = client.GetStream();
                using var input = new MemoryStream();
                var one = new byte[1];
                while (input.Length < 16_384)
                {
                    if (await stream.ReadAsync(one, timeout.Token) == 0) return;
                    if (one[0] == 10) break;
                    input.WriteByte(one[0]);
                }
                if (input.Length >= 16_384) return;
                using var json = JsonDocument.Parse(input.ToArray());
                var root = json.RootElement;
                var remoteAddress = (client.Client.RemoteEndPoint as IPEndPoint)?.Address;
                var peer = remoteAddress?.IsIPv4MappedToIPv6 == true
                    ? remoteAddress.MapToIPv4().ToString() : remoteAddress?.ToString();
                if (root.TryGetProperty("type", out var type) && type.GetString() is
                    "pair-init" or "pair-confirm")
                {
                    var response = Pair(root, peer);
                    if (response is not null)
                    {
                        await stream.WriteAsync(response, timeout.Token);
                        await stream.WriteAsync(new byte[] { 10 }, timeout.Token);
                    }
                    return;
                }
                var packet = Pull(root, peer);
                if (packet is not null) await stream.WriteAsync(packet, timeout.Token);
            }
            catch (OperationCanceledException) { StatusChanged?.Invoke("Transfer timed out."); }
            catch (Exception error) { StatusChanged?.Invoke("Transfer rejected: " + error.Message); }
        }
    }

    private byte[]? Pair(JsonElement request, string? peer = null)
    {
        if (!Guid.TryParse(Get(request, "sessionID"), out var sessionId) ||
            !Guid.TryParse(Get(request, "physicalDeviceID"), out var physicalId) ||
            physicalId == Guid.Empty ||
            !TryBase64(Get(request, "clientPublicKey"), 32, out var clientPublic)) return null;
        lock (_gate)
        {
            var session = _session;
            if (session is null || session.ExpiresAt < DateTimeOffset.UtcNow ||
                session.Invitation.SessionId != sessionId ||
                Get(request, "version") != "3" ||
                _state.Phones.Any(phone => phone.Udid == session.Device.Udid &&
                    phone.PhysicalDeviceId != physicalId) ||
                session.PhysicalDeviceId != Guid.Empty && session.PhysicalDeviceId != physicalId ||
                session.PublicKey is not null &&
                    !CryptographicOperations.FixedTimeEquals(session.PublicKey, clientPublic)) return null;
            if (session.Secret is null)
            {
                var shared = new byte[32];
                session.PrivateKey.GenerateSecret(new X25519PublicKeyParameters(clientPublic, 0), shared, 0);
                session.Secret = Hkdf(shared, Encoding.UTF8.GetBytes(Upper(sessionId)),
                    Encoding.UTF8.GetBytes($"MochiLog pair v3|{Upper(_state.HostId)}|{Upper(physicalId)}"));
                session.PublicKey = clientPublic;
                session.PhysicalDeviceId = physicalId;
                CryptographicOperations.ZeroMemory(shared);
            }
            if (Get(request, "type") == "pair-init")
            {
                return JsonSerializer.SerializeToUtf8Bytes(new
                {
                    type = "pair-challenge", sessionID = Upper(sessionId),
                    proof = Mac(session.Secret, $"pair-challenge|{Upper(sessionId)}")
                });
            }
            if (session.Attempts >= 3 || !TryHex(Get(request, "confirmationMAC"), out var supplied) ||
                !CryptographicOperations.FixedTimeEquals(supplied,
                    Hmac(session.Secret, $"pair-confirm|{Upper(sessionId)}|{session.Invitation.Code}")))
            {
                session.Attempts++;
                return null;
            }
            if (!session.Confirmed)
            {
                var phone = new PairedPhone
                {
                    Udid = session.Device.Udid, Name = session.Device.Name,
                    Model = session.Device.Model, PhysicalDeviceId = physicalId,
                    Secret = session.Secret, LastKnownAddress = ValidPeerAddress(peer) ? peer : null
                };
                lock (_state) {
                    _state.Phones.RemoveAll(existing => existing.Udid == phone.Udid ||
                        existing.PhysicalDeviceId == phone.PhysicalDeviceId);
                    _state.Phones.Add(phone);
                    StateStore.Save(_state);
                }
                session.Confirmed = true;
                PhoneConfirmed?.Invoke(phone);
            }
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                type = "pair-complete", sessionID = Upper(sessionId),
                proof = Mac(session.Secret, $"pair-complete|{Upper(sessionId)}")
            });
        }
    }

    private byte[]? Pull(JsonElement request, string? peer = null)
    {
        if (Get(request, "version") != "2" ||
            !Guid.TryParse(Get(request, "hostID"), out var hostId) || hostId != _state.HostId ||
            !Guid.TryParse(Get(request, "physicalDeviceID"), out var physicalId) ||
            !Guid.TryParse(Get(request, "nonce"), out var nonce) ||
            !TryHex(Get(request, "mac"), out var supplied)) return null;
        lock (_gate)
        {
            var phone = _state.Phones.FirstOrDefault(p => p.PhysicalDeviceId == physicalId);
            if (phone is null) return null;
            var now = DateTimeOffset.UtcNow;
            foreach (var expired in _nonces.Where(n => now - n.Value > TimeSpan.FromMinutes(5))
                .Select(n => n.Key).ToArray()) _nonces.Remove(expired);
            if (_nonces.ContainsKey(nonce)) return null;
            var ack = Get(request, "ack") ?? "";
            var message = $"v2|{Upper(hostId)}|{Upper(physicalId)}|{Upper(nonce)}|{ack}";
            if (!CryptographicOperations.FixedTimeEquals(supplied, Hmac(phone.Secret, message))) return null;
            _nonces[nonce] = now;
            if (ValidPeerAddress(peer) && phone.LastKnownAddress != peer)
            {
                phone.LastKnownAddress = peer;
                StateStore.Save(_state);
                PhoneAddressChanged?.Invoke(phone);
            }
            if (Get(request, "presence") == "foreground" &&
                TryHex(Get(request, "presenceMAC"), out var presence) &&
                CryptographicOperations.FixedTimeEquals(presence,
                    Hmac(phone.Secret, $"presence|{Upper(nonce)}|foreground")) &&
                phone.ConfirmedAt is null)
            {
                phone.ConfirmedAt = now;
                StateStore.Save(_state);
                PhoneConfirmed?.Invoke(phone);
            }
            if (Get(request, "clientDiagnosticsBox") is { } encoded &&
                TryBase64UpTo(encoded, 8_256, out var diagnosticBox) &&
                diagnosticBox.Length >= 28)
            {
                try
                {
                    var report = new byte[diagnosticBox.Length - 28];
                    var context = Encoding.UTF8.GetBytes(
                        $"v2|diagnostics|{Upper(hostId)}|{Upper(physicalId)}|{Upper(nonce)}");
                    using var cipher = new AesGcm(phone.Secret, 16);
                    cipher.Decrypt(diagnosticBox.AsSpan(0, 12),
                        diagnosticBox.AsSpan(12, report.Length),
                        diagnosticBox.AsSpan(12 + report.Length, 16), report, context);
                    if (report.Length <= 8_192)
                    {
                        lock (_state) {
                            _state.PhoneDiagnostics[Upper(physicalId)] = report;
                            StateStore.Save(_state);
                        }
                    }
                }
                catch (CryptographicException) { return null; }
            }
            if (!string.IsNullOrEmpty(ack) && ValidToken(ack))
            {
                var file = Path.Combine(QueuePath(phone), ack.Replace("::", Path.DirectorySeparatorChar.ToString()));
                if (File.Exists(file))
                {
                    lock (_state) {
                        _state.Delivered.Add(Upper(phone.PhysicalDeviceId) + "|" + ack);
                        StateStore.Save(_state);
                    }
                    File.Delete(file);
                }
            }
            // Only completed, classified files are transferable. A batch
            // collector may be writing into .staging-* under this queue.
            var queueRoot = QueuePath(phone);
            var next = new[] { "Host", "Watch" }
                .SelectMany(kind => Directory.Exists(Path.Combine(queueRoot, kind))
                    ? Directory.EnumerateFiles(Path.Combine(queueRoot, kind),
                        "Analytics-*.ips.ca.synced", SearchOption.AllDirectories)
                    : [])
                .Order(StringComparer.Ordinal).FirstOrDefault();
            var token = next is null ? "" : Path.GetRelativePath(QueuePath(phone), next)
                .Replace(Path.DirectorySeparatorChar.ToString(), "::");
            if (next is not null && (!ValidToken(token) || new FileInfo(next).Length > MaximumLogBytes))
                return null;
            var filename = Encoding.UTF8.GetBytes(token);
            var content = next is null ? SupportReport?.Invoke(phone) ??
                JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schema = 1, platform = "Windows", generatedAt = DateTimeOffset.Now,
                    recentEvents = new[] { "Windows alpha companion connected" }
                }) : File.ReadAllBytes(next);
            var plain = new byte[2 + filename.Length + content.Length];
            BinaryPrimitives.WriteUInt16BigEndian(plain, (ushort)filename.Length);
            filename.CopyTo(plain.AsSpan(2));
            content.CopyTo(plain.AsSpan(2 + filename.Length));
            var iv = RandomNumberGenerator.GetBytes(12);
            var ciphertext = new byte[plain.Length];
            var tag = new byte[16];
            var aad = Encoding.UTF8.GetBytes(
                $"v2|response|{Upper(hostId)}|{Upper(physicalId)}|{Upper(nonce)}");
            using (var aes = new AesGcm(phone.Secret, 16)) aes.Encrypt(iv, plain, ciphertext, tag, aad);
            var combined = new byte[iv.Length + ciphertext.Length + tag.Length];
            iv.CopyTo(combined, 0);
            ciphertext.CopyTo(combined, iv.Length);
            tag.CopyTo(combined, iv.Length + ciphertext.Length);
            var frame = new byte[4 + combined.Length];
            BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)combined.Length);
            combined.CopyTo(frame, 4);
            StatusChanged?.Invoke(token.Length == 0 ? $"{phone.Name}: no new logs" :
                $"{phone.Name}: sending {token}");
            return frame;
        }
    }

    public static string QueuePath(PairedPhone phone) => Path.Combine(StateStore.Root,
        "Queue", Upper(phone.PhysicalDeviceId));

    private static bool ValidToken(string token)
    {
        var parts = token.Split("::");
        if (parts.Length is < 2 or > 3 || parts[0] is not ("Host" or "Watch")) return false;
        if (parts.Length == 3 && !System.Text.RegularExpressions.Regex.IsMatch(parts[1],
            "^ProxiedDevice-[a-fA-F0-9]+$")) return false;
        var file = parts[^1];
        return file == Path.GetFileName(file) && file.StartsWith("Analytics-", StringComparison.Ordinal) &&
            file.EndsWith(".ips.ca.synced", StringComparison.Ordinal);
    }

    private static string? Get(JsonElement obj, string property) =>
        obj.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
    private static bool ValidPeerAddress(string? peer) =>
        IPAddress.TryParse(peer, out var address) && address.AddressFamily == AddressFamily.InterNetwork &&
        !IPAddress.IsLoopback(address) && !address.Equals(IPAddress.Any);
    private static string Upper(Guid value) => value.ToString("D").ToUpperInvariant();
    private static byte[] Hmac(byte[] key, string message) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(message));
    private static string Mac(byte[] key, string message) =>
        Convert.ToHexString(Hmac(key, message)).ToLowerInvariant();
    private static bool TryHex(string? text, out byte[] bytes)
    {
        try { bytes = text is { Length: 64 } ? Convert.FromHexString(text) : []; return bytes.Length == 32; }
        catch { bytes = []; return false; }
    }
    private static bool TryBase64(string? text, int size, out byte[] bytes)
    {
        try { bytes = Convert.FromBase64String(text ?? ""); return bytes.Length == size; }
        catch { bytes = []; return false; }
    }
    private static bool TryBase64UpTo(string? text, int maximum, out byte[] bytes)
    {
        try { bytes = Convert.FromBase64String(text ?? ""); return bytes.Length <= maximum; }
        catch { bytes = []; return false; }
    }
    private static byte[] Hkdf(byte[] ikm, byte[] salt, byte[] info)
    {
        var prk = HMACSHA256.HashData(salt, ikm);
        var input = new byte[info.Length + 1];
        info.CopyTo(input, 0);
        input[^1] = 1;
        return HMACSHA256.HashData(prk, input);
    }
    private static IEnumerable<string> AllIPv4() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
        .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
        .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
        .Select(address => address.Address.ToString());
    private static string[] LanAddresses() => AllIPv4().Where(ip => ip.StartsWith("10.") ||
        ip.StartsWith("192.168.") || Is172Private(ip)).Distinct().Take(4).ToArray();
    private static bool Is172Private(string value) => value.Split('.') is ["172", var second, _, _] &&
        int.TryParse(second, out var part) && part is >= 16 and <= 31;
    private static string? TailnetAddress() => AllIPv4().FirstOrDefault(ip =>
        ip.Split('.') is ["100", var second, _, _] && int.TryParse(second, out var part) &&
        part is >= 64 and <= 127);

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= NetworkAddressChanged;
        _lifetime?.Cancel();
        _listener?.Stop();
        _discovery?.Dispose();
        _lifetime?.Dispose();
    }
}
