using System.Text;
using System.Text.Json;

namespace MochiLog_Windows.Services;

public static class DiagnosticLogViewer
{
    private const string LegacyHeader = "# {\"type\":\"mochilog-diagnostic-log\",\"formatVersion\":1,\"appVersion\":\"unknown\",\"build\":\"unknown\"}";
    public static string[] Categories(string text)
    {
        var categories = new HashSet<string>(StringComparer.Ordinal);
        Visit(text, (category, _, _) => categories.Add(category));
        return DiagnosticLogArchive.Categories.Where(categories.Contains).ToArray();
    }
    public static string Text(string text, string? category)
    {
        if (category is null || !DiagnosticLogArchive.Categories.Contains(category)) return text;
        var output = new StringBuilder();
        string? previous = null;
        Visit(text, (actual, header, line) => {
            if (actual != category) return;
            if (previous != header) { output.AppendLine(header); previous = header; }
            output.AppendLine(line);
        });
        return output.ToString().TrimEnd('\r', '\n');
    }
    private static void Visit(string text, Action<string, string, string> consume)
    {
        var header = LegacyHeader;
        string? fixedCategory = null;
        var supported = true;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line) {
            if (line.Length == 0) continue;
            if (line.StartsWith("# ", StringComparison.Ordinal)) {
                try {
                    using var parsed = JsonDocument.Parse(line[2..]);
                    var value = parsed.RootElement;
                    if (value.ValueKind == JsonValueKind.Object &&
                        value.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String &&
                        type.GetString() == "mochilog-diagnostic-log" &&
                        value.TryGetProperty("formatVersion", out var version) && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number)) {
                        header = line;
                        supported = number is 1 or 2;
                        fixedCategory = value.TryGetProperty("category", out var name) &&
                            name.ValueKind == JsonValueKind.String && name.GetString() is { } candidate &&
                            DiagnosticLogArchive.Categories.Contains(candidate) ? candidate : null;
                        continue;
                    }
                } catch (JsonException) { /* Unknown header remains visible as an ordinary line. */ }
            }
            consume(supported ? fixedCategory ?? DiagnosticLogArchive.Category(line) : "general", header, line);
        }
    }
}
