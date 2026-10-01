using System.Text.Json;

namespace MochiLog_Windows.Services;

public sealed record StoredBatteryLog(string Id, string Path, Guid DeviceId,
    string DeviceName, string Kind, string? Source, long Size,
    DateTimeOffset StoredAt, bool Pending)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string LogDay => Name.Length >= 20 ? Name.Substring(10, 10) : "";
}

public sealed class BatteryStorageSettings
{
    public bool KeepAfterDelivery { get; set; }
    public int LimitMB { get; set; } = 500;
    public int RetentionMonths { get; set; } = 1;
}

public static class BatteryLogStorage
{
    private static readonly object Gate = new();
    private static string ArchiveRoot => System.IO.Path.Combine(StateStore.Root, "BatteryLogArchive");
    private static string SettingsFile => System.IO.Path.Combine(StateStore.Root, "battery-storage.json");
    private static BatteryStorageSettings _settings = LoadSettings();

    public static BatteryStorageSettings Settings
    {
        get { lock (Gate) return new BatteryStorageSettings {
            KeepAfterDelivery = _settings.KeepAfterDelivery,
            LimitMB = _settings.LimitMB,
            RetentionMonths = _settings.RetentionMonths
        }; }
    }

    public static void UpdateSettings(bool keep, int limitMB, int retentionMonths)
    {
        lock (Gate)
        {
            _settings = new BatteryStorageSettings {
                KeepAfterDelivery = keep,
                LimitMB = Math.Clamp(limitMB, 100, 100_000),
                RetentionMonths = Math.Clamp(retentionMonths, 1, 60)
            };
            Directory.CreateDirectory(StateStore.Root);
            var temporary = SettingsFile + ".new";
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(_settings));
            File.Move(temporary, SettingsFile, true);
            PruneLocked();
        }
    }

    private static BatteryStorageSettings LoadSettings()
    {
        try { return JsonSerializer.Deserialize<BatteryStorageSettings>(File.ReadAllBytes(SettingsFile))
            ?? new BatteryStorageSettings(); }
        catch (IOException) { return new BatteryStorageSettings(); }
        catch (JsonException) { return new BatteryStorageSettings(); }
    }

    public static void ArchiveAcknowledged(string file, PairedPhone phone)
    {
        lock (Gate)
        {
            var resendMarker = file + ".force-resend";
            if (!_settings.KeepAfterDelivery) {
                File.Delete(file);
                File.Delete(resendMarker);
                return;
            }
            var queue = System.IO.Path.GetFullPath(TransferServer.QueuePath(phone));
            var source = System.IO.Path.GetFullPath(file);
            if (!source.StartsWith(queue + System.IO.Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid battery log queue path");
            var relative = System.IO.Path.GetRelativePath(queue, source);
            var destination = System.IO.Path.Combine(ArchiveRoot,
                phone.PhysicalDeviceId.ToString("D").ToUpperInvariant(), relative);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
            if (File.Exists(destination)) File.Delete(source);
            else {
                File.Move(source, destination);
                File.SetLastWriteTimeUtc(destination, DateTime.UtcNow);
            }
            File.Delete(resendMarker);
            PruneLocked();
        }
    }

    public static IReadOnlyList<StoredBatteryLog> List(IReadOnlyList<PairedPhone> phones)
    {
        lock (Gate)
        {
            var rows = new List<StoredBatteryLog>();
            var names = phones.GroupBy(phone => phone.PhysicalDeviceId)
                .ToDictionary(group => group.Key, group => group.Last().Name);
            foreach (var phone in phones)
            {
                Scan(TransferServer.QueuePath(phone), phone.PhysicalDeviceId,
                    phone.Name, true, rows);
            }
            if (Directory.Exists(ArchiveRoot))
            {
                foreach (var folder in Directory.EnumerateDirectories(ArchiveRoot))
                {
                    if (!Guid.TryParse(System.IO.Path.GetFileName(folder), out var deviceId)) continue;
                    Scan(folder, deviceId, names.GetValueOrDefault(deviceId) ??
                        deviceId.ToString("D").ToUpperInvariant(), false, rows);
                }
            }
            return rows.OrderByDescending(row => row.StoredAt).ToArray();
        }
    }

    private static void Scan(string root, Guid deviceId, string deviceName, bool pending,
        List<StoredBatteryLog> rows)
    {
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.EnumerateFiles(root, "Analytics-*.ips.ca.synced",
            SearchOption.AllDirectories))
        {
            var parts = System.IO.Path.GetRelativePath(root, file).Split(System.IO.Path.DirectorySeparatorChar);
            if (parts.Length is not (2 or 3) || parts[0] is not ("Host" or "Watch")) continue;
            var info = new FileInfo(file);
            rows.Add(new StoredBatteryLog((pending ? "pending/" : "archive/") +
                deviceId.ToString("D").ToUpperInvariant() + "/" +
                string.Join('/', parts), file, deviceId, deviceName,
                parts[0], parts.Length == 3 ? parts[1] : null, info.Length,
                info.LastWriteTimeUtc, pending));
        }
    }

    public static void Delete(IReadOnlyList<StoredBatteryLog> rows)
    {
        lock (Gate) foreach (var row in rows.Where(row => !row.Pending)) File.Delete(row.Path);
    }

    public static void Export(IReadOnlyList<StoredBatteryLog> rows, string directory)
    {
        foreach (var row in rows)
        {
            var folder = System.IO.Path.Combine(directory,
                row.DeviceId.ToString("D").ToUpperInvariant(), row.Kind, row.Source ?? "");
            Directory.CreateDirectory(folder);
            var destination = System.IO.Path.Combine(folder, row.Name);
            File.Copy(row.Path, destination, overwrite: false);
        }
    }

    public static int Requeue(IReadOnlyList<StoredBatteryLog> rows, IReadOnlyList<PairedPhone> phones)
    {
        lock (Gate)
        {
            var copied = 0;
            foreach (var row in rows.Where(row => !row.Pending))
            {
                var phone = phones.FirstOrDefault(phone => phone.PhysicalDeviceId == row.DeviceId);
                if (phone is null) continue;
                var archived = System.IO.Path.Combine(ArchiveRoot,
                    row.DeviceId.ToString("D").ToUpperInvariant());
                var relative = System.IO.Path.GetRelativePath(archived, row.Path);
                var destination = System.IO.Path.Combine(TransferServer.QueuePath(phone), relative);
                if (File.Exists(destination)) continue;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
                try {
                    File.Copy(row.Path, destination);
                    File.WriteAllBytes(destination + ".force-resend", []);
                }
                catch {
                    File.Delete(destination);
                    throw;
                }
                copied++;
            }
            return copied;
        }
    }

    public static void Prune() { lock (Gate) PruneLocked(); }

    private static void PruneLocked()
    {
        if (!Directory.Exists(ArchiveRoot)) return;
        var cutoff = DateTime.UtcNow.AddMonths(-Math.Clamp(_settings.RetentionMonths, 1, 60));
        var retained = new List<FileInfo>();
        foreach (var path in Directory.EnumerateFiles(ArchiveRoot,
            "Analytics-*.ips.ca.synced", SearchOption.AllDirectories))
        {
            var file = new FileInfo(path);
            if (file.LastWriteTimeUtc < cutoff) file.Delete();
            else retained.Add(file);
        }
        var total = retained.Sum(file => file.Length);
        var limit = (long)Math.Clamp(_settings.LimitMB, 100, 100_000) * 1_000_000;
        foreach (var file in retained.OrderBy(file => file.LastWriteTimeUtc).Where(_ => total > limit))
        {
            file.Delete();
            total -= file.Length;
        }
    }
}
