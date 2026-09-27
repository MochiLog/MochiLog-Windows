using MochiLog_Windows.Services;

var software = AppleDeviceSoftwareDetector.Detect();
Console.WriteLine($"AppleDevices={software.AppleDevices}, ClassicITunes={software.ClassicITunes}, " +
    $"AppleDevicesCheckFailed={software.AppleDevicesCheckFailed}");
if (software.AppleDevicesCheckFailed)
    Environment.ExitCode = 1;
