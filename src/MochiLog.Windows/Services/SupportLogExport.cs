using System.Globalization;
using System.Text.Json;

namespace MochiLog_Windows.Services;

/// <summary>Bounded snapshots of split and compatibility logs; no original file is changed.</summary>
public static class SupportLogExport
{
    public sealed record Source(string Name, string Directory);

    public sealed record Diagnostic(string Name, byte[] Data);

    private sealed record Item(string Name, string Status, long Bytes);

    public static string[] Prepare(
        IEnumerable<Source> sources,
        IEnumerable<string> days,
        IEnumerable<Diagnostic> diagnostics,
        string directory,
        string[]? unavailable = null,
        int maximumBytes = 12 * 1024 * 1024,
        int maximumFiles = 96
    )
    {
        Directory.CreateDirectory(directory);
        var files = new List<string>();
        var items = new List<Item>();
        var remaining = maximumBytes;
        static bool ValidName(string name) =>
            name.Length is > 0 and <= 180
            && !name.StartsWith('.')
            && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_');
        bool Reserve(string name, long size)
        {
            if (
                !ValidName(name)
                || items.Any(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            )
                return false;
            if (size <= 0)
            {
                items.Add(new(name, "missing-or-empty", 0));
                return false;
            }
            if (size > remaining || files.Count >= maximumFiles)
            {
                items.Add(new(name, "omitted-export-limit", size));
                return false;
            }
            return true;
        }
        void AddFile(string source, string name)
        {
            var target = Path.Combine(directory, name);
            try
            {
                var info = new FileInfo(source);
                if (!info.Exists)
                {
                    items.Add(new(name, "missing", 0));
                    return;
                }
                if (
                    (info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory))
                    != 0
                )
                {
                    items.Add(new(name, "not-a-regular-file", 0));
                    return;
                }
                var size = info.Length;
                if (!Reserve(name, size))
                    return;
                using var input = new FileStream(
                    source,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete
                );
                using var output = new FileStream(
                    target,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read
                );
                var buffer = new byte[64 * 1024];
                long copied = 0;
                while (copied < size)
                {
                    var count = input.Read(buffer, 0, (int)Math.Min(buffer.Length, size - copied));
                    if (count == 0)
                        throw new IOException("Source changed while taking snapshot");
                    output.Write(buffer, 0, count);
                    copied += count;
                }
                remaining -= (int)copied;
                files.Add(target);
                items.Add(new(name, "included-snapshot", copied));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (File.Exists(target))
                    File.Delete(target);
                items.Add(new(name, "read-failed", 0));
            }
        }
        var requestedDiagnostics = diagnostics.Take(65).ToArray();
        foreach (var item in requestedDiagnostics.Take(64))
        {
            if (!Reserve(item.Name, item.Data.Length))
                continue;
            var target = Path.Combine(directory, item.Name);
            File.WriteAllBytes(target, item.Data);
            remaining -= item.Data.Length;
            files.Add(target);
            items.Add(new(item.Name, "included", item.Data.Length));
        }
        var requestedDays = days.Take(4).ToArray();
        var selectedDays = requestedDays.Take(3).ToArray();
        var requestedSources = sources.Take(33).ToArray();
        foreach (var source in requestedSources.Take(32).Where(s => ValidName(s.Name)))
        {
            foreach (var day in selectedDays)
            {
                if (
                    !DateOnly.TryParseExact(
                        day,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out _
                    )
                )
                    continue;
                var folder = Path.Combine(source.Directory, day);
                var folderName = $"{source.Name}-{day}-feature-files";
                try
                {
                    if (Directory.Exists(folder))
                    {
                        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                            items.Add(new(folderName, "symbolic-directory-skipped", 0));
                        else
                        {
                            var count = 0;
                            foreach (
                                var file in Directory
                                    .EnumerateFiles(folder, "*.log")
                                    .Order(StringComparer.Ordinal)
                            )
                            {
                                var name = Path.GetFileName(file);
                                if (
                                    ValidName(name)
                                    && DiagnosticLogArchive.Categories.Any(c =>
                                        name.StartsWith(c + "-v", StringComparison.Ordinal)
                                    )
                                )
                                {
                                    count++;
                                    AddFile(file, $"{source.Name}-{day}-{name}");
                                }
                            }
                            if (count == 0)
                                items.Add(new(folderName, "no-feature-files", 0));
                        }
                    }
                    else
                        items.Add(new(folderName, "missing-feature-directory", 0));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    items.Add(new(folderName, "directory-read-failed", 0));
                }
                AddFile(
                    Path.Combine(source.Directory, day + ".log"),
                    $"{source.Name}-debug-{day}.log"
                );
            }
        }
        var manifest = Path.Combine(directory, "mochilog-support-manifest.json");
        File.WriteAllBytes(
            manifest,
            JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    type = "mochilog-support-export",
                    schema = 1,
                    generatedAt = DateTimeOffset.Now,
                    days = selectedDays,
                    sourceLimitReached = requestedSources.Length > 32,
                    diagnosticLimitReached = requestedDiagnostics.Length > 64,
                    dayLimitReached = requestedDays.Length > 3,
                    unavailableSources = unavailable ?? [],
                    maximumBytes,
                    maximumFiles,
                    includedBytes = maximumBytes - remaining,
                    items,
                    note = "Feature files are included when stored locally. Remote peers exchange the complete daily compatibility stream; original feature files may not exist on the receiving device. Missing/limited files are explicitly listed.",
                },
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }
            )
        );
        return [.. files, manifest];
    }
}
