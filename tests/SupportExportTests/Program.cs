using System.IO.Compression;
using System.Text;
using MochiLog_Windows.Services;

var root = Path.Combine(Path.GetTempPath(), "mochilog-export-test-" + Guid.NewGuid().ToString("N"));
try
{
    var source = Path.Combine(root, "source");
    Directory.CreateDirectory(source);
    const string day = "2026-11-13";
    DiagnosticLogArchive.Append(
        day + "T16:00:00+09:00 | PC pairing: test",
        source,
        "4.1.0",
        "1068"
    );
    DiagnosticLogArchive.Append(
        day + "T16:00:01+09:00 | Local scheduler: test",
        source,
        "4.1.0",
        "1068"
    );
    var sources = new[]
    {
        new SupportLogExport.Source("phone", source),
        new SupportLogExport.Source("computer", Path.Combine(root, "absent")),
    };
    var output = Path.Combine(root, "export");
    var files = SupportLogExport.Prepare(
        sources,
        [day, "2026-11-12", "../bad"],
        [new("diagnostic.json", Encoding.UTF8.GetBytes("{}"))],
        output,
        ["computer: app pairing not completed"]
    );
    Check(
        files.Any(f => Path.GetFileName(f).Contains("pairing-v2-4.1.0-1068")),
        "Pairing split file missing"
    );
    Check(
        files.Any(f => Path.GetFileName(f).Contains("background-v2-4.1.0-1068")),
        "Background split file missing"
    );
    Check(
        files.Any(f => Path.GetFileName(f) == "phone-debug-" + day + ".log"),
        "Compatibility archive missing"
    );
    var manifest = File.ReadAllText(Path.Combine(output, "mochilog-support-manifest.json"));
    Check(
        manifest.Contains("missing") && manifest.Contains("computer: app pairing not completed"),
        "Missing source not reported"
    );
    var zipPath = SupportArchive.Create(files, output);
    using (var zip = ZipFile.OpenRead(zipPath))
    {
        Check(zip.Entries.Count == files.Length, "ZIP lost prepared attachments");
        foreach (var file in files)
        {
            var entry = zip.GetEntry(Path.GetFileName(file));
            Check(entry is not null, "ZIP entry missing");
            using var input = entry!.Open();
            using var bytes = new MemoryStream();
            input.CopyTo(bytes);
            Check(bytes.ToArray().SequenceEqual(File.ReadAllBytes(file)), "ZIP content changed");
        }
        Check(zip.GetEntry("mochilog-support-manifest.json") is not null, "ZIP manifest missing");
    }
    var rejected = Path.Combine(root, "rejected");
    Directory.CreateDirectory(rejected);
    try
    {
        SupportArchive.Create(files, rejected, maximumBytes: 1);
        throw new Exception("Oversized archive accepted");
    }
    catch (InvalidDataException)
    {
        Check(!File.Exists(Path.Combine(rejected, "mochilog-support.zip")), "Partial ZIP retained");
    }
    var bounded = Path.Combine(root, "bounded");
    SupportLogExport.Prepare(sources, [day], [], bounded, maximumBytes: 1);
    Check(
        File.ReadAllText(Path.Combine(bounded, "mochilog-support-manifest.json"))
            .Contains("omitted-export-limit"),
        "Budget omission not reported"
    );
    var noDevice = SupportLogExport.Prepare(
        [],
        [day],
        [new("computer.json", Encoding.UTF8.GetBytes("{}"))],
        Path.Combine(root, "unpaired"),
        ["phone: not paired"]
    );
    Check(noDevice.Length == 2, "Unpaired support report is not exportable");
    Console.WriteLine(
        "PASS: split/compatibility logs, legacy remote stream, missing source, unpaired export and bounded attachment manifest"
    );
}
finally
{
    if (Directory.Exists(root))
        Directory.Delete(root, true);
}
void Check(bool value, string message)
{
    if (!value)
        throw new InvalidOperationException(message);
}
