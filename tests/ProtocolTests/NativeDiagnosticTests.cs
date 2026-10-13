using System.Text;
using System.Text.Json;
using MochiLog_Windows.Services;

static class NativeDiagnosticTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    public static void Run()
    {
        var root = Path.Combine(StateStore.Root, "native-diagnostic-test");
        Directory.CreateDirectory(root);
        var routes = new DiagnosticRoutes.Report(null, "192.168.1.20",
            ["192.168.1.21"], DateTimeOffset.Now, null, null,
            [new("192.168.1.21", "authenticated hint, unverified")]);
        var device = new NativeDiagnostics.Device(Guid.NewGuid(), "Test iPad", "iPad16,6", true,
            null, routes, new(1, 2, 3, 4, 5, false, "synthetic failure"));
        var at = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.FromHours(9));
        var events = Enumerable.Range(0, 700).Select(i =>
            $"event {i}: " + new string('x', 800) + " PSDrive {\"Nested\":{\"Secret\":\"TEXT_ONLY\"}}").ToArray();
        var snapshot = NativeDiagnostics.Capture("0.1.28", "ready", "synthetic",
            [device], events, at);
        var data = NativeDiagnostics.Encode(snapshot);
        using (var json = JsonDocument.Parse(data)) {
            var value = json.RootElement;
            Check(value.GetProperty("schema").GetInt32() == 1, "Schema version missing");
            Check(value.GetProperty("recentEvents").GetArrayLength() == 50, "Event cap failed");
            Check(value.GetProperty("recentEvents")[0].GetString()!.Contains("event 650"), "Latest events not retained");
            Check(!value.TryGetProperty("PSDrive", out _), "Log text expanded into object metadata");
            Check(value.GetProperty("resources").GetProperty("processId").GetInt32() > 0 &&
                value.GetProperty("resources").GetProperty("managedBytes").GetInt64() > 0, "Native process memory missing");
            if (OperatingSystem.IsWindows())
                Check(value.GetProperty("resources").GetProperty("privateBytes").GetInt64() > 0, "Windows private memory missing");
            Check(value.GetProperty("devices")[0].GetProperty("lastCollection").GetProperty("retryHeld").GetInt32() == 5,
                "Retry state missing");
        }
        Check(data.Length <= NativeDiagnostics.MaxBytes && snapshot.Truncated, "Output cap not enforced");
        Check(!Encoding.UTF8.GetString(data).Contains("udid", StringComparison.OrdinalIgnoreCase), "OS identity leaked");
        Check(snapshot.Devices[0].Routes.Manual is null && snapshot.Devices[0].Routes.Verified is null,
            "Missing route metadata was changed to a value");
        var many = NativeDiagnostics.Capture("0.1.28", "", "",
            Enumerable.Repeat(device, 1000).ToArray(), [], at);
        Check(many.Devices.Length == NativeDiagnostics.MaxDevices && many.Truncated, "Device cap failed");
        try {
            NativeDiagnostics.Encode(snapshot with { RecentEvents = [new string('x', 1_000_000)] });
            throw new Exception("Encoder accepted unbounded input");
        } catch (InvalidDataException) { }
        var store = new DiagnosticSnapshotStore(root);
        store.Save(snapshot with { GeneratedAt = at.AddDays(-30) }, 30);
        store.Save(snapshot, 30);
        Check(!File.Exists(Path.Combine(store.Root, "2026-09-14.json")), "Expired snapshot retained");
        Check(File.Exists(Path.Combine(store.Root, "2026-10-14.json")), "Daily snapshot missing");
        Check(File.ReadAllBytes(store.CurrentPath).SequenceEqual(data), "Current snapshot differs from daily data");
        store.Save(snapshot, 30);
        Check(Directory.GetFiles(store.Root, "*.new").Length == 0, "Atomic writer left temporary files");
        var before = GC.GetTotalMemory(true);
        for (var i = 0; i < 1000; i++) _ = NativeDiagnostics.Encode(snapshot);
        var growth = GC.GetTotalMemory(true) - before;
        Check(growth < 8 * 1024 * 1024, "Repeated export retained excessive managed memory");
        store.Clear();
        Check(!File.Exists(store.CurrentPath), "Clear left current snapshot");
        Console.WriteLine($"PASS: native diagnostics, bounded DTOs, retention, 1000 exports; inputBytes={events.Sum(Encoding.UTF8.GetByteCount)}, outputBytes={data.Length}, managedGrowthBytes={growth}");
    }
}
