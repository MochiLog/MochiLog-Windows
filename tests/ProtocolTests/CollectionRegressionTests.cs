using System.Text;
using System.Text.Json;
using MochiLog_Windows.Services;

static class CollectionRegressionTests
{
    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new Exception(reason);
    }

    public static async Task Run()
    {
        var oldResult = JsonSerializer.Deserialize<CollectionResult>(
            """{"Saved":1,"Skipped":0,"Failed":0,"LastError":null,"Deferred":0,"RetryHeld":0}""")!;
        Check(oldResult.ListingFailures == 0 && oldResult.AcquisitionComplete,
            "A collection result from an older version no longer loads");
        Check(Collector.ReadPermissionDenied("FILE_OPEN failed with status: 10: None") &&
            Collector.ReadPermissionDenied("PERM_DENIED") &&
            !Collector.ReadPermissionDenied("FILE_OPEN failed with status: 100") &&
            !Collector.ReadPermissionDenied("TimeoutError 10 seconds"), "Permission gate is too broad");
        var day = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd");
        var first = $"Analytics-{day}-090007.000.ips.ca.synced";
        var next = $"Analytics-{day}-090006.000.ips.ca.synced";
        var third = $"Analytics-{day}-090005.000.ips.ca.synced";
        const string watch = "ProxiedDevice-ab12";
        static byte[] Body(bool accessory) => Encoding.UTF8.GetBytes(
            "{\"os_version\":\"" + (accessory ? "watchOS" : "iPhone OS") + " 27.2\"}\n" +
            string.Concat(Enumerable.Repeat("last_value_CycleCount last_value_NominalChargeCapacity last_value_AppleRawMaxCapacity\n", 120)));
        static PairedPhone Phone() => new() { Udid = "collection-fixture", Name = "Collection fixture",
            Model = "iPhone18,3", PhysicalDeviceId = Guid.NewGuid(), ManualAddress = "127.0.0.1" };
        static string Key(PairedPhone phone, string name) => phone.PhysicalDeviceId.ToString("D").ToUpperInvariant() + "|Host::" + name;
        static string Flag(string[] args, string name) => args[Array.IndexOf(args, name) + 1];
        var hostPaths = new[] { first, next, third }.Select(name => "/Retired/" + name).ToArray();
        var watchPath = $"/{watch}/Retired/{first}";
        var phone = Phone(); var state = new CompanionState();
        var pulled = new List<string>(); var batches = new List<int>();
        var deny = true; var includeWatch = true; var includeCurrentDuplicate = false;
        Task<string> Direct(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken token)
        {
            var args = arguments.ToArray();
            if (args[1] == "scan") {
                var rows = hostPaths.Select(path => new { path, source = (string?)null }).ToList();
                if (includeCurrentDuplicate) rows.Insert(0, new { path = "/" + first, source = (string?)null });
                if (includeWatch) rows.Add(new { path = watchPath, source = (string?)watch });
                return Task.FromResult(JsonSerializer.Serialize(new { files = rows }));
            }
            Check(args[0] == "direct-rsd" && args[1] == "pull-batch", "Unexpected direct fixture command");
            var items = JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllBytes(Flag(args, "--manifest")))!;
            batches.Add(items.Length);
            var results = new List<object>();
            for (var local = 0; local < items.Length; local++) {
                var path = items[local].GetProperty("path").GetString()!;
                pulled.Add(path);
                if (deny && path == hostPaths[0]) {
                    results.Add(new { index = local, ok = false, error = "FILE_OPEN failed with status: 10" });
                    continue;
                }
                var folder = Path.Combine(Flag(args, "--output"), local.ToString());
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, Path.GetFileName(path)), Body(path.Contains(watch)));
                results.Add(new { index = local, ok = true });
            }
            return Task.FromResult(JsonSerializer.Serialize(new { results }));
        }
        var result = await Collector.CollectDirectAsync(phone, state, null, CancellationToken.None, Direct);
        Check(result.Saved == 1 && result.Failed == 1 && result.RetryHeld == 2 && !result.AcquisitionComplete &&
            pulled.SequenceEqual(new[] { hostPaths[0], watchPath }) && state.Delivered.Count == 0 &&
            state.RecheckAfter.Count == 1, "Denied host source retried its remaining files or blocked Watch");
        Check(state.RecheckAfter[Key(phone, first)] <= DateTimeOffset.UtcNow,
            "Denied read cannot be tried on the next collection");
        var probeRotation = Collector.ReadProbeOrder(new[] { (hostPaths[0], (string?)null),
            ("/Retired/Analytics-2026-10-01-090000.ips.ca.synced", (string?)null) },
            item => item.Item1 == hostPaths[0] ? DateTimeOffset.UtcNow : null);
        Check(probeRotation[0].Path.Contains("2026-10-01"), "An unreadable current file starved an untried past day");
        // An individual file can remain inaccessible after the source recovers.
        // An untried file must be the next probe, even when the first retry is due.
        pulled.Clear(); batches.Clear();
        result = await Collector.CollectDirectAsync(phone, state, null, CancellationToken.None, Direct);
        Check(result.Saved == 2 && pulled.First() == hostPaths[1] && result.Failed == 1,
            "A permanently unreadable first file starved the remaining direct files");
        var mappedPhone = Phone(); var mappedState = new CompanionState();
        deny = false; includeWatch = false; includeCurrentDuplicate = true; pulled.Clear(); batches.Clear();
        result = await Collector.CollectDirectAsync(mappedPhone, mappedState, null, CancellationToken.None, Direct);
        Check(result.Saved == 3 && result.AcquisitionComplete && batches.SequenceEqual(new[] { 1, 2 }) &&
            !pulled.Contains("/" + first) &&
            hostPaths.All(path => File.Exists(Path.Combine(TransferServer.QueuePath(mappedPhone), "Host", Path.GetFileName(path)))),
            "Split direct batches lost local/global file indices");

        var legacyPhone = Phone(); var legacyState = new CompanionState();
        pulled.Clear();
        var contentOnly = false;
        Task<string> Legacy(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken token)
        {
            var args = arguments.ToArray(); var path = Flag(args, "--remote-file");
            if (args[1] == "ls") return Task.FromResult(path switch {
                "/" => "/Retired\n/" + watch,
                "/Retired" => string.Join('\n', hostPaths),
                _ when path == $"/{watch}/Retired" => watchPath,
                _ when path == $"/{watch}" => $"/{watch}/Retired",
                _ => ""
            });
            Check(args[1] == "pull", "Unexpected legacy fixture command");
            pulled.Add(path);
            if (!contentOnly && path == hostPaths[0]) throw new IOException("FILE_OPEN failed with status: 10");
            File.WriteAllBytes(Path.Combine(args[2], Path.GetFileName(path)), contentOnly
                ? Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("unrelated diagnostic value\n", 1000))) : Body(path.Contains(watch)));
            return Task.FromResult("");
        }
        result = await Collector.CollectLegacyAsync(legacyPhone, legacyState, null, CancellationToken.None, Legacy);
        Check(result.Saved == 1 && result.Failed == 1 && result.RetryHeld == 2 &&
            pulled.Count == 2 && pulled.Contains(watchPath) && legacyState.Delivered.Count == 0,
            "Legacy permission gate blocked Watch or permanently excluded a pending file");
        pulled.Clear();
        result = await Collector.CollectLegacyAsync(legacyPhone, legacyState, null, CancellationToken.None, Legacy);
        Check(result.Saved == 2 && pulled.First() == hostPaths[1], "Legacy source probe did not rotate");
        contentOnly = true; pulled.Clear();
        var contentPhone = Phone(); var contentState = new CompanionState();
        var firstContent = await Collector.CollectLegacyAsync(contentPhone, contentState, null, CancellationToken.None, Legacy);
        var heldContent = await Collector.CollectLegacyAsync(contentPhone, contentState, null, CancellationToken.None, Legacy);
        Check(firstContent.Deferred == 4 && heldContent.RetryHeld == 4 && pulled.Count == 4,
            "Failure recovery disabled the independent downloaded-content retry policy");
        var partialPhone = Phone(); var partialState = new CompanionState();
        var listingRecovered = false;
        Task<string> Partial(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken token)
        {
            var args = arguments.ToArray(); var path = Flag(args, "--remote-file");
            if (args[1] == "ls") {
                if (path == "/") return Task.FromResult($"/Retired\n/{first}\n/{watch}");
                if (path == "/Retired") return Task.FromResult("");
                if (path == $"/{watch}") return Task.FromResult($"/{watch}/Retired\n/{watch}/{first}");
                if (listingRecovered) return Task.FromResult("");
                throw new IOException("Accessory archive temporarily unavailable");
            }
            File.WriteAllBytes(Path.Combine(args[2], Path.GetFileName(path)), Body(path.Contains(watch)));
            return Task.FromResult("");
        }
        result = await Collector.CollectLegacyAsync(partialPhone, partialState, null, CancellationToken.None, Partial);
        Check(result.Saved == 2 && result.Failed == 0 && result.ListingFailures == 1 &&
            !result.AcquisitionComplete && result.LastError is not null,
            "An unreadable archive hid the current Watch report or falsely completed acquisition");
        result = await Collector.CollectLegacyAsync(partialPhone, partialState, null, CancellationToken.None, Partial);
        Check(result.Saved == 0 && result.ListingFailures == 1 && !result.AcquisitionComplete,
            "An empty pending queue hid an incomplete listing");
        listingRecovered = true;
        result = await Collector.CollectLegacyAsync(partialPhone, partialState, null, CancellationToken.None, Partial);
        Check(result.AcquisitionComplete && result.ListingFailures == 0,
            "A transient listing error persisted after recovery");
        Task<string> PartialDirect(IEnumerable<string> arguments, TimeSpan timeout, CancellationToken token) =>
            Task.FromResult("""{"files":[],"listingFailures":[{"directory":"/ProxiedDevice-ab12","source":"ProxiedDevice-ab12","error":"TimeoutError"}]}""");
        result = await Collector.CollectDirectAsync(Phone(), new(), null, CancellationToken.None, PartialDirect);
        Check(result.ListingFailures == 1 && !result.AcquisitionComplete,
            "Direct helper listing failures were discarded");
        Console.WriteLine("PASS: direct/legacy source permission gates, Watch isolation, pending recovery and split batch mapping");
    }
}
