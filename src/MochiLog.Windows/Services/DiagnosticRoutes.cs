using System.Text.Json;

namespace MochiLog_Windows.Services;

/// Endpoint hints cannot establish identity: the collector authenticates each
/// connection with the existing OS pairing record for the selected UDID.
public static class DiagnosticRoutes
{
    public sealed record Candidate(string Address, string Source);
    public sealed record Report(string? Manual, string? TransferPeer, string[] Hints,
        DateTimeOffset? HintsAt, string? Verified, DateTimeOffset? VerifiedAt, Candidate[] Candidates);
    private sealed class Cache
    {
        public Cache() { }
        public int Version { get; set; } = 1;
        public string[] Hints { get; set; } = [];
        public DateTimeOffset? HintsAt { get; set; }
        public string? Verified { get; set; }
        public DateTimeOffset? VerifiedAt { get; set; }
    }
    private static readonly object Gate = new();
    public static string? PrivateLanAddress(string text)
    {
        var parts = text.Split('.');
        if (parts.Length != 4) return null;
        var bytes = new byte[4];
        for (var i = 0; i < 4; i++)
            if (!byte.TryParse(parts[i], out bytes[i]) || bytes[i].ToString() != parts[i]) return null;
        return bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
            bytes[0] == 192 && bytes[1] == 168 ? string.Join('.', bytes) : null;
    }
    private static string FilePath(Guid id) => Path.Combine(StateStore.Root, "DiagnosticRoutes", id.ToString("D") + ".json");
    private static Cache Load(Guid id)
    {
        try {
            var path = FilePath(id);
            if (!File.Exists(path) || new FileInfo(path).Length > 8192) return new();
            var cache = JsonSerializer.Deserialize<Cache>(File.ReadAllBytes(path));
            return cache?.Version == 1 && cache.Hints is not null ? cache : new();
        } catch { return new(); }
    }
    private static void Save(Guid id, Cache cache)
    {
        var path = FilePath(id);
        var temporary = path + ".tmp";
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(cache));
            File.Move(temporary, path, true);
        } catch (Exception error) {
            Collector.Trace?.Invoke($"Diagnostic route cache save failed; device={id:D}; error={error.Message}");
        } finally {
            try { File.Delete(temporary); } catch { }
        }
    }
    public static bool ReceiveHints(string encoded, Guid id, DateTimeOffset? at = null)
    {
        if (encoded.Length > 512) return false;
        string[]? supplied;
        try { supplied = JsonSerializer.Deserialize<string[]>(encoded); } catch { return false; }
        if (supplied is null || supplied.Length > 4 || supplied.Any(value => value is null || PrivateLanAddress(value) is null)) return false;
        var hints = supplied.Distinct().ToArray();
        var now = at ?? DateTimeOffset.UtcNow;
        lock (Gate) {
            var cache = Load(id);
            var changed = !cache.Hints.SequenceEqual(hints);
            if (!changed && cache.HintsAt is { } before && now - before < TimeSpan.FromMinutes(5)) return false;
            cache.Hints = hints; cache.HintsAt = now; Save(id, cache);
            if (changed) Collector.Trace?.Invoke($"Diagnostic route hints updated; device={id:D}; count={hints.Length}; source=sealed v3 request; verified=false; received={now.ToLocalTime():O}");
            return changed;
        }
    }
    public static void RememberVerified(string address, Guid id, DateTimeOffset? at = null)
    {
        var canonical = PrivateLanAddress(address);
        if (canonical is null) return;
        var now = at ?? DateTimeOffset.UtcNow;
        lock (Gate) {
            var cache = Load(id);
            if (cache.Verified == canonical && cache.VerifiedAt is { } before && now - before < TimeSpan.FromMinutes(5)) return;
            cache.Verified = canonical; cache.VerifiedAt = now; Save(id, cache);
            Collector.Trace?.Invoke($"Diagnostic route authenticated; device={id:D}; address={canonical}; verified=OS pairing; at={now.ToLocalTime():O}");
        }
    }
    public static IReadOnlyList<Candidate> Candidates(PairedPhone phone, DateTimeOffset? at = null)
    {
        if (phone.ManualAddress is { Length: > 0 } manual) return [new(manual, "manual")];
        var now = at ?? DateTimeOffset.UtcNow;
        static bool Fresh(DateTimeOffset? date, DateTimeOffset now, TimeSpan age) => date is { } value && now >= value && now - value <= age;
        lock (Gate) {
            var cache = Load(phone.PhysicalDeviceId);
            var candidates = new List<Candidate>();
            if (Fresh(cache.HintsAt, now, TimeSpan.FromHours(1)))
                candidates.AddRange(cache.Hints.Where(value => value is not null && PrivateLanAddress(value) is not null)
                    .Select(value => new Candidate(value, "authenticated mobile LAN hint (unverified)")));
            if (Fresh(cache.VerifiedAt, now, TimeSpan.FromDays(7)) && cache.Verified is { } address && PrivateLanAddress(address) is not null)
                candidates.Add(new(address, "previous OS-authenticated endpoint"));
            // A mobile app can be paused after today's logs or be suspended.
            // Retain recent sealed hints as fallbacks; OS pairing still proves identity.
            if (!Fresh(cache.HintsAt, now, TimeSpan.FromHours(1)) && Fresh(cache.HintsAt, now, TimeSpan.FromHours(24)))
                candidates.AddRange(cache.Hints.Where(value => value is not null && PrivateLanAddress(value) is not null)
                    .Select(value => new Candidate(value, "recent authenticated mobile LAN hint (unverified)")));
            // Last candidate preserves older mobile versions without hints.
            if (phone.LastKnownAddress is { Length: > 0 } peer)
                candidates.Add(new(peer, "transfer peer fallback (unverified)"));
            return candidates.DistinctBy(value => value.Address).ToArray();
        }
    }
    public static Report Describe(PairedPhone phone)
    {
        lock (Gate) {
            var cache = Load(phone.PhysicalDeviceId);
            return new(phone.ManualAddress, phone.LastKnownAddress,
                cache.Hints.Where(value => value is not null && PrivateLanAddress(value) is not null)
                    .Take(4).ToArray(), cache.HintsAt, cache.Verified, cache.VerifiedAt,
                Candidates(phone).Take(8).ToArray());
        }
    }
}
