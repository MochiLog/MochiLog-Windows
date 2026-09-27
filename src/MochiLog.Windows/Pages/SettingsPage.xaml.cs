using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MochiLog_Windows.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Email;
using Windows.Storage;
using Windows.Storage.Streams;

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
        UpdateTrayStatus();
        SupportTitle.Text = UiText.Get("win_support");
        SupportIntro.Text = UiText.Get("win_support_intro");
        SupportDevicePicker.Header = UiText.Get("win_support_device");
        NicknameBox.Header = UiText.Get("win_support_nickname");
        EmailBox.Header = UiText.Get("win_support_email");
        MessageBox.Header = UiText.Get("win_support_message");
        PrivacyNote.Text = UiText.Get("win_privacy");
        ExportButton.Content = UiText.Get("win_export");
        MailButton.Content = UiText.Get("win_mail");
        DebugTitle.Text = UiText.Get("win_debug");
        CopyButton.Content = UiText.Get("win_copy");
        RefreshDebugButton.Content = UiText.Get("mt_015");
        PhoneTitle.Text = UiText.Get("win_phone_report");
        CopyPhoneButton.Content = UiText.Get("win_copy");
        HelpTitle.Text = UiText.Get("win_help");
        HelpContent.Text = UiText.Get("win_help_content");
        Loaded += (_, _) => { _runtime.Changed += RuntimeChanged; Render(); };
        Unloaded += (_, _) => _runtime.Changed -= RuntimeChanged;
    }

    private void RuntimeChanged() => DispatcherQueue.TryEnqueue(Render);
    private void UpdateTrayStatus()
    {
        var window = ((App)Microsoft.UI.Xaml.Application.Current).MainWindow;
        TrayStatus.Text = window?.TrayError is { } error
            ? UiText.Format("win_tray_status_failed", error)
            : UiText.Get(window?.IsTrayReady == true
                ? "win_tray_status_ready" : "win_tray_status_unavailable");
    }
    private void Render()
    {
        var selected = SupportDevicePicker.SelectedIndex;
        var labels = _runtime.State.Phones.Select(phone => $"{phone.Name} ({phone.Model})").ToArray();
        if (SupportDevicePicker.ItemsSource is not string[] current || !current.SequenceEqual(labels)) {
            SupportDevicePicker.ItemsSource = labels;
            SupportDevicePicker.SelectedIndex = selected >= 0 && selected < labels.Length ? selected :
                labels.Length > 0 ? 0 : -1;
        }
        DebugText.Text = _runtime.DebugLog;
        PhoneText.Text = _runtime.PhoneDiagnosticsText(SelectedPhone());
        MailButton.IsEnabled = !string.IsNullOrWhiteSpace(NicknameBox.Text) &&
            !string.IsNullOrWhiteSpace(EmailBox.Text) &&
            !string.IsNullOrWhiteSpace(MessageBox.Text);
    }

    private PairedPhone? SelectedPhone() => SupportDevicePicker.SelectedIndex is >= 0 and var index &&
        index < _runtime.State.Phones.Count ? _runtime.State.Phones[index] : null;

    private void SupportDeviceChanged(object sender, SelectionChangedEventArgs args) => Render();
    private void SupportTextChanged(object sender, TextChangedEventArgs args) => Render();
    private void RefreshDebugClicked(object sender, RoutedEventArgs args) => Render();

    private void CopyClicked(object sender, RoutedEventArgs args)
    {
        var package = new DataPackage();
        package.SetText(_runtime.DebugLog);
        Clipboard.SetContent(package);
    }

    private void CopyPhoneClicked(object sender, RoutedEventArgs args)
    {
        var package = new DataPackage();
        package.SetText(_runtime.PhoneDiagnosticsText(SelectedPhone()));
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
            UpdateTrayStatus();
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

    private async Task<(string Computer, string? Phone)> ExportReportAsync()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        Directory.CreateDirectory(directory);
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var computer = Path.Combine(directory, $"MochiLog-Windows-support-{stamp}.json");
        await File.WriteAllBytesAsync(computer, _runtime.SupportDiagnosticsData(SelectedPhone()));
        string? phone = null;
        if (_runtime.PhoneSupportDiagnosticsData(SelectedPhone()) is { } report) {
            phone = Path.Combine(directory, $"MochiLog-iPhone-support-{stamp}.json");
            await File.WriteAllBytesAsync(phone, report);
        }
        return (computer, phone);
    }

    private async void ExportClicked(object sender, RoutedEventArgs args)
    {
        try
        {
            var files = await ExportReportAsync();
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{files.Computer}\"") {
                UseShellExecute = true
            });
        }
        catch (Exception error) { await ShowErrorAsync(error.Message); }
    }

    private async void MailClicked(object sender, RoutedEventArgs args)
    {
        try
        {
            var files = await ExportReportAsync();
            var subject = "[MochiLog Windows] PC transfer alpha support";
            var body = $"{UiText.Get("win_support_nickname")}: {NicknameBox.Text}\r\n" +
                $"{UiText.Get("win_support_email")}: {EmailBox.Text}\r\n" +
                $"{UiText.Get("win_support_device")}: {SelectedPhone()?.Model ?? "-"}\r\n\r\n" +
                $"{UiText.Get("win_support_message")}:\r\n{MessageBox.Text}";
            try {
                var message = new EmailMessage { Subject = subject, Body = body };
                message.To.Add(new EmailRecipient("support@mochilog.ryuya-dev.net"));
                foreach (var file in new[] { files.Computer, files.Phone }.OfType<string>()) {
                    var storage = await StorageFile.GetFileFromPathAsync(file);
                    message.Attachments.Add(new EmailAttachment(Path.GetFileName(file),
                        RandomAccessStreamReference.CreateFromFile(storage)));
                }
                await EmailManager.ShowComposeNewEmailAsync(message);
            }
            catch {
                var url = $"mailto:support@mochilog.ryuya-dev.net?subject={Uri.EscapeDataString(subject)}" +
                    $"&body={Uri.EscapeDataString(body + "\r\n\r\n" + UiText.Get("win_mail_body") + "\r\n" + files.Computer + (files.Phone is null ? "" : "\r\n" + files.Phone))}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{files.Computer}\"") {
                    UseShellExecute = true
                });
            }
        }
        catch (Exception error) { await ShowErrorAsync(error.Message); }
    }

    private async Task ShowErrorAsync(string message) =>
        await new ContentDialog { XamlRoot = XamlRoot, Title = UiText.Get("win_setup_failed"),
            Content = message, CloseButtonText = UiText.Get("win_close") }.ShowAsync();
}
