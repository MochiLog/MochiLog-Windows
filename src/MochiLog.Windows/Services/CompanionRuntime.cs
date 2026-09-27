using System.Text.Json;

namespace MochiLog_Windows.Services;

public sealed class CompanionRuntime : IDisposable
{
    public static CompanionRuntime Shared { get; } = new();
    public CompanionState State { get; }
    public TransferServer Server { get; }
    public IReadOnlyList<ConnectedDevice> Available { get; private set; } = [];
    public string Status { get; private set; } = "Starting…";
    public string CollectionStatus { get; private set; } = "";
    public event Action? Changed;
    private readonly SemaphoreSlim _collection = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<string> _events = [];
    private bool _started;

    private CompanionRuntime()
    {
        State = StateStore.Load();
        Server = new TransferServer(State);
        Server.SupportReport = phone => JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 1, platform = "Windows", generatedAt = DateTimeOffset.Now,
            deviceName = phone.Name, osVersion = Environment.OSVersion.VersionString,
            recentEvents = DebugLog.Split(Environment.NewLine).TakeLast(30).ToArray()
        });
        Server.StatusChanged += message => Record(message);
        Server.PhoneConfirmed += phone => Record($"{phone.Name}: app pairing confirmed");
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
            Status = "Searching for trusted iPhone and iPad devices…";
            Changed?.Invoke();
            Available = await Collector.BrowseAsync(State.Phones, _lifetime.Token);
            Status = Available.Count == 0 ?
                "No unlocked trusted device was found. Connect once by USB or check Wi-Fi." :
                $"{Available.Count} device(s) available";
        }
        catch (Exception error) { Status = "Device search: " + error.Message; }
        Record(Status);
    }

    public async Task<ConnectedDevice> PrepareUsbAsync()
    {
        Status = "Waiting for USB trust and wireless pairing…";
        Changed?.Invoke();
        var device = await Collector.PrepareUsbPairingAsync(_lifetime.Token);
        Status = $"{device.Name}: USB trust recorded. Disconnect USB and verify Wi-Fi.";
        Record(Status);
        return device;
    }

    public async Task VerifyWirelessAsync(ConnectedDevice device)
    {
        Status = $"Checking {device.Name} over Wi-Fi…";
        Changed?.Invoke();
        await Collector.VerifyWirelessAsync(device.Udid, _lifetime.Token);
        Status = $"{device.Name}: wireless diagnostics connection verified.";
        Available = Available.Where(item => item.Udid != device.Udid).Append(device).ToArray();
        Record(Status);
    }

    public PairingInvitation BeginPairing(ConnectedDevice device)
    {
        if (!Available.Any(item => item.Udid == device.Udid))
            throw new InvalidOperationException("Verify this device's wireless connection first.");
        var invitation = Server.BeginPairing(device);
        Record($"{device.Name}: pairing QR generated (expires after 3 minutes)");
        return invitation;
    }

    public async Task CollectAsync(PairedPhone? selected = null)
    {
        if (!await _collection.WaitAsync(0)) return;
        try
        {
            foreach (var phone in selected is null ? State.Phones.ToArray() : [selected])
            {
                CollectionStatus = $"{phone.Name}: reading diagnostics…";
                Changed?.Invoke();
                try
                {
                    var result = await Collector.CollectAsync(phone, State,
                        (done, total) => { CollectionStatus = $"{phone.Name}: {done}/{total} checked";
                            Changed?.Invoke(); }, _lifetime.Token);
                    CollectionStatus = $"{phone.Name}: {result.Saved} battery log(s) queued, " +
                        $"{result.Skipped} excluded, {result.Failed} failed.";
                    if (result.LastError is not null) CollectionStatus += " " + result.LastError;
                    Record(CollectionStatus);
                }
                catch (Exception error) { Record($"{phone.Name}: {error.Message}"); }
            }
        }
        finally { _collection.Release(); }
    }

    private async Task PeriodicCollectionAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            while (await timer.WaitForNextTickAsync(token)) await CollectAsync();
        }
        catch (OperationCanceledException) { }
    }

    public string DebugLog
    {
        get { lock (_events) return string.Join(Environment.NewLine, _events); }
    }

    private void Record(string message)
    {
        lock (_events)
        {
            _events.Add($"{DateTimeOffset.Now:O} | {message.Replace('\n', ' ')}");
            if (_events.Count > 100) _events.RemoveRange(0, _events.Count - 100);
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
