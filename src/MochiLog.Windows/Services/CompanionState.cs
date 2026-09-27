using System.Security.Cryptography;
using System.Text.Json;

namespace MochiLog_Windows.Services;

public sealed class PairedPhone
{
    public string Udid { get; set; } = "";
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public Guid PhysicalDeviceId { get; set; }
    public byte[] Secret { get; set; } = [];
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? LastKnownAddress { get; set; }
    public string? ManualAddress { get; set; }
}

public sealed class CompanionState
{
    public Guid HostId { get; set; } = Guid.NewGuid();
    public List<PairedPhone> Phones { get; set; } = [];
    // Revoked keys authenticate a delayed unpair acknowledgement only.
    // These phones are excluded from discovery, collection and transfer.
    public List<PairedPhone> RevokedPhones { get; set; } = [];
    public HashSet<string> Delivered { get; set; } = [];
    public Dictionary<string, byte[]> PhoneDiagnostics { get; set; } = [];
    public Dictionary<string, CollectionResult> LastCollections { get; set; } = [];
}

public static class StateStore
{
    #if MOCHILOG_PROTOCOL_TEST
    public static string Root { get; } = Path.Combine(Path.GetTempPath(),
        "MochiLog-Windows-Protocol-Test", Guid.NewGuid().ToString("N"));
    #else
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MochiLog Windows");
    #endif
    private static string FileName => Path.Combine(Root, "state.bin");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly object Gate = new();

    public static CompanionState Load()
    {
        lock (Gate)
        {
            Directory.CreateDirectory(Root);
            if (!File.Exists(FileName)) return new CompanionState();
            try
            {
                var encrypted = File.ReadAllBytes(FileName);
                var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return JsonSerializer.Deserialize<CompanionState>(plain, JsonOptions) ?? new CompanionState();
            }
            catch (Exception error)
            {
                throw new InvalidDataException("The paired-device store cannot be opened; pairing keys were not reset.", error);
            }
        }
    }

    public static void Save(CompanionState state)
    {
        lock (state)
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Root);
                var plain = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
                var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                var temporary = FileName + ".new";
                File.WriteAllBytes(temporary, encrypted);
                File.Move(temporary, FileName, true);
            }
        }
    }
}
