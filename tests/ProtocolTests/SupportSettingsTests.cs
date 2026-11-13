using System.Text;
using System.Text.Json;
using MochiLog_Windows.Services;

static class SupportSettingsTests
{
    public static void Run()
    {
        var bytes = SupportSettingsSnapshot.Capture(2, 30, 54550);
        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        if (
            bytes.Length > 8192
            || root.GetProperty("type").GetString() != "mochilog-settings-snapshot"
            || root.GetProperty("schema").GetInt32() != 1
            || root.GetProperty("limits").GetProperty("pairedDeviceCount").GetInt32() != 2
            || root.GetProperty("permissions")
                .GetProperty("firewallAppRule")
                .GetProperty("status")
                .GetString() != "unknown"
        )
            throw new Exception("Settings snapshot bounds/schema/unknown permissions failed");
        var report = JsonSerializer.SerializeToUtf8Bytes(new { settingsSnapshot = root });
        var extracted =
            SupportSettingsSnapshot.Extract(report)
            ?? throw new Exception("Remote settings missing");
        using var remote = JsonDocument.Parse(extracted);
        if (
            !JsonElement.DeepEquals(root, remote.RootElement)
            || SupportSettingsSnapshot.Extract(Encoding.UTF8.GetBytes("{}")) != null
            || SupportSettingsSnapshot.Extract(Encoding.UTF8.GetBytes("{\"settingsSnapshot\":0}"))
                != null
        )
            throw new Exception("Remote settings snapshot compatibility failed");
        var text = Encoding.UTF8.GetString(bytes);
        if (
            text.Contains("Secret")
            || text.Contains("HostPrivateKey")
            || text.Contains(StateStore.Root.Replace("\\", "\\\\"))
        )
            throw new Exception("Settings snapshot leaked private state/path");
        Console.WriteLine(
            "PASS: bounded settings snapshot, unknown firewall status, secret exclusion and remote extraction"
        );
    }
}
