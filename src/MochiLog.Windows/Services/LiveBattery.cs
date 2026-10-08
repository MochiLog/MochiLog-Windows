using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

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

public sealed record BatterySummaryRow(string Key, string? Value, string Kind = "number", string Unit = "")
{
    public string Display(Func<string, string> text) => Value is null ? text("live_missing")
        : Kind == "boolean" ? text(Value == "true" ? "live_true" : "live_false") : Value + Unit;
}

// Exact paths only. New, ambiguous and invalid fields remain available in optional details.
public static class BatteryPresentation
{
    public static readonly string[] PrimaryKeys = ["CycleCount", "DesignCapacity", "NominalChargeCapacity",
        "AppleRawMaxCapacity", "FullChargeCapacity", "CurrentCapacity"];
    public static readonly string[] ExtraKeys = ["IsCharging", "FullyCharged", "ExternalConnected", "ExternalChargeCapable",
        "AppleRawExternalConnected", "BatteryInstalled", "AtCriticalLevel", "Voltage", "Amperage", "InstantAmperage", "Serial"];
    public static BatterySummaryRow? Extra(RawBatteryField field)
    {
        if (field.Path.Length != 1 || !ExtraKeys.Contains(field.Path[0])) return null;
        var key = field.Path[0];
        if (key is "Voltage" or "Amperage" or "InstantAmperage") {
            if (field.Kind != "number" || !long.TryParse(field.Value, System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out var number) ||
                (key == "Voltage" ? number is < 0 or > 100000 : number is < -2000000 or > 2000000)) return null;
            return new(key, field.Value, Unit: key == "Voltage" ? " mV" : " mA");
        }
        if (key == "Serial") return field.Kind == "string" && field.Value.Length > 0 ? new(key, field.Value, "string") : null;
        return field.Kind == "boolean" && field.Value is "true" or "false" ? new(key, field.Value, "boolean") : null;
    }
    public static BatterySummaryRow[] Summary(Dictionary<string, int> values, bool? charging, RawBatteryField[] fields)
    {
        var rows = PrimaryKeys.Select(key => new BatterySummaryRow(key,
            values.TryGetValue(key, out var number) ? number.ToString("N0") : null,
            Unit: key == "CycleCount" ? "" : key == "CurrentCapacity" ? "%" : " mAh")).ToList();
        foreach (var key in ExtraKeys) {
            if (key == "IsCharging" && charging is not null) rows.Add(new(key, charging.Value ? "true" : "false", "boolean"));
            else if (fields.FirstOrDefault(field => field.Path.SequenceEqual(new[] { key })) is { } field && Extra(field) is { } row)
                rows.Add(row);
        }
        return rows.ToArray();
    }
    public static RawBatteryField[] Details(Dictionary<string, int> values, bool? charging, RawBatteryField[] fields) => fields.Where(field => {
        if (Extra(field) is { } row)
            return row.Key == "IsCharging" && charging is not null && row.Value != (charging.Value ? "true" : "false");
        var key = field.Path.Last();
        if (!PrimaryKeys.Contains(key) || field.Kind != "number" || !int.TryParse(field.Value,
            System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var number) ||
            !values.TryGetValue(key, out var displayed) || displayed != number) return true;
        var isRoot = field.Path.SequenceEqual(new[] { key });
        var isCapacity = field.Path.SequenceEqual(new[] { "BatteryData", key }) && key is not ("CycleCount" or "CurrentCapacity");
        return !isRoot && !isCapacity;
    }).ToArray();
}

