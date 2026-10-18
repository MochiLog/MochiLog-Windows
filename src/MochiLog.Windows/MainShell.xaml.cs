using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MochiLog_Windows.Pages;
using MochiLog_Windows.Services;

namespace MochiLog_Windows;

/// All named controls belong to this disposable visual tree, not the persistent
/// HWND/tray owner. Background runtime services never own this shell.
public sealed partial class MainShell : UserControl
{
    private readonly MainWindow _owner;
    public TitleBar TitleBar => AppTitleBar;
    public bool ShowsLiveBattery => NavFrame.Content is LiveBatteryPage;
    public string PageKey { get; private set; } = "home";

    public MainShell(MainWindow owner, string page)
    {
        _owner = owner;
        InitializeComponent();
        NavFrame.CacheSize = 0;
        ((NavigationViewItem)NavView.MenuItems[0]).Content = UiText.Get("win_home");
        ((NavigationViewItem)NavView.MenuItems[1]).Content = UiText.Get("live_title");
        ((NavigationViewItem)NavView.MenuItems[2]).Content = UiText.Get("mt_battery_logs");
        ((NavigationViewItem)NavView.MenuItems[3]).Content = UiText.Get("win_about");
        SelectPage(page);
        Loaded += ShellLoaded;
    }

    private void ShellLoaded(object sender, RoutedEventArgs args)
    {
        SelectPage(PageKey);
        _ = _owner.ConfigureAutomaticUpdatesAsync();
    }

    public void SelectPage(string page)
    {
        var selected =
            page == "settings"
                ? NavView.SettingsItem
                : NavView
                    .MenuItems.OfType<NavigationViewItem>()
                    .FirstOrDefault(item => item.Tag as string == page);
        if (selected is not null)
            NavView.SelectedItem = selected;
        Navigate(page);
    }

    private void Navigate(string page)
    {
        var type = page switch
        {
            "settings" => typeof(SettingsPage),
            "liveBattery" => typeof(LiveBatteryPage),
            "battery" => typeof(BatteryLogsPage),
            "about" => typeof(AboutPage),
            _ => typeof(HomePage),
        };
        if (NavFrame.CurrentSourcePageType != type)
            NavFrame.Navigate(type);
        AppTitleBar.IsBackButtonVisible = NavFrame.CanGoBack;
        PageKey = page;
        _owner.RememberPage(page);
    }

    public void Release()
    {
        Loaded -= ShellLoaded;
        NavView.SelectionChanged -= NavView_SelectionChanged;
        AppTitleBar.PaneToggleRequested -= TitleBar_PaneToggleRequested;
        AppTitleBar.BackRequested -= TitleBar_BackRequested;
        NavFrame.Content = null;
        NavFrame.BackStack.Clear();
        NavFrame.ForwardStack.Clear();
        Content = null;
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args) =>
        NavView.IsPaneOpen = !NavView.IsPaneOpen;

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
            NavFrame.GoBack();
    }

    private void NavView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args
    )
    {
        if (args.IsSettingsSelected)
            Navigate("settings");
        else if (args.SelectedItem is NavigationViewItem item)
            Navigate(item.Tag as string ?? "home");
    }
}
