using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Concurrent;

namespace MochiLog_Windows.Services;

public sealed record RawBatteryField([property: JsonPropertyName("path")] string[] Path,
    [property: JsonPropertyName("kind")] string Kind, [property: JsonPropertyName("value")] string Value)
{
    public string Group => Path.Length > 1 ? Path[0] : "";
    public string Label => string.Join(" › ", Path.Length > 1 ? Path.Skip(1) : Path);
    public static RawBatteryField[] Decode(string text, string revision)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length > 262144 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != revision)
            throw new InvalidDataException();
        var fields = JsonSerializer.Deserialize<RawBatteryField[]>(text) ?? [];
        var kinds = new[] { "null", "boolean", "number", "string", "data", "date", "dictionary", "array" };
        if (fields.Length is 0 or > 10000 || fields.Any(f => f is null || f.Path is null ||
            f.Path.Length is 0 or > 32 || f.Path.Any(p => p is null || p.Length > 512) ||
            !kinds.Contains(f.Kind) || f.Value is null || Encoding.UTF8.GetByteCount(f.Value) > 524288))
            throw new InvalidDataException();
        return fields;
    }
}

// Values are session-only. This type is never included in CompanionState.
public sealed record LiveBatterySnapshot(Dictionary<string, int> Values, string Revision,
    DateTimeOffset AcquiredAt, bool? Charging, string? DetailsJSON = null, string? DetailsRevision = null)
{
    public RawBatteryField[] Fields => DetailsJSON is not null && DetailsRevision is not null
        ? RawBatteryField.Decode(DetailsJSON, DetailsRevision) : [];
    public static LiveBatterySnapshot Parse(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > 1048576) throw new InvalidDataException("battery_unavailable");
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1) throw new InvalidDataException();
        var raw = root.GetProperty("values");
        var values = new Dictionary<string, int>();
        foreach (var key in new[] { "CycleCount", "DesignCapacity", "FullChargeCapacity",
            "NominalChargeCapacity", "AppleRawMaxCapacity", "CurrentCapacity" })
        {
            if (!raw.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number)) continue;
            var minimum = key is "CycleCount" or "CurrentCapacity" ? 0 : 1;
            var maximum = key == "CycleCount" ? 100000 : key == "CurrentCapacity" ? 100 : 200000;
            if (number >= minimum && number <= maximum) values[key] = number;
        }
        var details = root.TryGetProperty("detailsJSON", out var detail) ? detail.GetString() : null;
        var detailsRevision = root.TryGetProperty("detailsRevision", out var detailDigest) ? detailDigest.GetString() : null;
        if (details is not null && detailsRevision is not null) _ = RawBatteryField.Decode(details, detailsRevision);
        else if (details is not null || detailsRevision is not null) throw new InvalidDataException();
        if (!values.Keys.Any(key => key != "CurrentCapacity") && details is null) throw new InvalidDataException();
        var revision = root.GetProperty("revision").GetString()!;
        if (revision.Length != 64 || revision.Any(c => !"0123456789abcdef".Contains(c)))
            throw new InvalidDataException();
        var acquired = root.GetProperty("acquiredAt").GetDateTimeOffset();
        bool? charging = raw.TryGetProperty("IsCharging", out var flag) &&
            flag.ValueKind is JsonValueKind.True or JsonValueKind.False ? flag.GetBoolean() : null;
        return new(values, revision, acquired, charging, details, detailsRevision);
    }
}

public sealed class LiveBatteryCache
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, LiveBatterySnapshot> _snapshots = [];
    private readonly HashSet<Guid> _failed = [];
    public void Set(Guid id, LiveBatterySnapshot? snapshot)
    {
        lock (_gate) {
            if (snapshot is null) _failed.Add(id);
            else { _snapshots[id] = snapshot; _failed.Remove(id); }
        }
    }
    public void Remove(Guid id)
    {
        lock (_gate) { _snapshots.Remove(id); _failed.Remove(id); }
    }
    public byte[] Response(Guid id, string? revision, bool includesDetails = false, string? detailsRevision = null)
    {
        lock (_gate) {
            var result = new Dictionary<string, object> {
                ["type"] = "live-battery", ["version"] = 1,
                ["state"] = _failed.Contains(id) ? "unavailable" : "waiting"
            };
            if (_snapshots.TryGetValue(id, out var snapshot)) {
                result["state"] = _failed.Contains(id) ? "stale" : "current";
                result["revision"] = snapshot.Revision;
                result["acquiredAt"] = snapshot.AcquiredAt.ToString("O");
                if (includesDetails && snapshot.DetailsJSON is { } detail && snapshot.DetailsRevision is { } digest) {
                    result["detailsVersion"] = 1;
                    result["detailsRevision"] = digest;
                    if (detailsRevision != digest) result["detailsJSON"] = detail;
                }
                if (revision != snapshot.Revision) {
                    result["values"] = snapshot.Values;
                    if (snapshot.Charging is bool charging) result["charging"] = charging;
                }
            }
            return JsonSerializer.SerializeToUtf8Bytes(result);
        }
    }
}
