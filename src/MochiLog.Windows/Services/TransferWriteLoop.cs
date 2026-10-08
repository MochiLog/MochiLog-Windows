namespace MochiLog_Windows.Services;

/// Refresh the idle deadline after each completed chunk, including on slow VPN links.
internal static class TransferWriteLoop
{
    internal static async Task WriteAsync(Stream stream, ReadOnlyMemory<byte> packet,
        TimeSpan idleTimeout, CancellationToken cancellation, Action<int>? progress = null)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var sent = 0;
        while (sent < packet.Length)
        {
            idle.CancelAfter(idleTimeout);
            var length = Math.Min(64 * 1024, packet.Length - sent);
            await stream.WriteAsync(packet.Slice(sent, length), idle.Token);
            sent += length;
            progress?.Invoke(sent);
        }
    }
}
