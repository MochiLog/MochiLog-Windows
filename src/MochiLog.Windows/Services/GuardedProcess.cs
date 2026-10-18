using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MochiLog_Windows.Services;

/// One lifetime for the process, descendants and output pipes. Disposing a
/// Process alone does not terminate it, and inherited pipes can outlive its exit.
public static class GuardedProcess
{
    public sealed record Result(int ExitCode, string Output, string Error);

    public static async Task<Result> RunAsync(
        ProcessStartInfo start,
        TimeSpan timeout,
        CancellationToken cancellation = default,
        int outputLimit = 8 * 1024 * 1024,
        int errorLimit = 1024 * 1024
    )
    {
        cancellation.ThrowIfCancellationRequested();
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        using var process = Process.Start(start) ?? throw new IOException("Tool did not start.");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(timeout);
        OwnedJob? job = null;
        Task<string>? output = null,
            error = null;
        try
        {
            job = OwnedJob.Attach(process);
            output = ReadAsync(process.StandardOutput, outputLimit, process, limit);
            error = ReadAsync(process.StandardError, errorLimit, process, limit);
            await Task.WhenAll(process.WaitForExitAsync(limit.Token), output, error);
            return new(process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new TimeoutException("Tool execution or output exceeded its deadline.");
        }
        finally
        {
            limit.Cancel();
            job?.Dispose();
            Kill(process);
            // Observe pipe errors after termination; never wait indefinitely.
            try
            {
                await Task.WhenAll(output ?? Task.FromResult(""), error ?? Task.FromResult(""))
                    .WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch { }
        }
    }

    public static async Task<int> RunElevatedAsync(
        ProcessStartInfo start,
        TimeSpan timeout,
        CancellationToken cancellation
    )
    {
        cancellation.ThrowIfCancellationRequested();
        using var process = Process.Start(start) ?? throw new IOException("Tool did not start.");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(timeout);
        OwnedJob? job = null;
        try
        {
            job = OwnedJob.Attach(process);
            await process.WaitForExitAsync(limit.Token);
            return process.ExitCode;
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new TimeoutException("Device repair exceeded its deadline.");
        }
        finally
        {
            job?.Dispose();
            Kill(process);
        }
    }

    private static async Task<string> ReadAsync(
        StreamReader reader,
        int maximum,
        Process process,
        CancellationTokenSource lifetime
    )
    {
        var text = new StringBuilder();
        var buffer = new char[4096];
        try
        {
            while (
                await reader.ReadAsync(buffer.AsMemory(), lifetime.Token) is var count && count > 0
            )
            {
                if (text.Length + count > maximum)
                    throw new InvalidDataException("Tool output exceeded its limit.");
                text.Append(buffer, 0, count);
            }
            return text.ToString();
        }
        catch
        {
            Kill(process);
            lifetime.Cancel();
            throw;
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception) { }
    }

    private sealed class OwnedJob(SafeFileHandle handle) : IDisposable
    {
        public static OwnedJob? Attach(Process process)
        {
            if (!OperatingSystem.IsWindows())
                return null;
            var handle = CreateJobObject(IntPtr.Zero, null);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            var info = new ExtendedLimits
            {
                Basic = new() { Flags = 0x2000 | 0x100 | 0x200 },
                ProcessMemory = (UIntPtr)(1024UL * 1024 * 1024),
                JobMemory = (UIntPtr)(2UL * 1024 * 1024 * 1024),
            };
            if (
                !SetInformationJobObject(
                    handle,
                    9,
                    ref info,
                    (uint)Marshal.SizeOf<ExtendedLimits>()
                ) || !AssignProcessToJobObject(handle, process.Handle)
            )
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, "Could not supervise the tool process.");
            }
            return new(handle);
        }

        public void Dispose() => handle.Dispose();

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimits
        {
            public long ProcessTime,
                JobTime;
            public uint Flags;
            public UIntPtr MinimumWorkingSet,
                MaximumWorkingSet;
            public uint ActiveProcesses;
            public UIntPtr Affinity;
            public uint Priority,
                Scheduling;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Counters
        {
            public ulong ReadOps,
                WriteOps,
                OtherOps,
                ReadBytes,
                WriteBytes,
                OtherBytes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimits
        {
            public BasicLimits Basic;
            public Counters IO;
            public UIntPtr ProcessMemory,
                JobMemory,
                PeakProcessMemory,
                PeakJobMemory;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            SafeFileHandle job,
            int kind,
            ref ExtendedLimits info,
            uint length
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    }
}
