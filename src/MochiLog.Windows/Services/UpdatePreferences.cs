using Microsoft.Win32;
namespace MochiLog_Windows.Services;
public static class UpdatePreferences
{
    private const string Path = @"Software\MochiLog Windows";
    public static bool Enabled {
        get { using var k = Registry.CurrentUser.OpenSubKey(Path); return k?.GetValue("AutomaticUpdateChecks") is int v && v == 1; }
        set { using var k = Registry.CurrentUser.CreateSubKey(Path, true); k.SetValue("AutomaticUpdateChecks", value ? 1 : 0, RegistryValueKind.DWord); k.SetValue("UpdateConsentAnswered", 1, RegistryValueKind.DWord); }
    }
    public static bool Answered { get { using var k = Registry.CurrentUser.OpenSubKey(Path); return k?.GetValue("UpdateConsentAnswered") is int v && v == 1; } }
}
