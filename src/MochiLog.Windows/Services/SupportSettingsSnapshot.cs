using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Text.Json;

namespace MochiLog_Windows.Services;

/// <summary>Explicit allowlist: never dump registry/preferences, credentials or pairing state.</summary>
public static class SupportSettingsSnapshot
{
    public static byte[] Capture(int pairedDevices, int debugRetentionDays, int configuredPort)
    {
        var unavailable = new List<string>();
        bool? Read(string name, Func<bool> read)
        {
            try
            {
                return read();
            }
            catch (Exception error)
                when (error
                        is IOException
                            or UnauthorizedAccessException
                            or System.Security.SecurityException
                            or NetworkInformationException
                )
            {
                unavailable.Add(name);
                return null;
            }
        }
        var storage = BatteryLogStorage.Settings;
        var at = DateTimeOffset.Now;
        var data = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                type = "mochilog-settings-snapshot",
                schema = 1,
                capturedAt = at,
                timeZone = TimeZoneInfo.Local.Id,
                platform = "Windows",
                appVersion = typeof(SupportSettingsSnapshot).Assembly.GetName().Version?.ToString(),
                osVersion = Environment.OSVersion.VersionString,
                processID = Environment.ProcessId,
                toggles = new Dictionary<string, bool?>
                {
                    ["trayIcon"] = Read("trayIcon", () => TrayPreferences.Enabled),
                    ["automaticUpdateChecks"] = Read(
                        "automaticUpdateChecks",
                        () => UpdatePreferences.Enabled
                    ),
                    ["releaseUIWhenHidden"] = Read(
                        "releaseUIWhenHidden",
                        () => UiMemoryPreferences.Enabled
                    ),
                    ["startupRegistrationPresent"] = Read(
                        "startupRegistrationPresent",
                        () => StartupRegistration.Enabled
                    ),
                    ["retainDeliveredLogs"] = storage.KeepAfterDelivery,
                    ["networkInterfaceAvailable"] = Read(
                        "networkInterfaceAvailable",
                        NetworkInterface.GetIsNetworkAvailable
                    ),
                },
                limits = new
                {
                    pairedDeviceCount = pairedDevices,
                    debugRetentionDays,
                    batteryArchiveLimitMB = storage.LimitMB,
                    batteryArchiveRetentionMonths = storage.RetentionMonths,
                    configuredListeningPort = configuredPort,
                },
                permissions = new
                {
                    localNetwork = new
                    {
                        status = "not-applicable",
                        source = "Windows has no Apple Local Network privilege",
                    },
                    firewallAppRule = new
                    {
                        status = "unknown",
                        source = "not-queried; listening does not prove inbound access",
                    },
                    startupApproval = new
                    {
                        status = "unknown",
                        source = "Run registry presence does not prove OS approval",
                    },
                    elevated = new
                    {
                        status = Read(
                            "elevated",
                            () =>
                            {
                                using var identity = WindowsIdentity.GetCurrent();
                                return new WindowsPrincipal(identity).IsInRole(
                                    WindowsBuiltInRole.Administrator
                                );
                            }
                        ),
                        source = "current-process Windows token",
                        checkedAt = at,
                    },
                },
                unavailableSettings = unavailable,
                note = "Current settings, not historical incident settings. A network interface/listening port does not prove reachability or firewall permission. Null means unavailable. No raw preferences, registry data, paths, account or credential dump.",
            }
        );
        if (data.Length > 8192)
            throw new InvalidDataException("Settings snapshot exceeded its bound");
        return data;
    }

    public static byte[] Formatted(byte[] data)
    {
        using var document = JsonDocument.Parse(data);
        return JsonSerializer.SerializeToUtf8Bytes(
            document.RootElement,
            new JsonSerializerOptions { WriteIndented = true }
        );
    }

    public static byte[]? Extract(byte[] report)
    {
        if (report.Length > 262_144)
            return null;
        try
        {
            using var document = JsonDocument.Parse(report);
            if (
                !document.RootElement.TryGetProperty("settingsSnapshot", out var value)
                || !value.TryGetProperty("type", out var type)
                || type.GetString() != "mochilog-settings-snapshot"
                || !value.TryGetProperty("schema", out var schema)
                || schema.GetInt32() != 1
            )
                return null;
            var data = JsonSerializer.SerializeToUtf8Bytes(value);
            return data.Length <= 8192 ? data : null;
        }
        catch (Exception error)
            when (error is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
