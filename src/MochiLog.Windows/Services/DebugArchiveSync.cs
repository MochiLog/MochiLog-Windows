using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MochiLog_Windows.Services;

// The archive fields travel inside the existing nonce-bound AES-GCM diagnostics
// payload. A chunk is appended only at its expected byte offset, so retries
// cannot duplicate or skip earlier log data.
public static class DebugArchiveSync
{
    private const int MaximumDayBytes = 64_000_000;
    private static readonly object SnapshotLock = new();
    private static readonly Dictionary<Guid, (DateTimeOffset Captured,
        Dictionary<string, int> Manifest)> Snapshots = [];
    private static string LocalRoot => Path.Combine(StateStore.Root, "DebugLogs");
    public static int RetentionDays {
        get {
            var file = Path.Combine(StateStore.Root, "debug-retention-days.txt");
            try { return int.TryParse(File.ReadAllText(file), out var days)
                ? Math.Clamp(days, 7, 365) : 30; }
            catch (IOException) { return 30; }
            catch (UnauthorizedAccessException) { return 30; }
        }
    }
    private static string RemoteRoot(Guid deviceId) => Path.Combine(StateStore.Root,
        "PhoneDebugLogs", deviceId.ToString("D").ToUpperInvariant());
    private static string? Expand(string compact) => Regex.IsMatch(compact, "^[0-9]{8}$") &&
        DateOnly.TryParseExact(compact, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date) ? date.ToString("yyyy-MM-dd",
            CultureInfo.InvariantCulture) : null;
    private static string Compact(string day) => day.Replace("-", "", StringComparison.Ordinal);

