using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace MochiLog_Windows.Services;

public sealed record AvailableUpdate(Version Version, Uri InstallerUrl, string Sha256,
    long Size, Uri ReleaseUrl, string Notes);

public static class UpdateService
{
    private const string ReleasesApi =
        "https://api.github.com/repos/MochiLog/MochiLog-Windows/releases?per_page=20";
    private const string InstallerName = "MochiLog-Windows-Alpha-Setup.exe";
    private static readonly HttpClient Client = CreateClient();

    public static Version InstalledVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1, 0);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MochiLog-Windows-Updater/0.1");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task<AvailableUpdate?> CheckAsync(CancellationToken cancellation = default)
    {
        using var response = await Client.GetAsync(ReleasesApi, cancellation);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellation));
        AvailableUpdate? newest = null;
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean()) continue;
            var tag = release.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
            if (!Version.TryParse(tag, out var version) || version <= InstalledVersion) continue;
            var page = release.GetProperty("html_url").GetString();
            if (!Uri.TryCreate(page, UriKind.Absolute, out var releaseUrl) ||
                releaseUrl.Scheme != Uri.UriSchemeHttps || releaseUrl.Host != "github.com") continue;
            foreach (var asset in release.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != InstallerName ||
                    asset.GetProperty("state").GetString() != "uploaded") continue;
                var url = asset.GetProperty("browser_download_url").GetString();
                var digest = asset.TryGetProperty("digest", out var digestValue)
                    ? digestValue.GetString() : null;
                if (!Uri.TryCreate(url, UriKind.Absolute, out var installerUrl) ||
                    installerUrl.Scheme != Uri.UriSchemeHttps || installerUrl.Host != "github.com") continue;
                if (digest is null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                    digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit)) continue;
                var size = asset.GetProperty("size").GetInt64();
                if (size is <= 0 or > 500_000_000) continue;
                var info = new AvailableUpdate(version, installerUrl, digest[7..], size,
                    releaseUrl, release.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "");
                if (newest is null || info.Version > newest.Version) newest = info;
            }
        }
        return newest;
    }

    public static async Task<string> DownloadAsync(AvailableUpdate update, IProgress<long>? progress = null,
        CancellationToken cancellation = default)
    {
        var directory = Path.Combine(StateStore.Root, "Updates", update.Version.ToString());
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, InstallerName);
        if (File.Exists(target) && Verify(target, update)) return target;
        var temporary = target + ".part";
        try
        {
            using var response = await Client.GetAsync(update.InstallerUrl,
                HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                await using var input = await response.Content.ReadAsStreamAsync(cancellation);
                await using var output = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                    FileShare.None, 81920, useAsync: true);
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellation)) != 0)
                {
                    total += count;
                    if (total > update.Size) throw new InvalidDataException("Update size exceeds release metadata.");
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellation);
                    progress?.Report(total);
                }
                await output.FlushAsync(cancellation);
                if (total != update.Size ||
                    !Convert.ToHexString(hash.GetHashAndReset()).Equals(update.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Update SHA-256 does not match GitHub release metadata.");
            }
            File.Move(temporary, target, true);
            return target;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static bool Verify(string path, AvailableUpdate update)
    {
        if (new FileInfo(path).Length != update.Size) return false;
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(update.Sha256,
            StringComparison.OrdinalIgnoreCase);
    }

    public static void LaunchInstaller(string path)
    {
        var start = new ProcessStartInfo(path) { UseShellExecute = true };
        start.ArgumentList.Add("/SILENT");
        start.ArgumentList.Add("/NORESTART");
        start.ArgumentList.Add("/CLOSEAPPLICATIONS");
        start.ArgumentList.Add("/RESTARTAPPLICATIONS");
        Process.Start(start) ?? throw new IOException("The update installer did not start.");
    }
}
