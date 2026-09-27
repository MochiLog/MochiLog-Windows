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
        #if MOCHILOG_PROTOCOL_TEST
        Environment.GetEnvironmentVariable("MOCHILOG_TEST_COLLECTOR") ?? "",
        #endif
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
        // The collector sometimes reports a failed device connection as a
        // coloured ERROR line while still returning exit code 0. Do not treat
        // that diagnostic as a directory listing or a completed download.
        var cleanOutput = Regex.Replace(stdout + "\n" + stderr, "\\x1B\\[[0-9;]*m", "");
        var toolError = cleanOutput.Split('\n').LastOrDefault(line =>
            Regex.IsMatch(line, @"\bERROR\b", RegexOptions.IgnoreCase));
        if (process.ExitCode != 0 || toolError is not null)
        {
            var detail = toolError?.Trim() ?? stderr.Trim();
            throw new IOException($"Collector exited {process.ExitCode}: {detail}");
        }
        return stdout;
    }

    public static async Task<IReadOnlyList<ConnectedDevice>> BrowseAsync(
        IEnumerable<PairedPhone>? known = null, CancellationToken cancellation = default)
    {
        var discovered = new Dictionary<string, ConnectedDevice>();
        Exception? nativeFailure = null;
        if (!OperatingSystem.IsWindows()) try
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
        try
        {
            var list = await RunAsync(["usbmux", "list", "--usb", "--simple"],
                TimeSpan.FromSeconds(20), cancellation);
            foreach (var id in JsonSerializer.Deserialize<string[]>(list) ?? [])
            {
                if (discovered.ContainsKey(id)) continue;
                try
                {
                    var info = await RunAsync(["lockdown", "info", "--udid", id],
                        TimeSpan.FromSeconds(20), cancellation);
                    var json = JsonSerializer.Deserialize<JsonElement>(info);
                    var model = json.GetProperty("ProductType").GetString() ?? "";
                    if (SupportedModel(model)) discovered[id] = new ConnectedDevice(id,
                        json.TryGetProperty("DeviceName", out var name) ? name.GetString() ?? model : model,
                        model);
                }
                catch { /* Continue past an attached but locked or untrusted device. */ }
            }
        }
        catch { /* USB is optional after initial pairing. */ }
        // The Microsoft Store Apple Devices service does not enumerate paired
        // Wi-Fi devices through usbmux. Bonjour still advertises them, and an
        // explicit mobdev2 connection can use the pair record made over USB.
        if (OperatingSystem.IsWindows()) try
        {
            var list = await RunAsync(["bonjour", "mobdev2", "--timeout", "4"],
                TimeSpan.FromSeconds(15), cancellation);
            foreach (var device in JsonSerializer.Deserialize<JsonElement>(list).EnumerateArray())
            {
                var id = device.GetProperty("UniqueDeviceID").GetString() ?? "";
                if (id.Length == 0 || discovered.ContainsKey(id)) continue;
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
                catch { /* An advertised but unpaired device is not usable yet. */ }
            }
        }
        catch { /* Retain devices found through other transports. */ }
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

    public static async Task<ConnectedDevice> PrepareUsbPairingAsync(
        string? selectedUdid = null, CancellationToken cancellation = default)
    {
        var output = await RunAsync(["usbmux", "list", "--usb", "--simple"],
            TimeSpan.FromSeconds(20), cancellation);
        var ids = JsonSerializer.Deserialize<string[]>(output) ?? [];
        if (ids.Length == 0)
            throw new InvalidOperationException(UiText.Get("win_usb_missing"));
        if (selectedUdid is not null && !ids.Contains(selectedUdid, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(UiText.Get("win_usb_not_connected"));
        if (selectedUdid is null && ids.Length > 1)
            throw new InvalidOperationException(UiText.Get("win_usb_select_multiple"));
        var id = selectedUdid ?? ids[0];
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
        var listing = await RunAsync(["crash", "ls", "--mobdev2", "--udid", udid,
            "--remote-file", "/", "--depth", "1"], TimeSpan.FromSeconds(45), cancellation);
        if (!listing.Contains("/Retired", StringComparison.Ordinal))
            throw new IOException("The device's wireless diagnostics service did not return a file listing.");
    }

    private static async Task<(string Root, string[] Connection)> RootListingAsync(string udid,
        CancellationToken cancellation)
    {
        var network = new[] { "--mobdev2", "--udid", udid };
        var native = new[] { "--native", "--udid", udid };
        Exception? wirelessError = null;
        try
        {
            var listing = await RunAsync(["crash", "ls", ..network,
                "--remote-file", "/", "--depth", "1"], TimeSpan.FromSeconds(45), cancellation);
            if (!string.IsNullOrWhiteSpace(listing)) return (listing, network);
        }
        catch (Exception error) { wirelessError = error; }
        if (!OperatingSystem.IsWindows())
        {
            var root = await RunAsync(["crash", "ls", ..native,
                "--remote-file", "/", "--depth", "1"], TimeSpan.FromSeconds(45), cancellation);
            if (root.Contains("/Retired", StringComparison.Ordinal)) return (root, native);
        }
        // An attached device can still be collected when wireless discovery is
        // temporarily unavailable. This is an explicit fallback, never reported
        // as a successful wireless connection by VerifyWirelessAsync.
        var attached = JsonSerializer.Deserialize<string[]>(await RunAsync(
            ["usbmux", "list", "--usb", "--simple"], TimeSpan.FromSeconds(20), cancellation)) ?? [];
        if (attached.Contains(udid))
        {
            var usb = new[] { "--udid", udid };
            var root = await RunAsync(["crash", "ls", ..usb, "--remote-file", "/", "--depth", "1"],
                TimeSpan.FromSeconds(45), cancellation);
            if (root.Contains("/Retired", StringComparison.Ordinal)) return (root, usb);
        }
        throw new IOException("The device's diagnostics service is unavailable over Wi-Fi or USB. " +
            wirelessError?.Message, wirelessError);
    }

    public static async Task<CollectionResult> CollectAsync(PairedPhone phone,
        CompanionState state, Action<int, int>? progress = null,
        CancellationToken cancellation = default)
    {
        if (OperatingSystem.IsWindows() &&
            !string.IsNullOrWhiteSpace(phone.ManualAddress ?? phone.LastKnownAddress))
        {
            try { return await CollectDirectAsync(phone, state, progress, cancellation); }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // USB remains useful when a saved network address is stale.
                try { return await CollectLegacyAsync(phone, state, progress, cancellation); }
                catch (Exception fallback) { throw new IOException(
                    $"Wireless collection failed: {error.Message}; USB fallback: {fallback.Message}", fallback); }
            }
        }
        return await CollectLegacyAsync(phone, state, progress, cancellation);
    }

    private sealed record DirectFile(string Path, string? Source);

    private static async Task<CollectionResult> CollectDirectAsync(PairedPhone phone,
        CompanionState state, Action<int, int>? progress, CancellationToken cancellation)
    {
        var address = phone.ManualAddress ?? phone.LastKnownAddress!;
        var connection = new[] { "direct-rsd", "scan", "--udid", phone.Udid,
            "--host", address, "--port", "49152" };
        var listing = await RunAsync(connection, TimeSpan.FromMinutes(2), cancellation);
        using var scan = JsonDocument.Parse(listing);
        var files = scan.RootElement.GetProperty("files").EnumerateArray().Select(item => new DirectFile(
            item.GetProperty("path").GetString()!, item.GetProperty("source").ValueKind == JsonValueKind.Null
                ? null : item.GetProperty("source").GetString())).ToArray();
        var queue = TransferServer.QueuePath(phone);
        Directory.CreateDirectory(queue);
        var pending = new List<DirectFile>();
        foreach (var item in files)
        {
            var name = Path.GetFileName(item.Path);
            var token = (item.Source is null ? "Host::" : $"Watch::{item.Source}::") + name;
            var deliveredKey = phone.PhysicalDeviceId.ToString("D").ToUpperInvariant() + "|" + token;
            lock (state) { if (state.Delivered.Contains(deliveredKey)) continue; }
            if (new[] { "Host", "Watch" }.Any(kind => File.Exists(
                Path.Combine(queue, kind, item.Source ?? "", name)))) continue;
            pending.Add(item);
        }
        if (pending.Count == 0) { progress?.Invoke(0, 0); return new CollectionResult(0, 0, 0, null); }
        var staging = Path.Combine(queue, ".staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var manifest = Path.Combine(staging, "manifest.json");
            await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(pending.Select(item =>
                new { path = item.Path, source = item.Source })), cancellation);
            var output = await RunAsync(["direct-rsd", "pull-batch", "--udid", phone.Udid,
                "--host", address, "--port", "49152",
                "--manifest", manifest, "--output", staging],
                TimeSpan.FromMinutes(Math.Max(3, pending.Count * 3)), cancellation);
            using var results = JsonDocument.Parse(output);
            var successful = results.RootElement.GetProperty("results").EnumerateArray()
                .ToDictionary(result => result.GetProperty("index").GetInt32(),
                    result => result.GetProperty("ok").GetBoolean());
            var saved = 0; var skipped = 0; var failed = 0;
            for (var index = 0; index < pending.Count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                var item = pending[index];
                progress?.Invoke(index, pending.Count);
                if (!successful.TryGetValue(index, out var ok) || !ok) { failed++; continue; }
                var name = Path.GetFileName(item.Path);
                var downloaded = Path.Combine(staging, index.ToString(), name);
                var kind = BatteryLogKind(downloaded);
                if (kind is null)
                {
                    skipped++;
                    var token = (item.Source is null ? "Host::" : $"Watch::{item.Source}::") + name;
                    lock (state) {
                        state.Delivered.Add(phone.PhysicalDeviceId.ToString("D").ToUpperInvariant() + "|" + token);
                        StateStore.Save(state);
                    }
                }
                else
                {
                    var folder = Path.Combine(queue, kind, item.Source ?? "");
                    Directory.CreateDirectory(folder);
                    File.Move(downloaded, Path.Combine(folder, name), true);
                    saved++;
                }
            }
            progress?.Invoke(pending.Count, pending.Count);
            return new CollectionResult(saved, skipped, failed,
                failed == 0 ? null : "Some diagnostic files could not be downloaded.");
        }
        finally { Directory.Delete(staging, true); }
    }

    private static async Task<CollectionResult> CollectLegacyAsync(PairedPhone phone,
        CompanionState state, Action<int, int>? progress,
        CancellationToken cancellation)
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
            var deliveredKey = phone.PhysicalDeviceId.ToString("D").ToUpperInvariant() + "|" + token;
            lock (state) { if (state.Delivered.Contains(deliveredKey)) continue; }
            var sourceFolder = item.Source ?? "";
            if (new[] { "Host", "Watch" }.Any(kind => File.Exists(
                System.IO.Path.Combine(queue, kind, sourceFolder, name)))) continue;
            var staging = System.IO.Path.Combine(queue, ".staging-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(staging);
                await RunAsync(["crash", "pull", staging, "--remote-file", item.Path, ..connection],
                    TimeSpan.FromMinutes(3), cancellation);
                var downloaded = System.IO.Path.Combine(staging, name);
                var kind = BatteryLogKind(downloaded);
                if (kind is null) {
                    skipped++;
                    lock (state) { state.Delivered.Add(deliveredKey); StateStore.Save(state); }
                }
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
        var lines = 0;
        foreach (var value in bytes) if (value == 10 && ++lines >= 100) break;
        if (lines < 100) return null;
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
