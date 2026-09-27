using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MochiLog_Windows.Pages;
using MochiLog_Windows.Services;
using System.Drawing;
using System.Windows.Forms;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MochiLog_Windows;

public sealed partial class MainWindow : Window
{
    private NotifyIcon? _tray;
    private Icon? _trayIcon;
    private bool _quitting;

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
            if (_quitting || !TrayPreferences.Enabled) return;
            args.Cancel = true;
            AppWindow.Hide();
        };
        Closed += (_, _) => DisposeTray();
        UpdateTrayPreference();
    }

    public void UpdateTrayPreference()
    {
        if (!TrayPreferences.Enabled) {
            if (!AppWindow.IsVisible) Restore();
            DisposeTray();
            return;
        }
        if (_tray is not null) return;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        _trayIcon = File.Exists(iconPath) ? new Icon(iconPath) :
            Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        var menu = new ContextMenuStrip();
        menu.Items.Add(UiText.Get("win_tray_open"), null, (_, _) => Restore());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(UiText.Get("win_tray_exit"), null, (_, _) => Quit());
        _tray = new NotifyIcon {
            Icon = _trayIcon, Text = "MochiLog Windows", ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => Restore();
    }

    public void Restore()
    {
        AppWindow.Show();
        Activate();
    }

    private void Quit()
    {
        _quitting = true;
        Close();
    }

    private void DisposeTray()
    {
        if (_tray is not null) {
            _tray.Visible = false;
            _tray.ContextMenuStrip?.Dispose();
            _tray.Dispose();
            _tray = null;
        }
        _trayIcon?.Dispose();
        _trayIcon = null;
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
