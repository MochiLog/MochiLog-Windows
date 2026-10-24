namespace MochiLog_Windows.Services;

// Compare only the bounded overlap of an authenticated retry. Never overwrite
// saved diagnostics; a matching partial retry appends only its missing suffix.
public static class DiagnosticArchiveChunk
{
    private static readonly object Gate = new();

    // Positive: bytes appended. Zero: identical retry. Negative: conflict/gap.
    public static int Append(string path, long offset, byte[] data)
    {
        if (data.Length is < 1 or > 8_192 || offset < 0 || offset > 64_000_000 - data.Length)
            return -1;
        lock (Gate)
        {
            if (!File.Exists(path) && offset != 0)
                return -1;
            using var stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read
            );
            var size = stream.Length;
            if (size > 64_000_000 || offset > size)
                return -1;
            var overlap = (int)Math.Min(data.Length, size - offset);
            if (overlap > 0)
            {
                stream.Position = offset;
                var previous = new byte[overlap];
                stream.ReadExactly(previous);
                if (!previous.AsSpan().SequenceEqual(data.AsSpan(0, overlap)))
                    return -1;
            }
            if (overlap == data.Length)
                return 0;
            stream.Position = size;
            stream.Write(data.AsSpan(overlap));
            return data.Length - overlap;
        }
    }
}
