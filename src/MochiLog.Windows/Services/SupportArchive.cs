using System.IO.Compression;

namespace MochiLog_Windows.Services;

public static class SupportArchive
{
    public static string Create(
        string[] files,
        string directory,
        long maximumBytes = 16 * 1024 * 1024
    )
    {
        if (files.Length is 0 or > 100)
            throw new InvalidDataException("Support archive file limit exceeded");
        var path = Path.Combine(directory, "mochilog-support.zip");
        if (File.Exists(path))
            throw new IOException("Support archive already exists");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var created = false;
        try
        {
            using (
                var output = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None
                )
            )
            {
                created = true;
                using var zip = new ZipArchive(output, ZipArchiveMode.Create);
                long total = 0;
                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    if (
                        name.Length is 0 or > 180
                        || name.StartsWith('.')
                        || !name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_')
                        || !names.Add(name)
                    )
                        throw new InvalidDataException("Invalid support archive entry");
                    var info = new FileInfo(file);
                    if (
                        (info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory))
                        != 0
                    )
                        throw new InvalidDataException("Support archive requires regular files");
                    var size = info.Length;
                    if (size > maximumBytes - total)
                        throw new InvalidDataException("Support archive size limit exceeded");
                    using var input = new FileStream(
                        file,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read
                    );
                    using var target = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                    var buffer = new byte[64 * 1024];
                    long copied = 0;
                    while (copied < size)
                    {
                        var count = input.Read(
                            buffer,
                            0,
                            (int)Math.Min(buffer.Length, size - copied)
                        );
                        if (count == 0)
                            throw new EndOfStreamException("Support snapshot changed");
                        target.Write(buffer, 0, count);
                        copied += count;
                    }
                    total += copied;
                }
            }
            if (new FileInfo(path).Length > maximumBytes + 256 * 1024)
                throw new InvalidDataException("Compressed archive size limit exceeded");
            return path;
        }
        catch
        {
            if (created && File.Exists(path))
                File.Delete(path);
            throw;
        }
    }
}
