using Microsoft.Win32;

namespace MochiLog_Windows.Services;

public static class StartupRegistration
{
    private const string Path = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "MochiLog Windows";

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(Path);
            return key?.GetValue(Name) is string value && value.Contains(
                "MochiLog.Windows.exe", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Path, writable: true)
            ?? throw new IOException("The Windows startup setting is unavailable.");
        if (enabled) key.SetValue(Name,
            $"\"{Environment.ProcessPath}\"{(TrayPreferences.Enabled ? " --background" : "")}",
            RegistryValueKind.String);
        else key.DeleteValue(Name, throwOnMissingValue: false);
    }
}
