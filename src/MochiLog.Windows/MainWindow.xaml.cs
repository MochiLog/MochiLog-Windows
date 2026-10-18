using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MochiLog_Windows.Pages;
using MochiLog_Windows.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MochiLog_Windows;

public sealed partial class MainWindow : Window
{
    private TrayIcon? _tray;
    private bool _quitting;
    private AvailableUpdate? _pendingUpdate;
    private bool _updatePromptOpen;
    private bool _checkingUpdate;
    private bool _consentPromptOpen;
    private DateTimeOffset _lastAutomaticCheck = DateTimeOffset.MinValue;
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(1) };
    public bool IsTrayReady => _tray is not null;
    public string? TrayError { get; private set; }
    private MainShell? _shell;
    private string _page = "home";
    private long _visibilityGeneration;
    private WeakReference? _lastReleasedShell;

    private sealed record UiCheckCycle(
        int Cycle,
        bool Released,
        bool Restored,
        NativeDiagnostics.Resources Before,
        NativeDiagnostics.Resources After
    );

    private Microsoft.UI.Xaml.XamlRoot? DialogRoot => _shell?.XamlRoot;
    public bool IsUiReleased => _shell is null;

    internal void RememberPage(string page) => _page = page;

    private void EnsureShell()
    {
        if (_shell is not null)
            return;
        _shell = new MainShell(this, _page);
        Content = _shell;
        SetTitleBar(_shell.TitleBar);
        CompanionRuntime.Shared.RecordUiLifecycle("created; page=" + _page);
    }

    public void HideToTray()
    {
        _visibilityGeneration++;
        CompanionRuntime.Shared.WatchBattery(false);
        DialogCoordinator.HideActive();
        AppWindow.Hide();
        if (!UiMemoryPreferences.Enabled || _shell is null)
        {
            CompanionRuntime.Shared.RecordUiLifecycle(
                "hidden; visual tree retained; enabled=" + UiMemoryPreferences.Enabled
            );
            return;
        }
        var before = NativeDiagnostics.Measure();
        var released = ReleaseShell();
        _lastReleasedShell = released;
        var generation = _visibilityGeneration;
        DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () =>
            {
                if (generation != _visibilityGeneration || AppWindow.IsVisible)
                    return;
                _ = Task.Run(async () =>
                {
                    // Let dialog animations and XAML Unloaded callbacks finish.
                    await Task.Delay(500);
                    if (generation != Interlocked.Read(ref _visibilityGeneration))
                        return;
                    GC.Collect(
                        GC.MaxGeneration,
                        GCCollectionMode.Forced,
                        blocking: true,
                        compacting: true
                    );
                    GC.WaitForPendingFinalizers();
                    GC.Collect(
                        GC.MaxGeneration,
                        GCCollectionMode.Forced,
                        blocking: true,
                        compacting: true
                    );
                    var after = NativeDiagnostics.Measure();
                    CompanionRuntime.Shared.RecordUiLifecycle(
                        $"released; oldShellAlive={released.IsAlive}; privateBytesBefore={before.PrivateBytes}; privateBytesAfter={after.PrivateBytes}; managedBytesBefore={before.ManagedBytes}; managedBytesAfter={after.ManagedBytes}"
                    );
                });
            }
        );
    }

    private WeakReference ReleaseShell()
    {
        var shell = _shell!;
        _page = shell.PageKey;
        SetTitleBar(null);
        shell.Release();
        Content = null;
        _shell = null;
        return new WeakReference(shell);
    }

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        EnsureShell();
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Closing += (_, args) =>
        {
            if (_quitting || _tray is null || !TrayPreferences.Enabled)
                return;
            args.Cancel = true;
            HideToTray();
        };
        Closed += (_, _) =>
        {
            _updateTimer.Stop();
            DisposeTray();
        };
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync(false);
        _updateTimer.Start();
        // The shell icon is registered after Activate(), when the HWND is ready.
    }

    public void UpdateTrayPreference()
    {
        // The notification-area icon is available whenever the app is running.
        // The preference controls only whether closing the window keeps it running.
        if (!TrayPreferences.Enabled && !AppWindow.IsVisible)
            Restore();
        if (_tray is not null)
            return;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        _tray = new TrayIcon(
            WinRT.Interop.WindowNative.GetWindowHandle(this),
            iconPath,
            UiText.Get("win_tray_open"),
            UiText.Get("win_tray_exit"),
            Restore,
            () => DispatcherQueue.TryEnqueue(Quit)
        );
        TrayError = null;
    }

    public void ReportTrayError(Exception error) => TrayError = error.Message;

    public void Restore()
    {
        _visibilityGeneration++;
        EnsureShell();
        AppWindow.Show();
        CompanionRuntime.Shared.WatchBattery(_shell?.ShowsLiveBattery == true);
        Activate();
        if (_pendingUpdate is not null)
            _ = PromptForUpdateAsync();
        _ = ConfigureAutomaticUpdatesAsync();
    }

    // Local, explicit UI regression check. It never changes records or pairing.
    internal async Task VerifyUiMemoryAsync()
    {
        var previous = UiMemoryPreferences.Enabled;
        var page = _page;
        var runtime = CompanionRuntime.Shared;
        var server = runtime.Server;
        var samples = new List<UiCheckCycle>();
        bool passed = false;
        string? error = null;
        try
        {
            await Task.Delay(1000);
            for (var cycle = 0; cycle < 3; cycle++)
            {
                UiMemoryPreferences.Enabled = true;
                _shell!.SelectPage("settings");
                await Task.Delay(750);
                var before = NativeDiagnostics.Measure();
                HideToTray();
                await Task.Delay(2000);
                var released =
                    _shell is null && Content is null && _lastReleasedShell?.IsAlive == false;
                var after = NativeDiagnostics.Measure();
                Restore();
                await Task.Delay(500);
                var restored = _shell?.PageKey == "settings" && Content is MainShell;
                samples.Add(new(cycle, released, restored, before, after));
                if (!released || !restored || !ReferenceEquals(server, runtime.Server))
                    throw new InvalidOperationException("UI lifetime check failed.");
            }
            UiMemoryPreferences.Enabled = false;
            var retained = _shell;
            HideToTray();
            await Task.Delay(500);
            if (!ReferenceEquals(retained, _shell))
                throw new InvalidOperationException("Disabled setting did not retain the screen.");
            Restore();
            passed = true;
        }
        catch (Exception failure)
        {
            error = failure.Message;
        }
        finally
        {
            UiMemoryPreferences.Enabled = previous;
            Restore();
            _shell!.SelectPage(page);
            var file = Path.Combine(StateStore.Root, "ui-memory-test.json");
            await File.WriteAllBytesAsync(
                file,
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                    new
                    {
                        schema = 1,
                        type = "mochilog-ui-memory-test",
                        generatedAt = DateTimeOffset.Now,
                        passed,
                        error,
                        samples,
                        backgroundServerPreserved = ReferenceEquals(server, runtime.Server),
                    }
                )
            );
            runtime.RecordUiLifecycle("local regression check; passed=" + passed);
        }
    }

    public async Task ConfigureAutomaticUpdatesAsync()
    {
        if (!AppWindow.IsVisible || DialogRoot is null || _consentPromptOpen)
            return;
        if (!UpdatePreferences.Answered)
        {
            _consentPromptOpen = true;
            try
            {
                var choice = await DialogCoordinator.ShowAsync(
                    new ContentDialog
                    {
                        XamlRoot = DialogRoot,
                        Title = UiText.Get("update_optin_title"),
                        Content = UiText.Get("update_optin_note"),
                        PrimaryButtonText = UiText.Get("update_optin_yes"),
                        CloseButtonText = UiText.Get("update_optin_no"),
                    }
                );
                if (choice is { } answer)
                    UpdatePreferences.Enabled = answer == ContentDialogResult.Primary;
            }
            finally
            {
                _consentPromptOpen = false;
            }
        }
        await CheckForUpdatesAsync(false);
    }

    public async Task CheckForUpdatesAsync(bool manual)
    {
        if (
            _checkingUpdate
            || (
                !manual
                && (
                    !UpdatePreferences.Enabled
                    || DateTimeOffset.UtcNow - _lastAutomaticCheck < TimeSpan.FromHours(24)
                )
            )
        )
            return;
        _checkingUpdate = true;
        try
        {
            _pendingUpdate = await UpdateService.CheckAsync();
            if (!manual)
                _lastAutomaticCheck = DateTimeOffset.UtcNow;
            if (_pendingUpdate is not null && AppWindow.IsVisible)
                await PromptForUpdateAsync();
            else if (manual)
                await ShowUpdateMessageAsync(UiText.Get("win_update_current"));
        }
        catch (Exception error)
        {
            if (manual)
                await ShowUpdateMessageAsync(UiText.Format("win_update_failed", error.Message));
        }
        finally
        {
            _checkingUpdate = false;
        }
    }

    private async Task PromptForUpdateAsync()
    {
        if (_pendingUpdate is not { } update || _updatePromptOpen || !AppWindow.IsVisible)
            return;
        _updatePromptOpen = true;
        try
        {
            var answer = await DialogCoordinator.ShowAsync(
                new ContentDialog
                {
                    XamlRoot = DialogRoot,
                    Title = UiText.Format("win_update_available", update.Version),
                    Content = UiText.Get("win_update_confirm"),
                    PrimaryButtonText = UiText.Get("win_update_install"),
                    CloseButtonText = UiText.Get("win_close"),
                }
            );
            if (answer != ContentDialogResult.Primary)
                return;
            _pendingUpdate = null;
            AppWindow.Title = UiText.Get("win_update_downloading");
            var installer = await UpdateService.DownloadAsync(update);
            UpdateService.LaunchInstallerAfterExit(installer);
            Quit();
        }
        catch (Exception error)
        {
            AppWindow.Title = "MochiLog Windows";
            await ShowUpdateMessageAsync(UiText.Format("win_update_failed", error.Message));
        }
        finally
        {
            _updatePromptOpen = false;
        }
    }

    private async Task ShowUpdateMessageAsync(string message) =>
        await DialogCoordinator.ShowAsync(
            new ContentDialog
            {
                XamlRoot = DialogRoot,
                Title = UiText.Get("win_update_title"),
                Content = message,
                CloseButtonText = UiText.Get("win_close"),
            }
        );

    private void Quit()
    {
        _quitting = true;
        Close();
    }

    private void DisposeTray()
    {
        _tray?.Dispose();
        _tray = null;
    }
}
