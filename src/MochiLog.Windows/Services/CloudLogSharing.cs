using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MochiLog_Windows.Services;

public sealed class CloudLogSharing
{
    // Memory-only consent leases: no grant survives a PC restart.
    private readonly Dictionary<Guid, (string Scope, DateTimeOffset At)> grants = [];
    public Action<string>? Audit { get; set; }
    private readonly Dictionary<string, string> lastDecision = [];
    private readonly Dictionary<string, (long Size, DateTime Modified, string Digest)> digests = [];
    public static string DebugLabel(string token) => Parse(token) is { } p ? $"Shared[origin={p.Origin:D}]::{p.Base}" :
        token.StartsWith("Shared::", StringComparison.Ordinal) ? "Shared[invalid token]" :
        new string(token.Where(c => !char.IsControl(c)).Take(800).ToArray());
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(15);
    public static bool ValidScope(string? scope) => scope is { Length: 64 } &&
        scope.All(c => "0123456789abcdef".Contains(c));
    public void Update(Guid id, string? scope, DateTimeOffset now)
    {
        if (Scope(id, now) != (ValidScope(scope) ? scope : null))
            Audit?.Invoke($"Cloud sharing: consent changed device={id:D}, state={(ValidScope(scope) ? "confirmed" : "off/unavailable")}, leaseSeconds={Lease.TotalSeconds}");
        if (ValidScope(scope)) grants[id] = (scope!, now);
        else grants.Remove(id);
    }
    public string? Scope(Guid id, DateTimeOffset now) => grants.TryGetValue(id, out var grant) &&
        now >= grant.At && now - grant.At < Lease ? grant.Scope : null;
    public static bool ValidBase(string token)
    {
        var p = token.Split("::");
        if (p.Length is < 2 or > 3 || p[0] is not ("Host" or "Watch") || token.Length > 800 || token.Any(char.IsControl)) return false;
        if (p.Length == 3 && (p[0] != "Watch" || !Regex.IsMatch(p[1], "^ProxiedDevice-[a-fA-F0-9]+$"))) return false;
        var n = p[^1];
        return n.StartsWith("Analytics-", StringComparison.Ordinal) && n.EndsWith(".ips.ca.synced", StringComparison.Ordinal) &&
            !n.Contains('/') && !n.Contains('\\') && !n.Contains("..") && !n.Contains("session", StringComparison.OrdinalIgnoreCase);
    }
    public static (string Scope, Guid Origin, string Base)? Parse(string token)
    {
        var p = token.Split("::");
        if (p.Length is < 5 or > 6 || p[0] != "Shared" || !ValidScope(p[1]) || !Guid.TryParse(p[2], out var origin)) return null;
        var b = string.Join("::", p.Skip(3));
        return ValidBase(b) ? (p[1], origin, b) : null;
    }
    public bool Eligible(string scope, Guid origin, Guid recipient, IReadOnlyList<PairedPhone> phones, DateTimeOffset now)
    {
        if (origin == recipient) return false;
        var reason = !phones.Any(p => p.PhysicalDeviceId == origin) ? "source no longer paired" :
            !phones.Any(p => p.PhysicalDeviceId == recipient) ? "recipient no longer paired" :
            Scope(recipient, now) is null ? "recipient consent absent/expired" :
            Scope(origin, now) is null ? "source consent absent/expired" :
            Scope(origin, now) != scope || Scope(recipient, now) != scope ? "account scopes differ" :
            "allowed: both sync settings and same account confirmed";
        var key = $"{origin:D}|{recipient:D}";
        if (!lastDecision.TryGetValue(key, out var previous) || previous != reason) {
            lastDecision[key] = reason;
            Audit?.Invoke($"Cloud sharing: source={origin:D}, recipient={recipient:D}, pairedDevices={phones.Count}, decision={reason}");
        }
        return reason.StartsWith("allowed:", StringComparison.Ordinal);
    }
    private static string Base(StoredBatteryLog row) => string.Join("::", new[] { row.Kind }
        .Concat(row.Source is null ? [] : new[] { row.Source }).Append(row.Name));
    public string? Resolve(string token, Guid recipient, IReadOnlyList<PairedPhone> phones, DateTimeOffset now)
    {
        if (Parse(token) is not { } p || !Eligible(p.Scope, p.Origin, recipient, phones, now)) return null;
        var phone = phones.First(v => v.PhysicalDeviceId == p.Origin);
        var queued = Path.Combine(TransferServer.QueuePath(phone), p.Base.Replace("::", Path.DirectorySeparatorChar.ToString()));
        if (File.Exists(queued)) return queued;
        return BatteryLogStorage.List(phones).FirstOrDefault(row => !row.Pending && row.DeviceId == p.Origin && Base(row) == p.Base)?.Path;
    }
    private static string ReceiptKey(string token, string digest)
    {
        var parsed = Parse(token) ?? throw new IOException("Invalid shared receipt token");
        return $"{parsed.Origin:D}|{parsed.Base}|{digest}";
    }
    private static string LedgerPath(Guid recipient) => Path.Combine(StateStore.Root, $"cloud-delivered-{recipient:D}.json");
    private static Dictionary<string, double> Receipts(Guid recipient)
    {
        var file = LedgerPath(recipient);
        if (!File.Exists(file)) return [];
        // A damaged ledger fails the request closed; it is not reset silently.
        return JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllBytes(file)) ?? [];
    }
    public void Acknowledge(string token, string file, Guid recipient)
    {
        if (new FileInfo(file).Length > 64 * 1024 * 1024) throw new IOException("Shared log exceeds size limit");
        var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
        var receipts = Receipts(recipient);
        receipts[ReceiptKey(token, digest)] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var key in receipts.OrderBy(v => v.Value).Take(Math.Max(0, receipts.Count - 10000)).Select(v => v.Key).ToArray()) receipts.Remove(key);
        Directory.CreateDirectory(StateStore.Root);
        var path = LedgerPath(recipient);
        File.WriteAllBytes(path + ".new", JsonSerializer.SerializeToUtf8Bytes(receipts));
        File.Move(path + ".new", path, true);
        // Foreign ACKs never change Delivered, source daily receipts or its queue.
    }
    private string Digest(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > 64 * 1024 * 1024) throw new IOException("Shared log exceeds size limit");
        if (digests.TryGetValue(path, out var cached) && cached.Size == info.Length && cached.Modified == info.LastWriteTimeUtc) return cached.Digest;
        var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        if (digests.Count >= 512) digests.Clear();
        digests[path] = (info.Length, info.LastWriteTimeUtc, digest);
        return digest;
    }
    public (string Token, string File)? Next(Guid recipient, IReadOnlyList<PairedPhone> phones, DateTimeOffset now)
    {
        if (Scope(recipient, now) is not { } scope) return null;
        var receipts = Receipts(recipient);
        foreach (var row in BatteryLogStorage.List(phones).OrderBy(r => r.Path, StringComparer.Ordinal))
        {
            var b = Base(row);
            if (!ValidBase(b) || !Eligible(scope, row.DeviceId, recipient, phones, now) || row.Size > 64 * 1024 * 1024) continue;
            var token = $"Shared::{scope}::{row.DeviceId:D}::{b}".Replace(row.DeviceId.ToString("D"), row.DeviceId.ToString("D").ToUpperInvariant());
            var digest = Digest(row.Path);
            if (!receipts.ContainsKey(ReceiptKey(token, digest))) return (token, row.Path);
        }
        return null;
    }
}
