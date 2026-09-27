using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MochiLog_Windows.Services;
using Windows.ApplicationModel.DataTransfer;

namespace MochiLog_Windows.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly CompanionRuntime _runtime = CompanionRuntime.Shared;

    public SettingsPage()
    {
        InitializeComponent();
        PageTitle.Text = UiText.Get("win_settings");
        StartupToggle.Header = UiText.Get("win_startup");
        StartupToggle.IsOn = StartupRegistration.Enabled;
        StartupToggle.Toggled += StartupToggled;
        TrayToggle.Header = UiText.Get("win_tray");
        TrayToggle.IsOn = TrayPreferences.Enabled;
        TrayToggle.Toggled += TrayToggled;
        SupportTitle.Text = UiText.Get("win_support");
        SupportIntro.Text = UiText.Get("win_support_intro");
        PrivacyNote.Text = UiText.Get("win_privacy");
        ExportButton.Content = UiText.Get("win_export");
        MailButton.Content = UiText.Get("win_mail");
        DebugTitle.Text = UiText.Get("win_debug");
        CopyButton.Content = UiText.Get("win_copy");
        PhoneTitle.Text = UiText.Get("win_phone_report");
        HelpTitle.Text = UiText.Get("win_help");
        HelpContent.Text = UiText.Get("win_help_content");
        Loaded += (_, _) => { _runtime.Changed += RuntimeChanged; Render(); };
        Unloaded += (_, _) => _runtime.Changed -= RuntimeChanged;
    }

    private void RuntimeChanged() => DispatcherQueue.TryEnqueue(Render);
    private void Render()
    {
        DebugText.Text = _runtime.DebugLog;
        PhoneText.Text = _runtime.LatestPhoneDiagnosticsText;
    }

    private void CopyClicked(object sender, RoutedEventArgs args)
    {
        var package = new DataPackage();
        package.SetText(_runtime.DebugLog);
        Clipboard.SetContent(package);
    }

    private async void StartupToggled(object sender, RoutedEventArgs args)
    {
        try { StartupRegistration.SetEnabled(StartupToggle.IsOn); }
        catch (Exception error) { await ShowErrorAsync(error.Message); }
    }

    private async void TrayToggled(object sender, RoutedEventArgs args)
    {
        try {
            TrayPreferences.Enabled = TrayToggle.IsOn;
            ((App)Microsoft.UI.Xaml.Application.Current).MainWindow?.UpdateTrayPreference();
            if (StartupRegistration.Enabled) StartupRegistration.SetEnabled(true);
        }
        catch (Exception error) {
            try { TrayPreferences.Enabled = false; }
            catch { /* Closing still exits if the icon was not created. */ }
            TrayToggle.Toggled -= TrayToggled;
            TrayToggle.IsOn = TrayPreferences.Enabled;
            TrayToggle.Toggled += TrayToggled;
            await ShowErrorAsync(error.Message);
        }
    }

    private async Task<string> ExportReportAsync()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory,
            $"MochiLog-Windows-support-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.json");
        await File.WriteAllBytesAsync(file, _runtime.SupportDiagnosticsData());
        return file;
    }

    private async void ExportClicked(object sender, RoutedEventArgs args)
    {
        try
        {
            var file = await ExportReportAsync();
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") {
                UseShellExecute = true
            });
        }
        catch (Exception error) { await ShowErrorAsync(error.Message); }
    }

    private async void MailClicked(object sender, RoutedEventArgs args)
    {
        try
        {
            var file = await ExportReportAsync();
            var subject = Uri.EscapeDataString("[MochiLog] Windows PC transfer alpha support");
            var body = Uri.EscapeDataString(UiText.Get("win_mail_body") + "\r\n\r\n" + file);
            Process.Start(new ProcessStartInfo(
                $"mailto:support@mochilog.ryuya-dev.net?subject={subject}&body={body}") {
                UseShellExecute = true
            });
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") {
                UseShellExecute = true
            });
        }
        catch (Exception error) { await ShowErrorAsync(error.Message); }
    }

    private async Task ShowErrorAsync(string message) =>
        await new ContentDialog { XamlRoot = XamlRoot, Title = UiText.Get("win_setup_failed"),
            Content = message, CloseButtonText = UiText.Get("win_close") }.ShowAsync();
}
