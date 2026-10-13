using Microsoft.Win32;

namespace MochiLog_Windows.Services;

public static class UiMemoryPreferences
{
    private const string Path = @"Software\MochiLog Windows";
    private const string Name = "ReleaseUiWhenHidden";
    public static bool Enabled {
        get { using var key = Registry.CurrentUser.OpenSubKey(Path);
            return key?.GetValue(Name) is not int value || value != 0; }
        set { using var key = Registry.CurrentUser.CreateSubKey(Path, writable: true)
                ?? throw new IOException("The memory setting is unavailable.");
            key.SetValue(Name, value ? 1 : 0, RegistryValueKind.DWord); }
    }
}
