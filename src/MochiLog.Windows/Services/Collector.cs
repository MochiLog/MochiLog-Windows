using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MochiLog_Windows.Services;

public sealed record ConnectedDevice(string Udid, string Name, string Model);
public sealed record CollectionResult(int Saved, int Skipped, int Failed, string? LastError);

public static partial class Collector
{
    private static string? Tool => new[]
    {
        Path.Combine(AppContext.BaseDirectory, "Collector", "pymobiledevice3.exe"),
        Path.Combine(AppContext.BaseDirectory, "pymobiledevice3.exe"),
        // Only for development on the configured Windows build host.
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp", "mochilog-win-collector", "Scripts", "pymobiledevice3.exe")
    }.FirstOrDefault(File.Exists);

    public static async Task<string> RunAsync(IEnumerable<string> arguments, TimeSpan timeout,
        CancellationToken cancellation = default)
    {
        var executable = Tool ?? throw new FileNotFoundException(
            "The bundled device collector is missing. Reinstall MochiLog Windows.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["NO_COLOR"] = "1";
        using var process = Process.Start(start) ?? throw new IOException("Collector did not start.");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(timeout);
        var output = process.StandardOutput.ReadToEndAsync(limit.Token);
        var errors = process.StandardError.ReadToEndAsync(limit.Token);
        try { await process.WaitForExitAsync(limit.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Device collection timed out."); }
        var stdout = await output;
        var stderr = await errors;
        if (process.ExitCode != 0)
        {
            var detail = stderr.Split('\n').LastOrDefault(line =>
                line.Contains("Error", StringComparison.OrdinalIgnoreCase))?.Trim();
            throw new IOException($"Collector exited {process.ExitCode}: {detail ?? stderr.Trim()}");
        }
        return stdout;
    }

    public static async Task<IReadOnlyList<ConnectedDevice>> BrowseAsync(
        IEnumerable<PairedPhone>? known = null, CancellationToken cancellation = default)
    {
        var discovered = new Dictionary<string, ConnectedDevice>();
        Exception? nativeFailure = null;
        try
        {
            var output = await RunAsync(["remote", "browse", "--native", "--timeout", "4"],
                TimeSpan.FromSeconds(25), cancellation);
            foreach (var device in JsonSerializer.Deserialize<JsonElement>(output).EnumerateArray())
            {
                var model = device.GetProperty("model").GetString() ?? "";
                if (!SupportedModel(model)) continue;
                var id = device.GetProperty("udid").GetString() ?? "";
                if (id.Length > 0) discovered[id] = new ConnectedDevice(id,
                    device.TryGetProperty("name", out var name) ? name.GetString() ?? model : model,
                    model);
            }
        }
        catch (Exception error) { nativeFailure = error; }
        try
        {
            var list = await RunAsync(["usbmux", "list", "--network", "--simple"],
                TimeSpan.FromSeconds(20), cancellation);
            foreach (var id in JsonSerializer.Deserialize<string[]>(list) ?? [])
            {
                if (discovered.ContainsKey(id)) continue;
                try
                {
                    var info = await RunAsync(["lockdown", "info", "--mobdev2", "--udid", id],
                        TimeSpan.FromSeconds(20), cancellation);
                    var json = JsonSerializer.Deserialize<JsonElement>(info);
                    var model = json.GetProperty("ProductType").GetString() ?? "";
                    if (SupportedModel(model)) discovered[id] = new ConnectedDevice(id,
                        json.TryGetProperty("DeviceName", out var name) ? name.GetString() ?? model : model,
                        model);
                }
                catch { /* One stale Bonjour entry does not hide other devices. */ }
            }
        }
        catch { /* Apple Devices may omit Wi-Fi devices from usbmux; try known pair records. */ }
        // Apple Devices from the Microsoft Store does not place Wi-Fi devices in
        // usbmux. Explicit mobdev2 uses its USB-created pair record instead.
        foreach (var phone in known ?? [])
        {
            if (discovered.ContainsKey(phone.Udid)) continue;
            try
            {
                var info = await RunAsync(["lockdown", "info", "--mobdev2", "--udid", phone.Udid],
                    TimeSpan.FromSeconds(20), cancellation);
                var json = JsonSerializer.Deserialize<JsonElement>(info);
                var model = json.GetProperty("ProductType").GetString() ?? "";
                if (SupportedModel(model)) discovered[phone.Udid] = new ConnectedDevice(phone.Udid,
                    json.TryGetProperty("DeviceName", out var name) ? name.GetString() ?? model : model,
                    model);
            }
            catch { }
        }
        if (discovered.Count == 0 && nativeFailure is not null) throw nativeFailure;
        return discovered.Values.ToArray();
    }

    public static async Task<ConnectedDevice> PrepareUsbPairingAsync(CancellationToken cancellation = default)
    {
        var output = await RunAsync(["usbmux", "list", "--usb", "--simple"],
            TimeSpan.FromSeconds(20), cancellation);
        var ids = JsonSerializer.Deserialize<string[]>(output) ?? [];
        if (ids.Length != 1) throw new InvalidOperationException(ids.Length == 0
            ? "Connect and unlock one iPhone or iPad with a data-capable USB cable."
            : "Disconnect other Apple devices and try again.");
        var id = ids[0];
        try { await RunAsync(["lockdown", "info", "--udid", id], TimeSpan.FromSeconds(20), cancellation); }
        catch { await RunAsync(["lockdown", "pair", "--udid", id], TimeSpan.FromSeconds(90), cancellation); }
        var info = JsonSerializer.Deserialize<JsonElement>(await RunAsync(
            ["lockdown", "info", "--udid", id], TimeSpan.FromSeconds(20), cancellation));
        var model = info.GetProperty("ProductType").GetString() ?? "";
        var version = info.GetProperty("ProductVersion").GetString() ?? "0";
        if (!SupportedModel(model) || !int.TryParse(version.Split('.')[0], out var major) || major < 27)
            throw new InvalidOperationException("This beta requires iOS or iPadOS 27 or later.");
        await RunAsync(["lockdown", "wifi-connections", "on", "--udid", id],
            TimeSpan.FromSeconds(20), cancellation);
        await RunAsync(["lockdown", "remotepairing", "--pair", "--udid", id],
            TimeSpan.FromSeconds(60), cancellation);
        return new ConnectedDevice(id,
            info.TryGetProperty("DeviceName", out var name) ? name.GetString() ?? model : model, model);
    }

    public static async Task VerifyWirelessAsync(string udid, CancellationToken cancellation = default)
    {
        _ = await RootListingAsync(udid, cancellation);
    }

    private static async Task<(string Root, string[] Connection)> RootListingAsync(string udid,
        CancellationToken cancellation)
    {
        var network = new[] { "--mobdev2", "--udid", udid };
        var native = new[] { "--native", "--udid", udid };
        try
        {
            var listing = await RunAsync(["crash", "ls", ..network,
                "--remote-file", "/", "--depth", "1"], TimeSpan.FromSeconds(45), cancellation);
            if (!string.IsNullOrWhiteSpace(listing)) return (listing, network);
        }
        catch { }
        var root = await RunAsync(["crash", "ls", ..native,
            "--remote-file", "/", "--depth", "1"], TimeSpan.FromSeconds(45), cancellation);
        if (string.IsNullOrWhiteSpace(root)) throw new IOException("No wireless diagnostics listing was returned.");
        return (root, native);
    }

    public static async Task<CollectionResult> CollectAsync(PairedPhone phone,
        CompanionState state, Action<int, int>? progress = null,
        CancellationToken cancellation = default)
    {
        var (root, connection) = await RootListingAsync(phone.Udid, cancellation);
        var sources = root.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).Where(line => ProxiedPath().IsMatch(line)).ToArray();
        var files = new List<(string Path, string? Source)>();
        async Task AddDirectory(string path, string? source)
        {
            try
            {
                var listing = await RunAsync(["crash", "ls", ..connection,
                    "--remote-file", path, "--depth", "1"], TimeSpan.FromSeconds(90), cancellation);
                foreach (var line in listing.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var remote = line.Trim();
                    var filename = System.IO.Path.GetFileName(remote);
                    if (remote.StartsWith(path + "/Analytics-", StringComparison.Ordinal) &&
                        ValidLogName().IsMatch(filename) &&
                        !filename.Contains("session", StringComparison.OrdinalIgnoreCase) &&
                        !filename.StartsWith("Analytics-Census-", StringComparison.Ordinal))
                        files.Add((remote, source));
                }
            }
            catch when (source is not null) { /* One unready Watch must not block iPhone logs. */ }
        }
        await AddDirectory("/Retired", null);
        foreach (var source in sources) await AddDirectory(source + "/Retired", source[1..]);
        var saved = 0; var skipped = 0; var failed = 0; string? lastError = null;
        var queue = TransferServer.QueuePath(phone);
        Directory.CreateDirectory(queue);
        for (var index = 0; index < files.Count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            var item = files[index];
            var name = System.IO.Path.GetFileName(item.Path);
            var tokenPrefix = item.Source is null ? "Host::" : $"Watch::{item.Source}::";
            var token = tokenPrefix + name;
            progress?.Invoke(index, files.Count);
            if (state.Delivered.Contains(phone.PhysicalDeviceId.ToString("D").ToUpperInvariant() + "|" + token) ||
                Directory.EnumerateFiles(queue, name, SearchOption.AllDirectories).Any()) continue;
            var staging = System.IO.Path.Combine(queue, ".staging-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(staging);
                await RunAsync(["crash", "pull", staging, "--remote-file", item.Path, ..connection],
                    TimeSpan.FromMinutes(3), cancellation);
                var downloaded = System.IO.Path.Combine(staging, name);
                var kind = BatteryLogKind(downloaded);
                if (kind is null) { skipped++; state.Delivered.Add(
                    phone.PhysicalDeviceId.ToString("D").ToUpperInvariant() + "|" + token); StateStore.Save(state); }
                else
                {
                    var folder = System.IO.Path.Combine(queue, kind,
                        item.Source is null ? "" : item.Source);
                    Directory.CreateDirectory(folder);
                    File.Move(downloaded, System.IO.Path.Combine(folder, name));
                    saved++;
                }
            }
            catch (Exception error) { failed++; lastError = error.Message; }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }
        progress?.Invoke(files.Count, files.Count);
        return new CollectionResult(saved, skipped, failed, lastError);
    }

    private static string? BatteryLogKind(string file)
    {
        if (!File.Exists(file) || new FileInfo(file).Length > 64 * 1024 * 1024) return null;
        var bytes = File.ReadAllBytes(file);
        if (bytes.Count(b => b == 10) < 100) return null;
        var text = Encoding.UTF8.GetString(bytes);
        if (!text.Contains("last_value_CycleCount", StringComparison.Ordinal) ||
            !text.Contains("last_value_NominalChargeCapacity", StringComparison.Ordinal) ||
            !text.Contains("last_value_AppleRawMaxCapacity", StringComparison.Ordinal)) return null;
        var firstLine = text.Split('\n')[0];
        try
        {
            using var header = JsonDocument.Parse(firstLine);
            var os = header.RootElement.GetProperty("os_version").GetString()?.ToLowerInvariant() ?? "";
            if (os.Contains("watch")) return "Watch";
            if (os.Contains("iphone") || os.Contains("ipad") || os.Contains("ios")) return "Host";
        }
        catch { }
        return null;
    }

    private static bool SupportedModel(string model) =>
        model.StartsWith("iPhone", StringComparison.Ordinal) || model.StartsWith("iPad", StringComparison.Ordinal);
    [GeneratedRegex("^/ProxiedDevice-[a-fA-F0-9]+$")]
    private static partial Regex ProxiedPath();
    [GeneratedRegex(@"^Analytics-[0-9]{4}-[0-9]{2}-[0-9]{2}-[0-9]{6}.*\.ips\.ca\.synced$")]
    private static partial Regex ValidLogName();
}
