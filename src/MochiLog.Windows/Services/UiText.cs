using System.Globalization;
using System.Text.Json;

namespace MochiLog_Windows.Services;

public static class UiText
{
    private static readonly JsonDocument? Windows = Load("WindowsStrings.json");
    private static readonly JsonDocument? Shared = Load("Shared.xcstrings");

    private static JsonDocument? Load(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "resources", name);
        return File.Exists(path) ? JsonDocument.Parse(File.ReadAllText(path)) : null;
    }

    private static string Language
    {
        get
        {
            var culture = CultureInfo.CurrentUICulture;
            if (culture.TwoLetterISOLanguageName == "zh")
            {
                var traditional = culture.Name.Contains("Hant", StringComparison.OrdinalIgnoreCase) ||
                    culture.Name.EndsWith("-TW", StringComparison.OrdinalIgnoreCase) ||
                    culture.Name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase) ||
                    culture.Name.EndsWith("-MO", StringComparison.OrdinalIgnoreCase);
                return traditional ? "zh-Hant" : "zh-Hans";
            }
            return culture.TwoLetterISOLanguageName is "ja" or "de" or "es" or "fr" or "ko"
                ? culture.TwoLetterISOLanguageName : "en";
        }
    }

    public static string Get(string key)
    {
        var language = Language;
        if (Windows is not null && Windows.RootElement.TryGetProperty(language, out var group) &&
            group.TryGetProperty(key, out var value)) return value.GetString() ?? key;
        if (Shared is not null && Shared.RootElement.GetProperty("strings").TryGetProperty(key, out var item) &&
            item.TryGetProperty("localizations", out var localizations))
        {
            if (!localizations.TryGetProperty(language, out var localized))
                localizations.TryGetProperty("en", out localized);
            if (localized.ValueKind == JsonValueKind.Object &&
                localized.TryGetProperty("stringUnit", out var unit) &&
                unit.TryGetProperty("value", out var text)) return text.GetString() ?? key;
        }
        if (Windows is not null && Windows.RootElement.TryGetProperty("en", out var english) &&
            english.TryGetProperty(key, out var fallback)) return fallback.GetString() ?? key;
        return key;
    }

    public static string Format(string key, params object[] values) =>
        string.Format(CultureInfo.CurrentUICulture, Get(key), values);
}
