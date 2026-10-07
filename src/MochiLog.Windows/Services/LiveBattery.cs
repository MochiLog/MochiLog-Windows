using System.Text.Json;
using System.Collections.Concurrent;

namespace MochiLog_Windows.Services;

// Values are session-only. This type is never included in CompanionState.
public sealed record LiveBatterySnapshot(Dictionary<string, int> Values, string Revision,
    DateTimeOffset AcquiredAt, bool? Charging)
{
    public static LiveBatterySnapshot Parse(string text)
    {
        if (text.Length > 8192) throw new InvalidDataException("battery_unavailable");
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
        if (!values.Keys.Any(key => key != "CurrentCapacity")) throw new InvalidDataException();
        var revision = root.GetProperty("revision").GetString()!;
        if (revision.Length != 64 || revision.Any(c => !"0123456789abcdef".Contains(c)))
            throw new InvalidDataException();
        var acquired = root.GetProperty("acquiredAt").GetDateTimeOffset();
        bool? charging = raw.TryGetProperty("IsCharging", out var flag) &&
            flag.ValueKind is JsonValueKind.True or JsonValueKind.False ? flag.GetBoolean() : null;
        return new(values, revision, acquired, charging);
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
    public byte[] Response(Guid id, string? revision)
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
                if (revision != snapshot.Revision) {
                    result["values"] = snapshot.Values;
                    if (snapshot.Charging is bool charging) result["charging"] = charging;
                }
            }
            return JsonSerializer.SerializeToUtf8Bytes(result);
        }
    }
}
