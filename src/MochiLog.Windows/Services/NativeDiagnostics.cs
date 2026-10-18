using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace MochiLog_Windows.Services;

/// Explicit DTOs only: never serialize CompanionState, PairedPhone, FileInfo,
/// PowerShell objects, keys, battery values, or arbitrary runtime objects.
public static class NativeDiagnostics
{
    public const int MaxBytes = 256 * 1024;
    public const int MaxDevices = 32;
    public const int MaxEvents = 50;

    public sealed record Resources(
        int ProcessId,
        long PrivateBytes,
        long WorkingSetBytes,
        long PeakWorkingSetBytes,
        long ManagedBytes,
        long TotalAllocatedBytes,
        int Gen0Collections,
        int Gen1Collections,
        int Gen2Collections
    );

    public sealed record Collection(
        int Saved,
        int Skipped,
        int Failed,
        int Deferred,
        int RetryHeld,
        bool AcquisitionComplete,
        string? Error,
        int ListingFailures = 0
    );

    public sealed record Device(
        Guid PhysicalDeviceId,
        string Name,
        string Model,
        bool PairingConfirmed,
        DateTimeOffset? AutomaticPauseUntil,
        DiagnosticRoutes.Report Routes,
        Collection? LastCollection
    );

    public sealed record Snapshot(
        int Schema,
        string Type,
        DateTimeOffset GeneratedAt,
        string AppVersion,
        string OsVersion,
        Resources Resources,
        string Status,
        string CollectionStatus,
        Device[] Devices,
        string[] RecentEvents,
        bool Truncated
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 12,
    };

    private static string Text(string? text, int maximum)
    {
        if (text is null)
            return "";
        var count = Math.Min(text.Length, maximum);
        if (count > 0 && char.IsHighSurrogate(text[count - 1]))
            count--;
        return text[..count];
    }

    private static string? Optional(string? text, int maximum) =>
        text is null ? null : Text(text, maximum);

    public static Resources Measure()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        return new(
            process.Id,
            process.PrivateMemorySize64,
            process.WorkingSet64,
            process.PeakWorkingSet64,
            GC.GetTotalMemory(false),
            GC.GetTotalAllocatedBytes(false),
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2)
        );
    }

    public static Snapshot Capture(
        string version,
        string status,
        string collectionStatus,
        IReadOnlyList<Device> devices,
        IReadOnlyList<string> events,
        DateTimeOffset? at = null
    )
    {
        var bounded = devices
            .Take(MaxDevices)
            .Select(device =>
                device with
                {
                    Name = Text(device.Name, 128),
                    Model = Text(device.Model, 64),
                    Routes = device.Routes with
                    {
                        Manual = Optional(device.Routes.Manual, 64),
                        TransferPeer = Optional(device.Routes.TransferPeer, 64),
                        Hints = device
                            .Routes.Hints.Take(4)
                            .Select(value => Text(value, 64))
                            .ToArray(),
                        Verified = Optional(device.Routes.Verified, 64),
                        Candidates = device
                            .Routes.Candidates.Take(8)
                            .Select(value => new DiagnosticRoutes.Candidate(
                                Text(value.Address, 64),
                                Text(value.Source, 96)
                            ))
                            .ToArray(),
                    },
                    LastCollection = device.LastCollection is { } collection
                        ? collection with
                        {
                            Error = Optional(collection.Error, 512),
                        }
                        : null,
                }
            )
            .ToArray();
        return new(
            1,
            "mochilog-native-diagnostics",
            at ?? DateTimeOffset.Now,
            Text(version, 64),
            Text(Environment.OSVersion.VersionString, 128),
            Measure(),
            Text(status, 512),
            Text(collectionStatus, 512),
            bounded,
            events.TakeLast(MaxEvents).Select(value => Text(value, 512)).ToArray(),
            devices.Count > MaxDevices
                || events.Count > MaxEvents
                || events.TakeLast(MaxEvents).Any(value => value.Length > 512)
        );
    }

    public static byte[] Encode(Snapshot snapshot)
    {
        // Reject unbounded fields before allocating JSON if Capture is bypassed.
        static bool Long(string? text, int count) => text?.Length > count;
        if (
            snapshot.Devices.Length > MaxDevices
            || snapshot.RecentEvents.Length > MaxEvents
            || Long(snapshot.AppVersion, 64)
            || Long(snapshot.OsVersion, 128)
            || Long(snapshot.Status, 512)
            || Long(snapshot.CollectionStatus, 512)
            || snapshot.RecentEvents.Any(value => Long(value, 512))
            || snapshot.Devices.Any(device =>
                Long(device.Name, 128)
                || Long(device.Model, 64)
                || Long(device.LastCollection?.Error, 512)
                || Long(device.Routes.Manual, 64)
                || Long(device.Routes.TransferPeer, 64)
                || Long(device.Routes.Verified, 64)
                || device.Routes.Hints.Length > 4
                || device.Routes.Hints.Any(value => Long(value, 64))
                || device.Routes.Candidates.Length > 8
                || device.Routes.Candidates.Any(value =>
                    Long(value.Address, 64) || Long(value.Source, 96)
                )
            )
        )
            throw new InvalidDataException("Native diagnostic fields exceed their size limits.");
        // Capture bounds every variable-length field. Keep a final output guard
        // and reduce events if escaping exceeds the budget.
        var data = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        while (data.Length > MaxBytes && snapshot.RecentEvents.Length > 0)
        {
            snapshot = snapshot with
            {
                RecentEvents = snapshot.RecentEvents.Skip(1).ToArray(),
                Truncated = true,
            };
            data = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        }
        if (data.Length > MaxBytes)
            throw new InvalidDataException("Native diagnostic snapshot exceeds its size limit.");
        return data;
    }
}

public sealed class DiagnosticSnapshotStore(string appDataRoot)
{
    private readonly object _gate = new();
    public string Root { get; } = Path.Combine(appDataRoot, "NativeDiagnostics");
    public string CurrentPath => Path.Combine(Root, "current.json");

    private static bool Day(string value) =>
        DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _
        );

    public void Save(NativeDiagnostics.Snapshot snapshot, int retentionDays)
    {
        var data = NativeDiagnostics.Encode(snapshot);
        var day = snapshot.GeneratedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        lock (_gate)
        {
            Directory.CreateDirectory(Root);
            foreach (var path in new[] { CurrentPath, Path.Combine(Root, day + ".json") })
            {
                var temporary = path + ".new";
                try
                {
                    File.WriteAllBytes(temporary, data);
                    File.Move(temporary, path, true);
                }
                finally
                {
                    File.Delete(temporary);
                }
            }
            Prune(retentionDays, snapshot.GeneratedAt);
        }
    }

    public void Prune(int retentionDays, DateTimeOffset? at = null)
    {
        lock (_gate)
        {
            if (!Directory.Exists(Root))
                return;
            var cutoff = (at ?? DateTimeOffset.Now)
                .Date.AddDays(1 - Math.Clamp(retentionDays, 7, 365))
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (var file in Directory.EnumerateFiles(Root, "*.json"))
            {
                var day = Path.GetFileNameWithoutExtension(file);
                if (Day(day) && string.CompareOrdinal(day, cutoff) < 0)
                    File.Delete(file);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (!Directory.Exists(Root))
                return;
            foreach (var file in Directory.EnumerateFiles(Root, "*.json"))
                if (file == CurrentPath || Day(Path.GetFileNameWithoutExtension(file)))
                    File.Delete(file);
        }
    }
}
