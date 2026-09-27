using Microsoft.Win32;

namespace MochiLog_Windows.Services;

public static class TrayPreferences
{
    private const string Path = @"Software\MochiLog Windows";
    private const string Name = "ShowTaskbarTrayIcon";

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(Path);
            return key?.GetValue(Name) is int value && value == 1;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(Path, writable: true)
                ?? throw new IOException("The taskbar tray setting is unavailable.");
            key.SetValue(Name, value ? 1 : 0, RegistryValueKind.DWord);
        }
    }
}
