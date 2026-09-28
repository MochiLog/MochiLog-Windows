using System.Diagnostics;

namespace MochiLog_Windows.Services;

public sealed class AppleUsbBridgeUnavailableException : IOException
{
    public AppleUsbBridgeUnavailableException() : base(UiText.Get("win_usb_service_stale")) { }
}

public static class AppleUsbRecovery
{
    // Only Apple Mobile Device USB composite parents are restarted. A phone,
    // tablet, or unrelated Apple USB accessory is never selected by UDID text.
    private const string DeviceQuery =
        "$d=Get-PnpDevice -PresentOnly -Class USBDevice | Where-Object { " +
        "$_.FriendlyName -eq 'Apple Mobile Device USB Composite Device' -and " +
        "$_.InstanceId -like 'USB\\VID_05AC*' }; " +
        "$d | Select-Object -ExpandProperty InstanceId";

    public static async Task<bool> HasConnectedDeviceAsync(CancellationToken cancellation)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command", DeviceQuery })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("PowerShell did not start.");
        var output = process.StandardOutput.ReadToEndAsync(cancellation);
        await process.WaitForExitAsync(cancellation);
        return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(await output);
    }

    public static async Task RepairAsync(CancellationToken cancellation)
    {
        // Device restart needs elevation on a normal Windows account. The
        // explicit repair button is the only place that can trigger UAC.
        const string script =
            "$d=Get-PnpDevice -PresentOnly -Class USBDevice | Where-Object { " +
            "$_.FriendlyName -eq 'Apple Mobile Device USB Composite Device' -and " +
            "$_.InstanceId -like 'USB\\VID_05AC*' }; " +
            "if (-not $d) { exit 2 }; " +
            "foreach ($item in $d) { & pnputil.exe /restart-device $item.InstanceId | Out-Null; " +
            "if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }";
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = true, Verb = "runas" };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command", script })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("USB repair did not start.");
        await process.WaitForExitAsync(cancellation);
        if (process.ExitCode != 0) throw new IOException(UiText.Get("win_usb_repair_failed"));
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var found = await Collector.RunAsync(["usbmux", "list", "--usb", "--simple"],
                TimeSpan.FromSeconds(20), cancellation);
            if (found.Trim().Length > 2) return;
            await Task.Delay(TimeSpan.FromSeconds(2), cancellation);
        }
        throw new IOException(UiText.Get("win_usb_repair_failed"));
    }
}