// Values are session-only. This type is never included in CompanionState.
public sealed record LiveBatterySnapshot(Dictionary<string, int> Values, string Revision,
    DateTimeOffset AcquiredAt, bool? Charging, string? DetailsJSON = null, string? DetailsRevision = null)
{
    public RawBatteryField[] Fields => DetailsJSON is not null && DetailsRevision is not null
        ? RawBatteryField.Decode(DetailsJSON, DetailsRevision) : [];
    public static LiveBatterySnapshot Parse(string text)
    {
        if (text.StartsWith("<?xml", StringComparison.Ordinal)) return FromRegistry(text);
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

    // The helper only fetches and encodes a plist. Interpretation and hashes are native.
    public static LiveBatterySnapshot FromRegistry(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > 1048576) throw new InvalidDataException();
        using var input = new StringReader(text);
        using var reader = XmlReader.Create(input, new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 1048576
        });
        var root = XDocument.Load(reader).Root;
        if (root?.Name != "plist" || root.Elements().Count() != 1 || root.Elements().First().Name != "dict")
            throw new InvalidDataException();
        var fields = new List<RawBatteryField>();
        void Visit(XElement node, string[] path) {
            if (path.Length > 32 || fields.Count >= 10000) throw new InvalidDataException();
            var children = node.Elements().ToArray();
            var type = node.Name.LocalName;
            if (node.Name.Namespace != XNamespace.None) throw new InvalidDataException();
            if (type == "dict" && children.Length > 0) {
                if (children.Length % 2 != 0) throw new InvalidDataException();
                var entries = new SortedDictionary<string, XElement>(StringComparer.Ordinal);
                for (var index = 0; index < children.Length; index += 2) {
                    if (children[index].Name != "key" || children[index].Value.Length > 512 ||
                        !entries.TryAdd(children[index].Value, children[index + 1])) throw new InvalidDataException();
                }
                foreach (var entry in entries) Visit(entry.Value, [.. path, entry.Key]);
                return;
            }
            if (type == "array" && children.Length > 0) {
                for (var index = 0; index < children.Length; index++) Visit(children[index], [.. path, $"[{index}]"]);
                return;
            }
            if (children.Length > 0) throw new InvalidDataException();
            var kind = type switch {
                "integer" or "real" => "number", "true" or "false" => "boolean", "string" => "string",
                "data" => "data", "date" => "date", "dict" => "dictionary", "array" => "array",
                _ => throw new InvalidDataException()
            };
            var value = type switch {
                "true" or "false" => type, "dict" => "{}", "array" => "[]",
                "data" => Convert.ToBase64String(Convert.FromBase64String(node.Value)), _ => node.Value
            };
            if (value.Length > 131072 || path.Length == 0) throw new InvalidDataException();
            fields.Add(new(path, kind, value));
        }
        Visit(root.Elements().First(), []);
        if (fields.Count == 0) throw new InvalidDataException();
        var values = new Dictionary<string, int>();
        foreach (var key in BatteryPresentation.PrimaryKeys) {
            var field = key is "CycleCount" or "CurrentCapacity" ? fields.FirstOrDefault(f => f.Path.SequenceEqual(new[] { key }))
                : fields.FirstOrDefault(f => f.Path.SequenceEqual(new[] { "BatteryData", key }))
                    ?? fields.FirstOrDefault(f => f.Path.SequenceEqual(new[] { key }));
            if (field?.Kind != "number" || !decimal.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
                number != decimal.Truncate(number)) continue;
            var minimum = key is "CycleCount" or "CurrentCapacity" ? 0 : 1;
            var maximum = key == "CycleCount" ? 100000 : key == "CurrentCapacity" ? 100 : 200000;
            if (number >= minimum && number <= maximum) values[key] = (int)number;
        }
        var chargingField = fields.FirstOrDefault(f => f.Path.SequenceEqual(new[] { "IsCharging" }));
        bool? charging = chargingField?.Kind == "boolean" ? chargingField.Value == "true" : null;
        var core = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var value in values) core[value.Key] = value.Value;
        if (charging is not null) core["IsCharging"] = charging.Value;
        // Match the wire's sorted object keys; integers stay strings in detail rows.
        var details = JsonSerializer.Serialize(fields.Select(f => new SortedDictionary<string, object>(StringComparer.Ordinal) {
            ["path"] = f.Path, ["kind"] = f.Kind, ["value"] = f.Value
        }));
        if (Encoding.UTF8.GetByteCount(details) > 262144) throw new InvalidDataException();
        static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        return new(values, Hash(JsonSerializer.Serialize(core)), DateTimeOffset.UtcNow, charging, details, Hash(details));
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
