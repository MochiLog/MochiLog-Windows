using Microsoft.Win32;
using Windows.Management.Deployment;

namespace MochiLog_Windows.Services;

public readonly record struct AppleDeviceSoftware(bool AppleDevices, bool ClassicITunes,
    bool AppleDevicesCheckFailed);

public static class AppleDeviceSoftwareDetector
{
    public static AppleDeviceSoftware Detect()
    {
        var devices = false;
        var failed = false;
        try
        {
            // A Store iTunes package is deliberately not counted: its device
            // service is not the classic installer required by the collector.
            devices = new PackageManager()
                .FindPackagesForUser("", "AppleInc.AppleDevices_nzyj5cx40ttqa")
                .Any();
        }
        catch { failed = true; }
        return new AppleDeviceSoftware(devices, HasClassicITunes(), failed);
    }

    private static bool HasClassicITunes()
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (var name in uninstall.GetSubKeyNames())
                {
                    using var entry = uninstall.OpenSubKey(name);
                    var title = entry?.GetValue("DisplayName") as string;
                    var publisher = entry?.GetValue("Publisher") as string;
                    if (title is not null &&
                        (title.Equals("iTunes", StringComparison.OrdinalIgnoreCase) ||
                         title.StartsWith("iTunes ", StringComparison.OrdinalIgnoreCase)) &&
                        publisher?.Contains("Apple", StringComparison.OrdinalIgnoreCase) == true)
                        return true;
                }
            }
            catch { /* A damaged uninstall entry must not hide another valid installation. */ }
        }
        return new[] { Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86 }
            .Select(folder => Path.Combine(Environment.GetFolderPath(folder), "iTunes", "iTunes.exe"))
            .Any(File.Exists);
    }
}
