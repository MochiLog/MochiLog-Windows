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
        var computerData = Encoding.UTF8.GetBytes("computer archive line\n");
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
        Check(chunk is not null && chunk["data"] as string ==
            Convert.ToBase64String(computerData),
            "Computer archive chunk did not match the requested day.");
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
        await client.ConnectAsync(IPAddress.Loopback, TransferServer.Port);
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
        CheckArchiveExchange();
        var state = new CompanionState();
        using var server = new TransferServer(state);
        server.Start();
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
        var ackNonce = Guid.NewGuid();
        var final = Open(await Pull(ackNonce, first.Name), key,
            invitation.HostId, physical, ackNonce);
        Check(final.Name.Length == 0 && !File.Exists(Path.Combine(queue, name)),
            "File acknowledgement did not complete the batch.");
        Check(state.Delivered.Contains(Upper(physical) + "|" + first.Name),
            "Delivered file was not marked.");
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
        Console.WriteLine("PASS: v3 identity pairing, encrypted v2 log, ACK, and replay rejection");
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