    private static string[] Days(string directory) => Directory.Exists(directory)
        ? Directory.EnumerateFiles(directory, "*.log")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(day => day is not null && DateOnly.TryParseExact(day, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .Select(day => day!)
            .OrderByDescending(day => day, StringComparer.Ordinal).ToArray() : [];

    public static IReadOnlyList<string> PhoneDays(Guid deviceId) => Days(RemoteRoot(deviceId));

    public static string PhoneText(Guid deviceId, string day)
    {
        if (!DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _)) return "";
        try { return File.ReadAllText(Path.Combine(RemoteRoot(deviceId), day + ".log")); }
        catch (IOException) { return ""; }
        catch (UnauthorizedAccessException) { return ""; }
    }

    public static void PruneAllRemote(int retentionDays)
    {
        var root = Path.Combine(StateStore.Root, "PhoneDebugLogs");
        if (!Directory.Exists(root)) return;
        var cutoff = DateOnly.FromDateTime(DateTime.Today)
            .AddDays(1 - Math.Clamp(retentionDays, 7, 365))
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        foreach (var folder in Directory.EnumerateDirectories(root))
            foreach (var old in Days(folder).Where(day =>
                string.CompareOrdinal(day, cutoff) < 0))
                File.Delete(Path.Combine(folder, old + ".log"));
    }

    public static Dictionary<string, int> LocalManifest()
    {
        var result = new Dictionary<string, int>();
        foreach (var day in Days(LocalRoot)) {
            var size = new FileInfo(Path.Combine(LocalRoot, day + ".log")).Length;
            if (size is >= 0 and <= MaximumDayBytes) result[Compact(day)] = (int)size;
        }
        return result;
    }

    public static Dictionary<string, int> LocalManifest(Guid deviceId)
    {
        lock (SnapshotLock) {
            if (Snapshots.TryGetValue(deviceId, out var snapshot) &&
                DateTimeOffset.Now - snapshot.Captured < TimeSpan.FromMinutes(10))
                return snapshot.Manifest;
            var manifest = LocalManifest();
            Snapshots[deviceId] = (DateTimeOffset.Now, manifest);
            return manifest;
        }
    }

    public static void RefreshSnapshot(Guid deviceId)
    {
        lock (SnapshotLock) Snapshots.Remove(deviceId);
    }

    public static void RefreshAllSnapshots()
    {
        lock (SnapshotLock) Snapshots.Clear();
    }

    public static Dictionary<string, object>? RequestPhoneChunk(byte[]? phoneReport,
        Guid deviceId)
    {
        if (phoneReport is null) return null;
        using var parsed = JsonDocument.Parse(phoneReport);
        if (!parsed.RootElement.TryGetProperty("archiveManifest", out var manifest) ||
            manifest.ValueKind != JsonValueKind.Object) return null;
        var cutoff = DateOnly.FromDateTime(DateTime.Today)
            .AddDays(1 - RetentionDays).ToString("yyyy-MM-dd",
                CultureInfo.InvariantCulture);
        foreach (var entry in manifest.EnumerateObject()
            .OrderByDescending(item => item.Name, StringComparer.Ordinal)) {
            var day = Expand(entry.Name);
            if (day is null || entry.Value.ValueKind != JsonValueKind.Number ||
                !entry.Value.TryGetInt32(out var size) ||
                size is < 0 or > MaximumDayBytes ||
                string.CompareOrdinal(day, cutoff) < 0) continue;
            var file = Path.Combine(RemoteRoot(deviceId), day + ".log");
            var current = File.Exists(file) ? new FileInfo(file).Length : 0;
            if (current < size) return new Dictionary<string, object> {
                ["day"] = entry.Name, ["offset"] = current
            };
        }
        return null;
    }

    public static Dictionary<string, object>? ComputerChunk(byte[]? phoneReport,
        int maximum = 4_096)
    {
        if (phoneReport is null) return null;
        using var parsed = JsonDocument.Parse(phoneReport);
        if (!parsed.RootElement.TryGetProperty("archiveRequest", out var request) ||
            request.ValueKind != JsonValueKind.Object ||
            !request.TryGetProperty("day", out var compactElement) ||
            compactElement.ValueKind != JsonValueKind.String ||
            compactElement.GetString() is not { } compact ||
            Expand(compact) is not { } day ||
            !request.TryGetProperty("offset", out var offsetElement) ||
            offsetElement.ValueKind != JsonValueKind.Number ||
            !offsetElement.TryGetInt64(out var offset) ||
            offset is < 0 or > MaximumDayBytes) return null;
        var file = Path.Combine(LocalRoot, day + ".log");
        if (!File.Exists(file)) return null;
        using var stream = File.OpenRead(file);
        if (offset >= stream.Length) return null;
        stream.Seek(offset, SeekOrigin.Begin);
        var data = new byte[Math.Min(maximum, (int)(stream.Length - offset))];
        var count = stream.Read(data);
        if (count == 0) return null;
        return new Dictionary<string, object> {
            ["day"] = compact, ["offset"] = offset,
            ["data"] = Convert.ToBase64String(data.AsSpan(0, count))
        };
    }

    public static void ReceivePhoneChunk(byte[] report, Guid deviceId, int retentionDays)
    {
        using var parsed = JsonDocument.Parse(report);
        if (parsed.RootElement.TryGetProperty("archiveRefresh", out var refresh) &&
            refresh.ValueKind == JsonValueKind.True) RefreshSnapshot(deviceId);
        if (!parsed.RootElement.TryGetProperty("archiveChunk", out var chunk) ||
            chunk.ValueKind != JsonValueKind.Object ||
            !chunk.TryGetProperty("day", out var compactElement) ||
            compactElement.ValueKind != JsonValueKind.String ||
            compactElement.GetString() is not { } compact ||
            Expand(compact) is not { } day ||
            !chunk.TryGetProperty("offset", out var offsetElement) ||
            offsetElement.ValueKind != JsonValueKind.Number ||
            !offsetElement.TryGetInt64(out var offset) ||
            offset is < 0 or > MaximumDayBytes ||
            !chunk.TryGetProperty("data", out var encodedElement) ||
            encodedElement.ValueKind != JsonValueKind.String ||
            encodedElement.GetString() is not { } encoded) return;
        byte[] data;
        try { data = Convert.FromBase64String(encoded); }
        catch (FormatException) { return; }
        if (data.Length is < 1 or > 8_192 || offset + data.Length > MaximumDayBytes) return;
        var directory = RemoteRoot(deviceId);
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, day + ".log");
        using (var stream = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite,
            FileShare.Read)) {
            if (stream.Length != offset) return;
            stream.Seek(0, SeekOrigin.End);
            stream.Write(data);
        }
        var cutoff = DateOnly.FromDateTime(DateTime.Today).AddDays(1 - retentionDays)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        foreach (var old in Days(directory).Where(item =>
            string.CompareOrdinal(item, cutoff) < 0))
            File.Delete(Path.Combine(directory, old + ".log"));
    }
}
