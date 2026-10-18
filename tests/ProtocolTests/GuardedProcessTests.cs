using System.Diagnostics;
using MochiLog_Windows.Services;

static class GuardedProcessTests
{
    private static ProcessStartInfo Start(string kind, string file)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var item in new[] { "--guarded-fixture", kind, file })
            start.ArgumentList.Add(item);
        return start;
    }

    public static async Task Fixture(string kind, string file)
    {
        File.WriteAllText(file, Environment.ProcessId.ToString());
        if (kind == "descendant")
        {
            using var child = Process.Start(Start("idle", file + ".child"))!;
            for (var i = 0; i < 100 && !File.Exists(file + ".child"); i++)
                await Task.Delay(20);
            return;
        }
        Console.WriteLine("fixture ready");
        if (kind == "flood")
        {
            for (var i = 0; i < 512; i++)
                Console.WriteLine(new string('x', 1024));
            return;
        }
        await Task.Delay(TimeSpan.FromMinutes(2));
    }

    private static void Gone(string file)
    {
        if (!File.Exists(file))
            throw new Exception("Fixture never started");
        var pid = int.Parse(File.ReadAllText(file));
        try
        {
            using var process = Process.GetProcessById(pid);
            if (!process.HasExited)
                throw new Exception("Supervised process survived termination");
        }
        catch (ArgumentException) { }
    }

    public static async Task Run()
    {
        var root = Path.Combine(StateStore.Root, "guarded-process-test");
        Directory.CreateDirectory(root);
        var timeout = Path.Combine(root, "timeout");
        try
        {
            await GuardedProcess.RunAsync(Start("idle", timeout), TimeSpan.FromSeconds(2));
            throw new Exception("Execution deadline ignored");
        }
        catch (TimeoutException) { }
        Gone(timeout);
        var flood = Path.Combine(root, "flood");
        try
        {
            await GuardedProcess.RunAsync(
                Start("flood", flood),
                TimeSpan.FromSeconds(10),
                outputLimit: 32768
            );
            throw new Exception("Output bound ignored");
        }
        catch (InvalidDataException) { }
        Gone(flood);
        var cancel = Path.Combine(root, "cancel");
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await GuardedProcess.RunAsync(
                Start("idle", cancel),
                TimeSpan.FromMinutes(1),
                stop.Token
            );
            throw new Exception("Caller cancellation ignored");
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        Gone(cancel);
        if (OperatingSystem.IsWindows())
        {
            var parent = Path.Combine(root, "parent");
            try
            {
                await GuardedProcess.RunAsync(Start("descendant", parent), TimeSpan.FromSeconds(3));
            }
            catch (TimeoutException)
            { /* An inherited stdout pipe also shares the deadline. */
            }
            Gone(parent);
            Gone(parent + ".child");
            Console.WriteLine("PASS: Windows Job Object kills descendants after the parent exits");
        }
        else
            Console.WriteLine("SKIP: Windows descendant Job Object test on non-Windows host");
        Console.WriteLine("PASS: tool deadline, caller cancellation and output cap");
    }
}
