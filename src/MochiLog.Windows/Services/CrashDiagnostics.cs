using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MochiLog_Windows.Services;

public sealed record CrashDiagnostic(
    DateTimeOffset OccurredAt, string Source, string ExceptionType,
    string Message, string StackTrace, int HResult, string Fingerprint,
    string AppVersion, string OsVersion);

public static class CrashDiagnostics
{
    private static readonly object Gate = new();
    private static string FileName => Path.Combine(StateStore.Root, "last-crash.json");

    public static void Record(string source, Exception? error)
    {
        try
        {
            var type = error?.GetType().FullName ?? "UnknownException";
            var stack = Truncate(error?.ToString() ?? "No exception details", 12_000);
            var fingerprintSource = type + "|" + stack.Split('\n').FirstOrDefault();
            var fingerprint = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(fingerprintSource))).Substring(0, 16);
            var report = new CrashDiagnostic(DateTimeOffset.Now, source, type,
                Truncate(error?.Message ?? "No exception message", 1_000), stack,
                error?.HResult ?? 0, fingerprint,
                typeof(CrashDiagnostics).Assembly.GetName().Version?.ToString() ?? "unknown",
                Environment.OSVersion.VersionString);
            lock (Gate)
            {
                Directory.CreateDirectory(StateStore.Root);
                var temporary = FileName + ".new";
                File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(report));
                File.Move(temporary, FileName, true);
            }
        }
        catch (Exception) { /* Crash reporting must not replace the original exception. */ }
    }

    public static CrashDiagnostic? Latest()
    {
        try
        {
            lock (Gate) return JsonSerializer.Deserialize<CrashDiagnostic>(File.ReadAllBytes(FileName));
        }
        catch (Exception) { return null; }
    }

    public static string SummaryText => Latest() is { } crash
        ? $"Last crash: {crash.OccurredAt:O} | {crash.Source} | {crash.ExceptionType} " +
          $"| HRESULT 0x{crash.HResult:X8} | {crash.Fingerprint}{Environment.NewLine}" +
          crash.StackTrace
        : "";

    private static string Truncate(string text, int length) =>
        text.Length <= length ? text : text[..length];
}
