using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MochiLog_Windows.Services;

public sealed class CloudLogSharing
{
    // Memory-only consent leases: no grant survives a PC restart.
    private readonly Dictionary<Guid, (string Scope, DateTimeOffset At)> grants = [];
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(15);
    public static bool ValidScope(string? scope) => scope is { Length: 64 } &&
        scope.All(c => "0123456789abcdef".Contains(c));
    public void Update(Guid id, string? scope, DateTimeOffset now)
    {
        if (ValidScope(scope)) grants[id] = (scope!, now);
        else grants.Remove(id);
    }
    public string? Scope(Guid id, DateTimeOffset now) => grants.TryGetValue(id, out var grant) &&
        now >= grant.At && now - grant.At < Lease ? grant.Scope : null;
    public static bool ValidBase(string token)
    {
        var p = token.Split("::");
        if (p.Length is < 2 or > 3 || p[0] is not ("Host" or "Watch") || token.Length > 800) return false;
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
    public bool Eligible(string scope, Guid origin, Guid recipient, IReadOnlyList<PairedPhone> phones, DateTimeOffset now) =>
        origin != recipient && phones.Any(p => p.PhysicalDeviceId == origin) && phones.Any(p => p.PhysicalDeviceId == recipient) &&
        Scope(origin, now) == scope && Scope(recipient, now) == scope;
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
        var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
        var receipts = Receipts(recipient);
        receipts[token + "|" + digest] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var key in receipts.OrderBy(v => v.Value).Take(Math.Max(0, receipts.Count - 10000)).Select(v => v.Key).ToArray()) receipts.Remove(key);
        Directory.CreateDirectory(StateStore.Root);
        var path = LedgerPath(recipient);
        File.WriteAllBytes(path + ".new", JsonSerializer.SerializeToUtf8Bytes(receipts));
        File.Move(path + ".new", path, true);
        // Foreign ACKs never change Delivered, source daily receipts or its queue.
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
            var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(row.Path))).ToLowerInvariant();
            if (!receipts.ContainsKey(token + "|" + digest)) return (token, row.Path);
        }
        return null;
    }
}
