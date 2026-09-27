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
    public bool IsTrayReady => _tray is not null;
    public string? TrayError { get; private set; }
    public double ContentViewportWidth =>
        AppWindow.Size.Width / (NavView.XamlRoot?.RasterizationScale ?? 1) -
        (NavView.IsPaneOpen ? NavView.OpenPaneLength : NavView.CompactPaneLength);

    public MainWindow()
    {
        InitializeComponent();

        ((NavigationViewItem)NavView.MenuItems[0]).Content = UiText.Get("win_home");
        ((NavigationViewItem)NavView.MenuItems[1]).Content = UiText.Get("win_about");

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Closing += (_, args) => {
            if (_quitting || _tray is null || !TrayPreferences.Enabled) return;
            args.Cancel = true;
            AppWindow.Hide();
        };
        Closed += (_, _) => DisposeTray();
        // The shell icon is registered after Activate(), when the HWND is ready.
    }

    public void UpdateTrayPreference()
    {
        // The notification-area icon is available whenever the app is running.
        // The preference controls only whether closing the window keeps it running.
        if (!TrayPreferences.Enabled && !AppWindow.IsVisible) Restore();
        if (_tray is not null) return;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        _tray = new TrayIcon(WinRT.Interop.WindowNative.GetWindowHandle(this), iconPath,
            UiText.Get("win_tray_open"), UiText.Get("win_tray_exit"), Restore,
            () => DispatcherQueue.TryEnqueue(Quit));
        TrayError = null;
    }

    public void ReportTrayError(Exception error) => TrayError = error.Message;

    public void Restore()
    {
        AppWindow.Show();
        Activate();
        if (_pendingUpdate is not null) _ = PromptForUpdateAsync();
    }

    public async Task CheckForUpdatesAsync(bool manual)
    {
        try
        {
            _pendingUpdate = await UpdateService.CheckAsync();
            if (_pendingUpdate is not null && AppWindow.IsVisible)
                await PromptForUpdateAsync();
            else if (manual)
                await ShowUpdateMessageAsync(UiText.Get("win_update_current"));
        }
        catch (Exception error)
        {
            if (manual) await ShowUpdateMessageAsync(UiText.Format("win_update_failed", error.Message));
        }
    }

    private async Task PromptForUpdateAsync()
    {
        if (_pendingUpdate is not { } update || _updatePromptOpen || !AppWindow.IsVisible) return;
        _updatePromptOpen = true;
        try
        {
            var answer = await DialogCoordinator.ShowAsync(new ContentDialog {
                XamlRoot = NavView.XamlRoot,
                Title = UiText.Format("win_update_available", update.Version),
                Content = UiText.Get("win_update_confirm"),
                PrimaryButtonText = UiText.Get("win_update_install"),
                CloseButtonText = UiText.Get("win_close")
            });
            if (answer != ContentDialogResult.Primary) return;
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
        finally { _updatePromptOpen = false; }
    }

    private async Task ShowUpdateMessageAsync(string message) =>
        await DialogCoordinator.ShowAsync(new ContentDialog { XamlRoot = NavView.XamlRoot,
            Title = UiText.Get("win_update_title"), Content = message,
            CloseButtonText = UiText.Get("win_close") });

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

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        NavFrame.GoBack();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
        }
        else if (args.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag)
            {
                case "home":
                    NavFrame.Navigate(typeof(HomePage));
                    break;
                case "about":
                    NavFrame.Navigate(typeof(AboutPage));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown navigation item tag: {item.Tag}");
            }
        }
    }
}
