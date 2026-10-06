using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MochiLog_Windows.Services;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

static class Program
{
    private static int _serverPort;
    private static string Upper(Guid id) => id.ToString("D").ToUpperInvariant();
    private static byte[] Hmac(byte[] key, string text) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(text));
    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void CheckArchiveExchange()
    {
        var deviceId = Guid.NewGuid();
        var day = DateTime.Today.ToString("yyyyMMdd",
            System.Globalization.CultureInfo.InvariantCulture);
        var fullDay = DateTime.Today.ToString("yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture);
        var local = Path.Combine(StateStore.Root, "DebugLogs");
        Directory.CreateDirectory(local);
        var computerData = Encoding.UTF8.GetBytes(new string('x', 10_000));
        File.WriteAllBytes(Path.Combine(local, fullDay + ".log"), computerData);
        Check(DebugArchiveSync.LocalManifest(deviceId)[day] == computerData.Length,
            "Computer archive manifest omitted a day.");
        var phoneData = Encoding.UTF8.GetBytes("phone archive line\n");
        var report = JsonSerializer.SerializeToUtf8Bytes(new {
            archiveManifest = new Dictionary<string, int> { [day] = phoneData.Length },
            archiveRequest = new { day, offset = 0 },
            archiveChunk = new { day, offset = 0,
                data = Convert.ToBase64String(phoneData) }
        });
        var chunk = DebugArchiveSync.ComputerChunk(report);
        Check(chunk is not null && chunk["data"] is string encodedChunk &&
            Convert.FromBase64String(encodedChunk).Length == 8_192 &&
            computerData.AsSpan(0, 8_192).SequenceEqual(Convert.FromBase64String(encodedChunk)),
            "Computer archive chunk did not match the requested day or size.");
        DebugArchiveSync.ReceivePhoneChunk(report, deviceId, 30);
        DebugArchiveSync.ReceivePhoneChunk(report, deviceId, 30);
        Check(DebugArchiveSync.PhoneText(deviceId, fullDay) ==
            Encoding.UTF8.GetString(phoneData),
            "Retried phone archive chunk was duplicated.");
        var extended = JsonSerializer.SerializeToUtf8Bytes(new {
            archiveManifest = new Dictionary<string, int> { [day] = phoneData.Length + 1 }
        });
        var request = DebugArchiveSync.RequestPhoneChunk(extended, deviceId);
        Check(request is not null && Convert.ToInt64(request["offset"]) == phoneData.Length,
            "Phone archive did not resume from the stored byte offset.");
        for (var offset = 1; offset <= 4; offset++) {
            var date = DateTime.Today.AddDays(-offset);
            var oldCompact = date.ToString("yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture);
            var oldDay = date.ToString("yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture);
            File.WriteAllBytes(Path.Combine(local, oldDay + ".log"), computerData);
            var oldReport = JsonSerializer.SerializeToUtf8Bytes(new {
                archiveChunk = new { day = oldCompact, offset = 0,
                    data = Convert.ToBase64String(phoneData) }
            });
            DebugArchiveSync.ReceivePhoneChunk(oldReport, deviceId, 30);
        }
        DebugArchiveSync.RefreshSnapshot(deviceId);
        Check(DebugArchiveSync.LocalManifest(deviceId).Count >= 5,
            "Five computer archive days were not available.");
        Check(DebugArchiveSync.PhoneDays(deviceId).Count >= 5,
            "Five phone archive days were not available.");
    }

    private static void CheckCurrentDiagnostics()
    {
        Check(Collector.ToolFailureLine(
            "{\"results\":[{\"index\":0,\"ok\":false,\"error\":\"FILE_OPEN failed\"}]}",
            "") is null,
            "A file-level JSON error was mistaken for a failed collection command.");
        Check(Collector.ToolFailureLine("", "ERROR: device connection failed") is not null,
            "A real collector diagnostic was ignored.");
        const string name = "Analytics-2026-10-01-090003.000.ips.ca.synced";
        var current = Collector.AnalyticsEntries("/", null,
            $"/{name}\n/Analytics-2026-10-01-023246.session.ips.ca.synced\n");
        var retired = Collector.AnalyticsEntries("/Retired", null, $"/Retired/{name}\n");
        Check(current.Count == 1 && current[0].Path == "/" + name &&
            retired.Count == 1 && retired[0].Path == "/Retired/" + name,
            "Current diagnostic files were hidden or session files were accepted.");
        Check(Collector.IsLikelyDailyReport("/Retired/" + name, null) &&
            Collector.IsLikelyDailyReport(
                "/ProxiedDevice-abcdef/Retired/Analytics-2026-10-01-090017.ips.ca.synced",
                "ProxiedDevice-abcdef") &&
            !Collector.IsLikelyDailyReport("/Retired/Analytics-2026-10-01-090003.ips.ca.synced", null),
            "A truncated daily battery report could be permanently excluded.");
    }

    private static void CheckBatteryLogStorage()
    {
        var phone = new PairedPhone { Udid = "storage-test", Name = "Storage Test",
            Model = "iPhone18,3", PhysicalDeviceId = Guid.NewGuid() };
        var queue = Path.Combine(TransferServer.QueuePath(phone), "Host");
        Directory.CreateDirectory(queue);
        var file = Path.Combine(queue, "Analytics-2026-09-29-storage.ips.ca.synced");
        var raw = Encoding.UTF8.GetBytes("raw diagnostic bytes");
        BatteryLogStorage.UpdateSettings(false, 500, 1);
        File.WriteAllBytes(file, raw);
        BatteryLogStorage.ArchiveAcknowledged(file, phone);
        Check(!File.Exists(file) && BatteryLogStorage.List([phone]).Count == 0,
            "Immediate-delete mode retained an acknowledged log.");
        BatteryLogStorage.UpdateSettings(true, 500, 1);
        File.WriteAllBytes(file, raw);
        BatteryLogStorage.ArchiveAcknowledged(file, phone);
        var archived = BatteryLogStorage.List([phone]);
        Check(archived.Count == 1 && !archived[0].Pending,
            "Keep mode did not archive an acknowledged log.");
        var export = Path.Combine(StateStore.Root, "test-export");
        BatteryLogStorage.Export(archived, export);
        Check(File.ReadAllBytes(Path.Combine(export, Upper(phone.PhysicalDeviceId),
            "Host", Path.GetFileName(file))).SequenceEqual(raw),
            "Battery log export changed the raw file.");
        Check(BatteryLogStorage.Requeue(archived, [phone]) == 1 && File.Exists(file),
            "Manual resend did not restore the pending queue file.");
        Check(BatteryLogStorage.List([phone]).Count == 2,
            "Pending and archived copies were not both listed.");
        BatteryLogStorage.ArchiveAcknowledged(file, phone);
        BatteryLogStorage.Delete(archived);
        Check(BatteryLogStorage.List([phone]).Count == 0,
            "Deleting the archived log left a copy.");
        BatteryLogStorage.UpdateSettings(false, 500, 1);
    }

    private static void CheckDailyCollectionCoverage()
    {
        StoredBatteryLog Row(string kind, string? source, string day) =>
            new(Guid.NewGuid().ToString(),
                $"Analytics-{day}-090000.ips.ca.synced", Guid.NewGuid(), "Test",
                kind, source, 100_000, DateTimeOffset.UtcNow, true);
        var host = Row("Host", null, "2026-10-03");
        var firstWatch = Row("Watch", "ProxiedDevice-a1", "2026-10-03");
        var secondWatch = Row("Watch", "ProxiedDevice-b2", "2026-10-02");
        Check(BatteryLogStorage.HasRequiredDailyLogs("iPad16,6", [host], "2026-10-03"),
            "iPad host log did not stop collection.");
        Check(!BatteryLogStorage.HasRequiredDailyLogs("iPhone18,3", [host], "2026-10-03"),
            "Unknown Watch state stopped iPhone collection.");
        Check(!BatteryLogStorage.HasRequiredDailyLogs("iPhone18,3",
            [host, firstWatch, secondWatch], "2026-10-03"),
            "A second known Watch was ignored.");
        Check(BatteryLogStorage.HasRequiredDailyLogs("iPhone18,3",
            [host, firstWatch, secondWatch,
                Row("Watch", "ProxiedDevice-b2", "2026-10-03")], "2026-10-03"),
            "Complete iPhone and Watch logs did not stop collection.");
        var receipts = new BatteryLogStorage.VerifiedBatteryReceipt[] {
            new("Host", null, "2026-10-03"),
            new("Watch", "ProxiedDevice-a1", "2026-10-03")
        };
        Check(BatteryLogStorage.HasRequiredDailyLogs("iPhone18,3", [], "2026-10-03", receipts),
            "Deleting acknowledged raw files restarted daily collection.");
        Check(!BatteryLogStorage.HasRequiredDailyLogs("iPhone18,3", [], "2026-10-04", receipts),
            "Yesterday's acknowledged logs stopped today's collection.");
    }

    private static byte[] Derive(byte[] shared, Guid session, Guid host, Guid physical)
    {
        var salt = Encoding.UTF8.GetBytes(Upper(session));
        var prk = HMACSHA256.HashData(salt, shared);
        var info = Encoding.UTF8.GetBytes($"MochiLog pair v3|{Upper(host)}|{Upper(physical)}");
        return HMACSHA256.HashData(prk, info.Concat(new byte[] { 1 }).ToArray());
    }

    private static async Task<byte[]> ExchangeAsync(object request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _serverPort);
        var stream = client.GetStream();
        await stream.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(request));
        await stream.WriteAsync(new byte[] { 10 });
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        return output.ToArray();
    }

    private static (string Name, byte[] Content) Open(byte[] response, byte[] secret,
        Guid host, Guid physical, Guid nonce)
    {
        Check(response.Length >= 32, "Encrypted response was missing.");
        var length = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(0, 4));
        Check(length == response.Length - 4, "Response frame length was wrong.");
        var combined = response.AsSpan(4);
        var plain = new byte[combined.Length - 28];
        var aad = Encoding.UTF8.GetBytes(
            $"v2|response|{Upper(host)}|{Upper(physical)}|{Upper(nonce)}");
        using var aes = new AesGcm(secret, 16);
        aes.Decrypt(combined[..12], combined.Slice(12, plain.Length),
            combined.Slice(12 + plain.Length, 16), plain, aad);
        var nameLength = BinaryPrimitives.ReadUInt16BigEndian(plain.AsSpan(0, 2));
        var name = Encoding.UTF8.GetString(plain.AsSpan(2, nameLength));
        return (name, plain.AsSpan(2 + nameLength).ToArray());
    }

    public static async Task Main()
    {
        CheckCurrentDiagnostics();
        CheckArchiveExchange();
        CheckBatteryLogStorage();
        CheckDailyCollectionCoverage();
        using (var blocker = new TcpListener(IPAddress.Any, 0))
        {
            blocker.Start();
            var blockedPort = ((IPEndPoint)blocker.LocalEndpoint).Port;
            var fallbackState = new CompanionState();
            using var fallback = new TransferServer(fallbackState, blockedPort);
            fallback.Start();
            Check(fallback.ListeningPort != blockedPort &&
                fallbackState.TransferPort == fallback.ListeningPort &&
                StateStore.Load().TransferPort == fallback.ListeningPort,
                "Reserved transfer port did not fall back to a persisted available port.");
            var fallbackInvitation = fallback.BeginPairing(new ConnectedDevice(
                "fallback-device", "Fallback iPad", "iPad16,6"));
            Check(fallbackInvitation.Url.Contains($"port={fallback.ListeningPort}"),
                "Pairing QR did not advertise the selected transfer port.");
        }
        var state = new CompanionState();
        using var server = new TransferServer(state);
        var preflightEvents = new List<string>();
        var idleEvents = new List<string>();
        server.StatusChanged += message => {
            if (message.Contains("preflight", StringComparison.Ordinal))
                preflightEvents.Add(message);
            if (message.Contains(": no new logs", StringComparison.Ordinal))
                idleEvents.Add(message);
        };
        server.Start();
        _serverPort = server.ListeningPort;
        try {
            server.BeginPairing(new ConnectedDevice("untrusted", "Untrusted iPad", ""));
            throw new Exception("An untrusted device produced a pairing QR.");
        } catch (InvalidOperationException) { }
        var device = new ConnectedDevice("test-iphone", "Protocol iPhone", "iPhone18,3");
        var invitation = server.BeginPairing(device);
        var qr = new Uri(invitation.Url);
        var fields = qr.Query.TrimStart('?').Split('&').Select(part => part.Split('=', 2))
            .ToDictionary(part => Uri.UnescapeDataString(part[0]),
                part => Uri.UnescapeDataString(part[1]));
        Check(fields["v"] == "3" && fields["platform"] == "windows", "QR version is wrong.");
        var publicKey = Convert.FromBase64String(fields["public"]);
        var clientPrivate = new X25519PrivateKeyParameters(new SecureRandom());
        var clientPublic = new byte[32];
        clientPrivate.GeneratePublicKey().Encode(clientPublic, 0);
        var shared = new byte[32];
        clientPrivate.GenerateSecret(new X25519PublicKeyParameters(publicKey, 0), shared, 0);
        var physical = Guid.NewGuid();
        var key = Derive(shared, invitation.SessionId, invitation.HostId, physical);
        var common = new Dictionary<string, string> {
            ["sessionID"] = Upper(invitation.SessionId), ["version"] = "3",
            ["physicalDeviceID"] = Upper(physical),
            ["clientPublicKey"] = Convert.ToBase64String(clientPublic)
        };
        var init = new Dictionary<string, string>(common) { ["type"] = "pair-init" };
        var challenge = JsonDocument.Parse(await ExchangeAsync(init)).RootElement;
        Check(challenge.GetProperty("proof").GetString() ==
            Hex(Hmac(key, $"pair-challenge|{Upper(invitation.SessionId)}")),
            "Pairing challenge MAC was wrong.");
        var incorrect = new Dictionary<string, string>(common) {
            ["type"] = "pair-confirm", ["confirmationMAC"] = Hex(Hmac(key,
                $"pair-confirm|{Upper(invitation.SessionId)}|000000-wrong"))
        };
        Check((await ExchangeAsync(incorrect)).Length == 0 && state.Phones.Count == 0,
            "An incorrect confirmation code paired the phone.");
        var confirm = new Dictionary<string, string>(common) {
            ["type"] = "pair-confirm",
            ["confirmationMAC"] = Hex(Hmac(key,
                $"pair-confirm|{Upper(invitation.SessionId)}|{invitation.Code}"))
        };
        var completed = JsonDocument.Parse(await ExchangeAsync(confirm)).RootElement;
        Check(completed.GetProperty("proof").GetString() ==
            Hex(Hmac(key, $"pair-complete|{Upper(invitation.SessionId)}")),
            "Pairing completion MAC was wrong.");
        Check(state.Phones.Count == 1 && state.Phones[0].PhysicalDeviceId == physical,
            "Windows changed the mobile device's physical ID.");
        var encryptedState = File.ReadAllBytes(Path.Combine(StateStore.Root, "state.bin"));
        Check(!Encoding.UTF8.GetString(encryptedState).Contains("PhysicalDeviceId") &&
            StateStore.Load().Phones[0].Secret.SequenceEqual(key),
            "Pairing keys were not protected and recoverable through Windows DPAPI.");

        var queue = Path.Combine(TransferServer.QueuePath(state.Phones[0]), "Host");
        Directory.CreateDirectory(queue);
        var uncertain = Path.Combine(queue, ".unclassified-test");
        await File.WriteAllBytesAsync(uncertain, new byte[1_000_000]);
        Check(Collector.ShouldRecheckUnclassified(uncertain),
            "A large unclassified Analytics download must remain eligible for retry.");
        await File.WriteAllTextAsync(uncertain, "short unrelated diagnostic");
        Check(!Collector.ShouldRecheckUnclassified(uncertain),
            "A small unrelated diagnostic should be excluded.");
        var firstObservation = Collector.ObserveUnclassified(uncertain, null);
        var secondObservation = Collector.ObserveUnclassified(uncertain, firstObservation);
        var thirdObservation = Collector.ObserveUnclassified(uncertain, secondObservation);
        Check(firstObservation?.Confirmations == 1 && secondObservation?.Confirmations == 2 &&
            thirdObservation?.Confirmations == 3,
            "Stable non-battery downloads should stop after three checks.");
        await File.WriteAllBytesAsync(uncertain, []);
        Check(Collector.ObserveUnclassified(uncertain, thirdObservation) is null,
            "An empty pull must not confirm a non-battery report.");
        await File.WriteAllTextAsync(uncertain, "changed diagnostic");
        Check(Collector.ObserveUnclassified(uncertain, thirdObservation)?.Confirmations == 1,
            "A changed report must start a new confirmation count.");
        File.Delete(uncertain);
        var name = "Analytics-2026-09-27-test.ips.ca.synced";
        var payload = Encoding.UTF8.GetBytes("diagnostic transport fixture");
        await File.WriteAllBytesAsync(Path.Combine(queue, name), payload);
        var unfinished = Path.Combine(TransferServer.QueuePath(state.Phones[0]), ".staging-test");
        Directory.CreateDirectory(unfinished);
        await File.WriteAllBytesAsync(Path.Combine(unfinished,
            "Analytics-2026-09-01-unclassified.ips.ca.synced"), payload);
        async Task<byte[]> Pull(Guid nonce, string ack = "") => await ExchangeAsync(new {
            version = "2", hostID = Upper(invitation.HostId), physicalDeviceID = Upper(physical),
            nonce = Upper(nonce), ack,
            mac = Hex(Hmac(key,
                $"v2|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(nonce)}|{ack}"))
        });
        async Task<(string Name, byte[] Content)> PullOffer(Guid nonce,
            string? token = null, string? digest = null, string? decision = null) {
            var fields = new Dictionary<string, string> {
                ["version"] = "2", ["hostID"] = Upper(invitation.HostId),
                ["physicalDeviceID"] = Upper(physical), ["nonce"] = Upper(nonce),
                ["ack"] = "", ["mac"] = Hex(Hmac(key,
                    $"v2|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(nonce)}|")),
                ["offerVersion"] = "1", ["offerMAC"] = Hex(Hmac(key,
                    $"file-offer|v1|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(nonce)}"))
            };
            if (token is not null && digest is not null && decision is not null) {
                fields["offerToken"] = token;
                fields["offerDigest"] = digest;
                fields["offerDecision"] = decision;
                fields["offerDecisionMAC"] = Hex(Hmac(key,
                    $"file-decision|v1|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(nonce)}|{token}|{digest}|{decision}"));
            }
            return Open(await ExchangeAsync(fields), key, invitation.HostId, physical, nonce);
        }
        var badNonce = Guid.NewGuid();
        Check((await ExchangeAsync(new {
            version = "2", hostID = Upper(invitation.HostId),
            physicalDeviceID = Upper(physical), nonce = Upper(badNonce), ack = "",
            mac = new string('0', 64)
        })).Length == 0, "Invalid request authentication was accepted.");
        var firstNonce = Guid.NewGuid();
        var first = Open(await Pull(firstNonce), key, invitation.HostId, physical, firstNonce);
        Check(first.Name == "Host::" + name && first.Content.SequenceEqual(payload),
            "Encrypted battery log transfer failed.");
        Check((await Pull(firstNonce)).Length == 0, "Repeated nonce was accepted.");
        var backgroundNonce = Guid.NewGuid();
        var backgroundNotice = new {
            version = "2", hostID = Upper(invitation.HostId), physicalDeviceID = Upper(physical),
            nonce = Upper(backgroundNonce), ack = "", presence = "background",
            mac = Hex(Hmac(key,
                $"v2|background|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(backgroundNonce)}"))
        };
        Check((await ExchangeAsync(backgroundNotice)).Length == 0,
            "Authenticated background notice returned log data.");
        Check((await ExchangeAsync(backgroundNotice)).Length == 0,
            "Repeated background notice returned log data.");
        Check((await Pull(backgroundNonce)).Length == 0,
            "Background nonce was not recorded for replay rejection.");
        var diagnosticNonce = Guid.NewGuid();
        var diagnostic = Encoding.UTF8.GetBytes("encrypted phone diagnostic fixture");
        var diagnosticIv = RandomNumberGenerator.GetBytes(12);
        var diagnosticCipher = new byte[diagnostic.Length];
        var diagnosticTag = new byte[16];
        var diagnosticContext = Encoding.UTF8.GetBytes(
            $"v2|diagnostics|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(diagnosticNonce)}");
        using (var aes = new AesGcm(key, 16))
            aes.Encrypt(diagnosticIv, diagnostic, diagnosticCipher, diagnosticTag, diagnosticContext);
        var diagnosticBox = diagnosticIv.Concat(diagnosticCipher).Concat(diagnosticTag).ToArray();
        var diagnosticResponse = await ExchangeAsync(new {
            version = "2", hostID = Upper(invitation.HostId),
            physicalDeviceID = Upper(physical), nonce = Upper(diagnosticNonce), ack = "",
            mac = Hex(Hmac(key,
                $"v2|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(diagnosticNonce)}|")),
            clientDiagnosticsBox = Convert.ToBase64String(diagnosticBox)
        });
        Check(diagnosticResponse.Length > 0 &&
            state.PhoneDiagnostics.TryGetValue(Upper(physical), out var storedDiagnostic) &&
            storedDiagnostic.SequenceEqual(diagnostic),
            "Encrypted phone diagnostics were not authenticated and saved.");
        BatteryLogStorage.UpdateSettings(true, 500, 1);
        var ackNonce = Guid.NewGuid();
        var final = Open(await Pull(ackNonce, first.Name), key,
            invitation.HostId, physical, ackNonce);
        Check(final.Name.Length == 0 && !File.Exists(Path.Combine(queue, name)),
            "File acknowledgement did not complete the batch.");
        Check(state.Delivered.Contains(Upper(physical) + "|" + first.Name),
            "Delivered file was not marked.");
        var repeatedIdleNonce = Guid.NewGuid();
        var repeatedIdle = Open(await Pull(repeatedIdleNonce), key,
            invitation.HostId, physical, repeatedIdleNonce);
        Check(repeatedIdle.Name.Length == 0 && idleEvents.Count == 1,
            "Repeated empty pulls produced duplicate idle status events.");
        var archivedForResend = BatteryLogStorage.List(state.Phones).Where(row => !row.Pending).ToArray();
        Check(archivedForResend.Length == 1 &&
            BatteryLogStorage.Requeue(archivedForResend, state.Phones) == 1,
            "Acknowledged log was not available for manual resend.");
        var resendNonce = Guid.NewGuid();
        var resent = Open(await Pull(resendNonce), key, invitation.HostId, physical, resendNonce);
        Check(resent.Name == first.Name && resent.Content.SequenceEqual(payload),
            "Manual resend did not deliver the selected raw log.");
        var resendAckNonce = Guid.NewGuid();
        var resendEnd = Open(await Pull(resendAckNonce, resent.Name), key,
            invitation.HostId, physical, resendAckNonce);
        Check(resendEnd.Name.Length == 0 &&
            BatteryLogStorage.List(state.Phones).Count(row => row.Pending) == 0,
            "Repeated acknowledgement left the resend pending.");
        BatteryLogStorage.Delete(archivedForResend);
        BatteryLogStorage.UpdateSettings(false, 500, 1);
        var duplicateName = "Analytics-2026-09-28-090000.ips.ca.synced";
        var duplicatePath = Path.Combine(queue, duplicateName);
        await File.WriteAllBytesAsync(duplicatePath, payload);
        var offered = await PullOffer(Guid.NewGuid());
        var offer = JsonDocument.Parse(offered.Content).RootElement;
        var digest = Hex(SHA256.HashData(payload));
        Check(offered.Name.Length == 0 && offer.GetProperty("type").GetString() == "file-offer" &&
            offer.GetProperty("sha256").GetString() == digest &&
            offer.GetProperty("token").GetString() == "Host::" + duplicateName,
            "Preflight sent bytes or an incorrect digest.");
        var mismatch = await PullOffer(Guid.NewGuid(), "Host::" + duplicateName,
            new string('0', 64), "have");
        Check(mismatch.Name.Length == 0 && File.Exists(duplicatePath),
            "Mismatched digest removed a queued log.");
        var skipped = await PullOffer(Guid.NewGuid(), "Host::" + duplicateName,
            digest, "have");
        Check(skipped.Name.Length == 0 && !File.Exists(duplicatePath),
            "Matching mobile receipt did not suppress duplicate transfer.");
        Check(preflightEvents.Any(message => message.Contains("preflight offer Host::" + duplicateName)) &&
            preflightEvents.Any(message => message.Contains("preflight decision=have, action=skip Host::" + duplicateName)) &&
            preflightEvents.Any(message => message.Contains("digest changed")),
            "Preflight offer, duplicate decision, or rejection was missing from the debug event stream.");
        await File.WriteAllBytesAsync(duplicatePath, payload);
        await File.WriteAllBytesAsync(duplicatePath + ".force-resend", []);
        var forcedOffer = JsonDocument.Parse((await PullOffer(Guid.NewGuid())).Content)
            .RootElement;
        Check(forcedOffer.GetProperty("force").GetString() == "true",
            "Manual resend was not identified.");
        _ = await PullOffer(Guid.NewGuid(), "Host::" + duplicateName, digest, "have");
        Check(File.Exists(duplicatePath),
            "A mobile duplicate decision cancelled an explicit manual resend.");
        var sent = await PullOffer(Guid.NewGuid(), "Host::" + duplicateName,
            digest, "send");
        Check(sent.Name == "Host::" + duplicateName && sent.Content.SequenceEqual(payload),
            "Approved preflight did not deliver a manual resend.");
        _ = await Pull(Guid.NewGuid(), sent.Name);
        Check(!File.Exists(duplicatePath + ".force-resend"),
            "Manual resend marker remained after acknowledgement.");
        var pauseUntil = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString();
        var invalidPauseNonce = Guid.NewGuid();
        var invalidPause = Open(await ExchangeAsync(new {
            version = "2", hostID = Upper(invitation.HostId), physicalDeviceID = Upper(physical),
            nonce = Upper(invalidPauseNonce), ack = "",
            mac = Hex(Hmac(key,
                $"v2|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(invalidPauseNonce)}|")),
            dailyPauseUntil = pauseUntil, dailyPauseMAC = new string('0', 64)
        }), key, invitation.HostId, physical, invalidPauseNonce);
        Check(invalidPause.Name.Length == 0 && state.Phones[0].AutomaticPauseUntil is null,
            "Invalid pause proof changed collection state.");
        var pauseNonce = Guid.NewGuid();
        var acceptedPause = Open(await ExchangeAsync(new {
            version = "2", hostID = Upper(invitation.HostId), physicalDeviceID = Upper(physical),
            nonce = Upper(pauseNonce), ack = "",
            mac = Hex(Hmac(key,
                $"v2|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(pauseNonce)}|")),
            dailyPauseUntil = pauseUntil,
            dailyPauseMAC = Hex(Hmac(key,
                $"daily-pause|v1|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(pauseNonce)}|{pauseUntil}"))
        }), key, invitation.HostId, physical, pauseNonce);
        var pauseAck = JsonDocument.Parse(acceptedPause.Content).RootElement;
        Check(acceptedPause.Name.Length == 0 &&
            pauseAck.GetProperty("type").GetString() == "daily-pause-ack" &&
            pauseAck.GetProperty("until").GetString() == pauseUntil &&
            StateStore.Load().Phones[0].AutomaticPauseUntil is not null,
            "Authenticated pause was not acknowledged and persisted.");
        Check((await Pull(pauseNonce)).Length == 0,
            "A pause request nonce was accepted twice.");
        var resumeNonce = Guid.NewGuid();
        var resumed = Open(await ExchangeAsync(new {
            version = "2", hostID = Upper(invitation.HostId), physicalDeviceID = Upper(physical),
            nonce = Upper(resumeNonce), ack = "",
            mac = Hex(Hmac(key,
                $"v2|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(resumeNonce)}|")),
            dailyResumeMAC = Hex(Hmac(key,
                $"daily-resume|v1|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(resumeNonce)}"))
        }), key, invitation.HostId, physical, resumeNonce);
        Check(JsonDocument.Parse(resumed.Content).RootElement.GetProperty("type").GetString() ==
            "daily-resume-ack" && StateStore.Load().Phones[0].AutomaticPauseUntil is null,
            "Authenticated resume did not clear the collection pause.");
        var addressChanges = 0;
        server.PhoneAddressChanged += _ => addressChanges++;
        var addressNonce = Guid.NewGuid();
        using var addressRequest = JsonDocument.Parse(JsonSerializer.Serialize(new {
            version = "2", hostID = Upper(invitation.HostId), physicalDeviceID = Upper(physical),
            nonce = Upper(addressNonce), ack = "",
            mac = Hex(Hmac(key,
                $"v2|{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(addressNonce)}|"))
        }));
        var pullMethod = typeof(TransferServer).GetMethod("Pull",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        _ = pullMethod.Invoke(server, [addressRequest.RootElement, "192.168.3.31"]);
        Check(state.Phones[0].LastKnownAddress == "192.168.3.31" && addressChanges == 1,
            "An authenticated address change did not update the wireless collection route.");
        var invalidNonce = Guid.NewGuid();
        using var invalidAddressRequest = JsonDocument.Parse(JsonSerializer.Serialize(new {
            version = "2", hostID = Upper(invitation.HostId), physicalDeviceID = Upper(physical),
            nonce = Upper(invalidNonce), ack = "", mac = new string('0', 64)
        }));
        _ = pullMethod.Invoke(server, [invalidAddressRequest.RootElement, "192.168.3.32"]);
        Check(state.Phones[0].LastKnownAddress == "192.168.3.31" && addressChanges == 1,
            "An unauthenticated request changed the device address.");
        var secondKey = RandomNumberGenerator.GetBytes(32);
        var second = new PairedPhone { Udid = "other-iphone", Name = "Other iPhone",
            Model = "iPhone18,3", PhysicalDeviceId = Guid.NewGuid(), Secret = secondKey };
        state.Phones.Add(second);
        StateStore.Save(state);
        async Task<byte[]> SecurePull(Guid nonce, long? timestamp = null) {
            var at = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var inner = JsonSerializer.SerializeToUtf8Bytes(new {
                version = "2", hostID = Upper(invitation.HostId),
                physicalDeviceID = Upper(second.PhysicalDeviceId), nonce = Upper(nonce), ack = "",
                mac = Hex(Hmac(secondKey,
                    $"v2|{Upper(invitation.HostId)}|{Upper(second.PhysicalDeviceId)}|{Upper(nonce)}|"))
            });
            var iv = RandomNumberGenerator.GetBytes(12);
            var cipher = new byte[inner.Length];
            var tag = new byte[16];
            var aad = Encoding.UTF8.GetBytes(
                $"v3|request|{Upper(invitation.HostId)}|{Upper(second.PhysicalDeviceId)}|{Upper(nonce)}|{at}");
            using (var aes = new AesGcm(secondKey, 16))
                aes.Encrypt(iv, inner, cipher, tag, aad);
            return await ExchangeAsync(new {
                version = "3", hostID = Upper(invitation.HostId),
                physicalDeviceID = Upper(second.PhysicalDeviceId), nonce = Upper(nonce),
                issuedAt = at, box = Convert.ToBase64String(iv.Concat(cipher).Concat(tag).ToArray())
            });
        }
        var secureNonce = Guid.NewGuid();
        _ = Open(await SecurePull(secureNonce), secondKey, invitation.HostId,
            second.PhysicalDeviceId, secureNonce);
        Check((await SecurePull(secureNonce)).Length == 0,
            "Sealed transfer replay was accepted.");
        Check((await SecurePull(Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds())).Length == 0,
            "Expired sealed transfer was accepted.");
        var oldNonce = Guid.NewGuid();
        Check((await ExchangeAsync(new {
            version = "2", hostID = Upper(invitation.HostId),
            physicalDeviceID = Upper(second.PhysicalDeviceId), nonce = Upper(oldNonce), ack = "",
            mac = Hex(Hmac(secondKey,
                $"v2|{Upper(invitation.HostId)}|{Upper(second.PhysicalDeviceId)}|{Upper(oldNonce)}|"))
        })).Length == 0 && StateStore.Load().SecureTransferPhones.Contains(second.PhysicalDeviceId) &&
            StateStore.Load().UsedRequestNonces.ContainsKey(secureNonce),
            "Secure pairing downgraded after update or state reload.");
        server.Revoke(state.Phones[0]);
        Check(state.Phones.Count == 1 && state.Phones[0] == second &&
            state.RevokedPhones.Count == 1, "Revoking one phone removed another pairing.");
        var revokedNonce = Guid.NewGuid();
        var control = Open(await Pull(revokedNonce), key, invitation.HostId, physical, revokedNonce);
        Check(control.Name.Length == 0 &&
            JsonDocument.Parse(control.Content).RootElement.GetProperty("type").GetString() == "unpair",
            "The revoked phone did not receive an authenticated removal command.");
        Check((await Pull(revokedNonce)).Length == 0,
            "A revoked phone replayed a pull nonce.");
        var unpairNonce = Guid.NewGuid();
        var identity = $"{Upper(invitation.HostId)}|{Upper(physical)}|{Upper(unpairNonce)}";
        var unpairRequest = new { type = "unpair", version = "1",
            hostID = Upper(invitation.HostId), physicalDeviceID = Upper(physical),
            nonce = Upper(unpairNonce), proof = Hex(Hmac(key, $"unpair|v1|{identity}")) };
        var unpairReply = JsonDocument.Parse(await ExchangeAsync(unpairRequest)).RootElement;
        Check(unpairReply.GetProperty("type").GetString() == "unpair-ack" &&
            unpairReply.GetProperty("proof").GetString() ==
                Hex(Hmac(key, $"unpair-ack|v1|{identity}")),
            "The revoked phone could not finish the idempotent handshake.");
        Check((await ExchangeAsync(new { type = "unpair", version = "1",
            hostID = Upper(invitation.HostId), physicalDeviceID = Upper(second.PhysicalDeviceId),
            nonce = Upper(Guid.NewGuid()), proof = new string('0', 64) })).Length == 0 &&
            state.Phones.Contains(second), "An invalid removal proof revoked another phone.");
        var secondNonce = Guid.NewGuid();
        var secondIdentity = $"{Upper(invitation.HostId)}|{Upper(second.PhysicalDeviceId)}|{Upper(secondNonce)}";
        var secondReply = JsonDocument.Parse(await ExchangeAsync(new {
            type = "unpair", version = "1", hostID = Upper(invitation.HostId),
            physicalDeviceID = Upper(second.PhysicalDeviceId), nonce = Upper(secondNonce),
            proof = Hex(Hmac(secondKey, $"unpair|v1|{secondIdentity}"))
        })).RootElement;
        Check(secondReply.GetProperty("type").GetString() == "unpair-ack" &&
            state.Phones.Count == 0 && state.RevokedPhones.Count == 2,
            "Phone-initiated removal did not persist on Windows.");
        Console.WriteLine("PASS: v3 identity pairing, sealed transfer, ACK, replay and downgrade rejection");
        var directUdid = Environment.GetEnvironmentVariable("MOCHILOG_TEST_DIRECT_UDID");
        var directAddress = Environment.GetEnvironmentVariable("MOCHILOG_TEST_DIRECT_ADDRESS");
        if (!string.IsNullOrWhiteSpace(directUdid) && !string.IsNullOrWhiteSpace(directAddress))
        {
            var probe = new PairedPhone {
                Udid = directUdid, Name = "Direct diagnostic probe", Model = "iPad",
                PhysicalDeviceId = Guid.NewGuid(), ManualAddress = directAddress
            };
            var probeState = new CompanionState();
            try
            {
                var collection = await Collector.CollectAsync(probe, probeState);
                Check(collection.Failed == 0, "Direct wireless collection failed: " + collection.LastError);
                Console.WriteLine($"PASS: direct wireless collection, saved={collection.Saved}, skipped={collection.Skipped}");
            }
            finally
            {
                if (Directory.Exists(StateStore.Root)) Directory.Delete(StateStore.Root, true);
            }
        }
    }
}
