using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace MochiLog_Windows.Services;

// Same header and paths as the Swift archive. The compatibility stream is
// append-only: older peers' byte offsets must never be invalidated by sorting.
public static class DiagnosticLogArchive
{
    public const int FormatVersion = 2;
    private static readonly object Gate = new();
    public static readonly string[] Categories = ["background", "local-collection", "pc-transfer", "live-battery", "cloud-sync", "pairing", "general"];
    public static string Category(string message)
    {
        var text = message.ToLowerInvariant();
        if (text.Contains("local scheduler:") || text.Contains("background") || text.Contains("os wake")) return "background";
        if (text.Contains("live battery") || text.Contains("current battery") || text.Contains("battery snapshot")) return "live-battery";
        if (text.Contains("pairing") || text.Contains("usb trust") || text.Contains("ペアリング")) return "pairing";
        if (text.Contains("cloud sharing") || text.Contains("cloudkit") || text.Contains("icloud")) return "cloud-sync";
        if (text.Contains("local diagnostics") || text.Contains("local collection")) return "local-collection";
        if (text.Contains("connection:") || text.Contains("transfer") || text.Contains("preflight:") ||
            text.Contains("bonjour") || text.Contains("collection") || text.Contains("受信")) return "pc-transfer";
        return "general";
    }
    private static string Safe(string value)
    {
        var clean = new string(value.Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-').Take(64).ToArray());
        return clean.Length == 0 ? "unknown" : clean;
    }
    private static bool ValidDay(string day) => DateOnly.TryParseExact(day, "yyyy-MM-dd",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    public static void Append(string eventText, string root, string? appVersion = null,
        string? build = null, bool legacy = false)
    {
        var day = eventText.Length >= 10 ? eventText[..10] : "";
        if (!ValidDay(day)) return;
        var assembly = Assembly.GetExecutingAssembly();
        appVersion ??= assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";
        build ??= assembly.GetName().Version?.ToString() ?? "unknown";
        lock (Gate) {
            var category = Category(eventText);
            var version = legacy ? 1 : FormatVersion;
            var identity = legacy ? "legacy" : Safe(appVersion) + "-" + Safe(build);
            var folder = Path.Combine(root, day);
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"{category}-v{version}-{identity}.log");
            var aggregate = Path.Combine(root, day + ".log");
            var marker = Path.Combine(folder, $".compat-v{version}-{identity}");
            var header = "# " + JsonSerializer.Serialize(new {
                type = "mochilog-diagnostic-log", formatVersion = version, category,
                appVersion = legacy ? "unknown" : appVersion, build = legacy ? "unknown" : build,
                recordLayout = "timestamp | message", createdAt = eventText.Split(' ')[0],
                timeZone = TimeZoneInfo.Local.Id
            }) + "\n";
            if (!File.Exists(file)) File.WriteAllText(file, header);
            File.AppendAllText(file, eventText + "\n");
            if (!File.Exists(marker)) {
                var combined = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(header[2..])!;
                combined["category"] = JsonSerializer.SerializeToElement("combined");
                var combinedHeader = "# " + JsonSerializer.Serialize(combined) + "\n";
                File.AppendAllText(aggregate, combinedHeader);
                File.WriteAllText(marker, combinedHeader);
            }
            File.AppendAllText(aggregate, eventText + "\n");
        }
    }
    public static void RemoveDay(string day, string root)
    {
        if (!ValidDay(day)) return;
        lock (Gate) {
            File.Delete(Path.Combine(root, day + ".log"));
            var folder = Path.Combine(root, day);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
