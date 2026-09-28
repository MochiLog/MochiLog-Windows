using System.Text.Json;
using System.Net;
using System.Net.Sockets;

namespace MochiLog_Windows.Services;

public sealed class CompanionRuntime : IDisposable
{
    public static CompanionRuntime Shared { get; } = new();
    public CompanionState State { get; }
    public TransferServer Server { get; }
    public IReadOnlyList<ConnectedDevice> Available { get; private set; } = [];
    public string Status { get; private set; } = UiText.Get("win_searching");
    public string CollectionStatus { get; private set; } = "";
    public event Action? Changed;
    public event Action<PairedPhone>? PairingConfirmed;
    private readonly SemaphoreSlim _collection = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<string> _events = [];
    private readonly Dictionary<Guid, string> _lastAutomaticDecision = [];
    private static string EventsFile => Path.Combine(StateStore.Root, "support-events.json");
    private bool _started;

    private CompanionRuntime()
    {
        State = StateStore.Load();
        try { _events.AddRange(JsonSerializer.Deserialize<string[]>(File.ReadAllText(EventsFile)) ?? []); }
        catch (IOException) { }
        catch (JsonException) { }
        Server = new TransferServer(State);
        Server.SupportReport = phone => JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 1, platform = "Windows", generatedAt = DateTimeOffset.Now,
            deviceName = phone.Name, osVersion = Environment.OSVersion.VersionString,
            recentEvents = DebugLog.Split(Environment.NewLine).TakeLast(30).ToArray()
        });
        Server.StatusChanged += message => Record(message);
        Server.PhoneConfirmed += phone => {
            Record($"{phone.Name}: app pairing confirmed");
            PairingConfirmed?.Invoke(phone);
            _ = CollectAsync(phone, trigger: "app pairing confirmed");
        };
        Server.PhoneAddressChanged += phone => {
            Record($"{phone.Name}: authenticated network address updated");
            _ = CollectAsync(phone, trigger: "authenticated address changed");
        };
        Server.PairingRevoked += () => {
            Record("MochiLog app pairing removed for one device");
            Changed?.Invoke();
        };
    }

    public void Start()
    {
        if (_started) return;
        _started = true;
        try { Server.Start(); }
        catch (Exception error) { Record("Transfer server could not start: " + error.Message); }
        _ = PeriodicCollectionAsync(_lifetime.Token);
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            Status = UiText.Get("win_searching");
            Changed?.Invoke();
            Available = await Task.Run(() => Collector.BrowseAsync(State.Phones, _lifetime.Token));
            Status = Available.Count == 0 ? UiText.Get("win_none_found") :
                UiText.Format("win_devices_available", Available.Count);
        }
        catch (Exception error) { Status = "Device search: " + error.Message; }
        Record(Status);
    }

    public async Task<ConnectedDevice> PrepareUsbAsync(string? selectedUdid = null,
        Action<int, string>? progress = null)
    {
        Status = UiText.Get("win_usb_wait");
        Changed?.Invoke();
        try
        {
            var device = await Task.Run(() => Collector.PrepareUsbPairingAsync(
                selectedUdid, progress, _lifetime.Token));
            Available = Available.Where(item => item.Udid != device.Udid).Append(device).ToArray();
            Status = UiText.Format("win_usb_ready", device.Name);
            Record(Status);
            return device;
        }
        catch (Exception error)
        {
            Record("USB setup failed: " + error.Message);
            throw;
        }
    }

    public async Task VerifyWirelessAsync(ConnectedDevice device)
    {
        Status = UiText.Format("win_checking", device.Name);
        Changed?.Invoke();
        await Task.Run(() => Collector.VerifyWirelessAsync(device.Udid, _lifetime.Token));
        Status = UiText.Format("win_wireless_ready", device.Name);
        Available = Available.Where(item => item.Udid != device.Udid).Append(device).ToArray();
        Record(Status);
    }

    public PairingInvitation BeginPairing(ConnectedDevice device)
    {
        if (!Available.Any(item => item.Udid == device.Udid))
            throw new InvalidOperationException("Set up USB trust or select a discovered device first.");
        if (string.IsNullOrWhiteSpace(device.Model))
            throw new InvalidOperationException(UiText.Get("win_pair_trust_first"));
        var invitation = Server.BeginPairing(device);
        Record($"{device.Name}: pairing QR generated (expires after 3 minutes)");
        return invitation;
    }

    public void SetManualAddress(PairedPhone phone, string? address)
    {
        address = address?.Trim();
        if (!string.IsNullOrEmpty(address) &&
            (!IPAddress.TryParse(address, out var parsed) ||
             parsed.AddressFamily != AddressFamily.InterNetwork ||
             IPAddress.IsLoopback(parsed) || parsed.Equals(IPAddress.Any)))
            throw new ArgumentException("Enter a valid iPhone or iPad IPv4 address.");
        lock (State) {
            phone.ManualAddress = string.IsNullOrEmpty(address) ? null : address;
            StateStore.Save(State);
        }
        Record($"{phone.Name}: manual device address {(phone.ManualAddress is null ? "cleared" : "set")}");
        if (phone.ManualAddress is not null)
            _ = CollectAsync(phone, trigger: "manual device address set");
    }

    public async Task CollectAsync(PairedPhone? selected = null, bool manual = false,
        string trigger = "5-minute timer")
    {
        if (!await _collection.WaitAsync(0))
        {
            Record($"Collection trigger skipped: {trigger}; another collection is in progress");
            return;
        }
        try
        {
            PairedPhone[] phones;
            lock (State) { phones = selected is null ? State.Phones.ToArray() :
                State.Phones.Contains(selected) ? [selected] : []; }
            var now = DateTimeOffset.UtcNow;
            var collectionOpen = now.ToOffset(TimeSpan.FromHours(9)).Hour >= 9;
            foreach (var phone in phones)
            {
                if (!manual)
                {
                    var japanNow = now.ToOffset(TimeSpan.FromHours(9));
                    var nextWindow = new DateTimeOffset(japanNow.Date.AddHours(9)
                        .AddDays(japanNow.Hour >= 9 ? 1 : 0), TimeSpan.FromHours(9));
                    var resume = !collectionOpen ? nextWindow : phone.AutomaticPauseUntil;
                    var reason = !collectionOpen ? "before the daily collection window" :
                        phone.AutomaticPauseUntil > now ?
                        "mobile app confirmed all required daily logs" : null;
                    if (reason is not null)
                    {
                        var key = $"{reason}|{resume:O}";
                        if (!_lastAutomaticDecision.TryGetValue(phone.PhysicalDeviceId,
                            out var previous) || previous != key)
                        {
                            Record($"{phone.Name}: automatic collection stopped; trigger={reason}; " +
                                $"resume={resume?.ToLocalTime():O}");
                            _lastAutomaticDecision[phone.PhysicalDeviceId] = key;
                        }
                        continue;
                    }
                    if (_lastAutomaticDecision.Remove(phone.PhysicalDeviceId))
                        Record($"{phone.Name}: automatic collection resumed; trigger={trigger}");
                }
                Record($"{phone.Name}: collection started; trigger={(manual ? "manual request" : trigger)}");
                CollectionStatus = UiText.Format("win_reading", phone.Name);
                Changed?.Invoke();
                try
                {
                    var result = await Task.Run(() => Collector.CollectAsync(phone, State,
                        (done, total) => { CollectionStatus = UiText.Format("win_progress", phone.Name, done, total);
                            Changed?.Invoke(); }, _lifetime.Token));
                    CollectionStatus = UiText.Format("win_collection_result", phone.Name,
                        result.Saved, result.Skipped, result.Failed);
                    if (result.LastError is not null) CollectionStatus += " " + result.LastError;
                    lock (State) {
                        State.LastCollections[phone.PhysicalDeviceId.ToString("D").ToUpperInvariant()] = result;
                        StateStore.Save(State);
                    }
                    Record(CollectionStatus);
                    Record($"{phone.Name}: collection finished; saved={result.Saved}, " +
                        $"excluded={result.Skipped}, failed={result.Failed}");
                }
                catch (Exception error) { Record($"{phone.Name}: collection failed; " +
                    $"trigger={(manual ? "manual request" : trigger)}; error={error.Message}"); }
            }
        }
        finally { _collection.Release(); }
    }

    public void Unpair(PairedPhone phone)
    {
        Server.Revoke(phone);
        Record($"{phone.Name}: MochiLog pairing removed; OS trust and saved logs retained");
        Changed?.Invoke();
    }

    private async Task PeriodicCollectionAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
                await CollectAsync(trigger: "5-minute timer");
        }
        catch (OperationCanceledException) { }
    }

    public string DebugLog
    {
        get { lock (_events) return CrashDiagnostics.SummaryText + Environment.NewLine +
            string.Join(Environment.NewLine, _events); }
    }

    public string PhoneDiagnosticsText(PairedPhone? phone)
    {
        if (phone is null || !State.PhoneDiagnostics.TryGetValue(
            phone.PhysicalDeviceId.ToString("D").ToUpperInvariant(), out var report))
            return UiText.Get("win_no_phone_report");
        try {
            using var parsed = JsonDocument.Parse(report);
            return parsed.RootElement.TryGetProperty("recentEvents", out var events) &&
                events.ValueKind == JsonValueKind.Array
                ? string.Join(Environment.NewLine, events.EnumerateArray().Select(e => e.GetString()))
                : UiText.Get("win_no_phone_report");
        }
        catch (JsonException) { return UiText.Get("win_no_phone_report"); }
    }

    public byte[] SupportDiagnosticsData(PairedPhone? phone)
    {
        var id = phone?.PhysicalDeviceId.ToString("D").ToUpperInvariant();
        var pending = phone is null ? 0 : new[] { "Host", "Watch" }
            .Sum(kind => Directory.Exists(Path.Combine(TransferServer.QueuePath(phone), kind))
                ? Directory.EnumerateFiles(Path.Combine(TransferServer.QueuePath(phone), kind),
                    "Analytics-*.ips.ca.synced", SearchOption.AllDirectories).Count() : 0);
        var delivered = id is null ? 0 : State.Delivered.Count(token => token.StartsWith(id + "|"));
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 1, platform = "Windows", generatedAt = DateTimeOffset.Now,
            appVersion = typeof(CompanionRuntime).Assembly.GetName().Version?.ToString(),
            osVersion = Environment.OSVersion.VersionString,
            deviceName = phone?.Name, deviceModel = phone?.Model,
            pairingConfirmed = phone?.ConfirmedAt is not null,
            pendingFiles = pending, deliveredFiles = delivered,
            lastCollection = id is not null && State.LastCollections.TryGetValue(id, out var last)
                ? last : null,
            lastCrash = CrashDiagnostics.Latest(),
            recentEvents = DebugLog.Split(Environment.NewLine).TakeLast(40).ToArray(),
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    public byte[]? PhoneSupportDiagnosticsData(PairedPhone? phone) =>
        phone is not null && State.PhoneDiagnostics.TryGetValue(
            phone.PhysicalDeviceId.ToString("D").ToUpperInvariant(), out var report)
            ? report : null;

    private void Record(string message)
    {
        lock (_events)
        {
            _events.Add($"{DateTimeOffset.Now:O} | {new string(message.Replace('\n', ' ').Take(200).ToArray())}");
            if (_events.Count > 500) _events.RemoveRange(0, _events.Count - 500);
            try {
                Directory.CreateDirectory(StateStore.Root);
                var temporary = EventsFile + ".new";
                File.WriteAllText(temporary, JsonSerializer.Serialize(_events));
                File.Move(temporary, EventsFile, true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { /* Debug logging must not block transfers. */ }
        }
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        Server.Dispose();
        _lifetime.Dispose();
    }
}
