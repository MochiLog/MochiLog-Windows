using System.Diagnostics;
using System.Globalization;
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

    private bool _rendering;
    private string _debugContent = "";
    private string _phoneContent = "";
    private int _logGeneration;
    private bool _loaded;
    private string? _logRequestKey;

    public SettingsPage()
    {
        InitializeComponent();
        PageTitle.Text = UiText.Get("win_settings");
        AutomaticUpdateToggle.Header = UiText.Get("update_optin_title");
        AutomaticUpdateNote.Text = UiText.Get("update_optin_note");
        AutomaticUpdateToggle.IsOn = UpdatePreferences.Enabled;
        AutomaticUpdateToggle.Toggled += async (_, _) => {
            UpdatePreferences.Enabled = AutomaticUpdateToggle.IsOn;
            await ((App)Application.Current).MainWindow!.ConfigureAutomaticUpdatesAsync();
        };
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
        IncidentTitle.Text = UiText.Get("mt_log_incident");
        IncidentHint.Text = UiText.Get("mt_log_support_days");
        IncidentDatePicker.Date = DateTimeOffset.Now;
        IncidentTimePicker.Time = DateTime.Now.TimeOfDay;
        PrivacyNote.Text = UiText.Get("win_privacy");
        ExportButton.Content = UiText.Get("win_export");
        MailButton.Content = UiText.Get("win_mail");
        DebugTitle.Text = UiText.Get("win_debug");
        DebugCategoryPicker.Header = UiText.Get("mt_log_category");
        PhoneCategoryPicker.Header = UiText.Get("mt_log_category");
        DebugDayPicker.Header = UiText.Get("mt_log_date");
        RetentionPicker.Header = UiText.Get("mt_log_retention");
        RetentionPicker.ItemsSource = new[] { 7, 30, 90, 180, 365 };
        RetentionPicker.SelectedItem = _runtime.DebugRetentionDays;
        DeleteDebugButton.Content = UiText.Get("mt_log_delete");
        CopyButton.Content = UiText.Get("win_copy");
        RefreshDebugButton.Content = UiText.Get("mt_015");
        PhoneTitle.Text = UiText.Get("win_phone_report");
        PhoneDayPicker.Header = UiText.Get("mt_log_date");
        CopyPhoneButton.Content = UiText.Get("win_copy");
        HelpTitle.Text = UiText.Get("win_help");
        HelpContent.Text = UiText.Get("win_help_content");
        Loaded += (_, _) => { _loaded = true; _runtime.Changed += RuntimeChanged; Render(); };
        Unloaded += (_, _) => {
            _loaded = false;
            _logGeneration++;
            _logRequestKey = null;
            _runtime.Changed -= RuntimeChanged;
        };
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
        // Updating picker selections can synchronously raise SelectionChanged.
        if (_rendering) return;
        _rendering = true;
        try {
            var selected = SupportDevicePicker.SelectedIndex;
            var labels = _runtime.State.Phones.Select(phone => $"{phone.Name} ({phone.Model})").ToArray();
            if (SupportDevicePicker.ItemsSource is not string[] current || !current.SequenceEqual(labels)) {
                SupportDevicePicker.ItemsSource = labels;
                SupportDevicePicker.SelectedIndex = selected >= 0 && selected < labels.Length ? selected :
                    labels.Length > 0 ? 0 : -1;
            }
            MailButton.IsEnabled = !string.IsNullOrWhiteSpace(NicknameBox.Text) &&
                !string.IsNullOrWhiteSpace(EmailBox.Text) &&
                !string.IsNullOrWhiteSpace(MessageBox.Text);
        } finally { _rendering = false; }
        if (_loaded) _ = ReloadLogsAsync();
    }

    // Runtime status changes must not repeatedly read and parse multi-MB logs on
    // the UI thread. A viewer snapshot changes only on selection or refresh.
    private async Task ReloadLogsAsync()
    {
        var day = DebugDayPicker.SelectedItem as string;
        var phoneDay = PhoneDayPicker.SelectedItem as string;
        var phone = SelectedPhone();
        var category = SelectedCategory(DebugCategoryPicker);
        var phoneCategory = SelectedCategory(PhoneCategoryPicker);
        var key = $"{DateTime.Today:yyyy-MM-dd}|{day}|{phoneDay}|{phone?.PhysicalDeviceId}|{category}|{phoneCategory}";
        if (_logRequestKey == key) return;
        _logRequestKey = key;
        var generation = ++_logGeneration;
        CopyButton.IsEnabled = CopyPhoneButton.IsEnabled = false;
        DebugLoading.IsActive = PhoneLoading.IsActive = true;
        try {
            var snapshot = await Task.Run(() => {
                var days = _runtime.DebugLogDays.ToArray();
                var phoneDays = _runtime.PhoneDebugDays(phone).ToArray();
                var selectedDay = day is not null && days.Contains(day) ? day : days.FirstOrDefault();
                var selectedPhoneDay = phoneDay is not null && phoneDays.Contains(phoneDay)
                    ? phoneDay : phoneDays.FirstOrDefault();
                var raw = selectedDay is not null ? _runtime.DebugLogForDay(selectedDay) : _runtime.DebugLog;
                var phoneRaw = selectedPhoneDay is not null
                    ? _runtime.PhoneDebugForDay(phone, selectedPhoneDay) : _runtime.PhoneDiagnosticsText(phone);
                var categories = DiagnosticLogViewer.Categories(raw);
                var phoneCategories = DiagnosticLogViewer.Categories(phoneRaw);
                var selectedCategory = categories.Contains(category) ? category : null;
                var selectedPhoneCategory = phoneCategories.Contains(phoneCategory) ? phoneCategory : null;
                var text = DiagnosticLogViewer.Text(raw, selectedCategory);
                var phoneText = DiagnosticLogViewer.Text(phoneRaw, selectedPhoneCategory);
                return (days, phoneDays, selectedDay, selectedPhoneDay, categories, phoneCategories,
                    selectedCategory, selectedPhoneCategory, text, phoneText,
                    lines: DiagnosticLines(text), phoneLines: DiagnosticLines(phoneText));
            });
            if (!_loaded || generation != _logGeneration) return;
            _rendering = true;
            try {
                DebugDayPicker.ItemsSource = snapshot.days;
                DebugDayPicker.SelectedItem = snapshot.selectedDay;
                PhoneDayPicker.ItemsSource = snapshot.phoneDays;
                PhoneDayPicker.SelectedItem = snapshot.selectedPhoneDay;
                UpdateCategoryPicker(DebugCategoryPicker, snapshot.categories, snapshot.selectedCategory);
                UpdateCategoryPicker(PhoneCategoryPicker, snapshot.phoneCategories, snapshot.selectedPhoneCategory);
                _debugContent = snapshot.text;
                _phoneContent = snapshot.phoneText;
                DebugText.ItemsSource = snapshot.lines;
                PhoneText.ItemsSource = snapshot.phoneLines;
                _logRequestKey = $"{DateTime.Today:yyyy-MM-dd}|{snapshot.selectedDay}|{snapshot.selectedPhoneDay}|{phone?.PhysicalDeviceId}|{snapshot.selectedCategory}|{snapshot.selectedPhoneCategory}";
            } finally { _rendering = false; }
        } catch (Exception error) {
            if (_loaded && generation == _logGeneration) await ShowErrorAsync(error.Message);
        } finally {
            if (_loaded && generation == _logGeneration) {
                DebugLoading.IsActive = PhoneLoading.IsActive = false;
                CopyButton.IsEnabled = _debugContent.Length > 0;
                CopyPhoneButton.IsEnabled = _phoneContent.Length > 0;
            }
        }
    }

    private static string? SelectedCategory(ComboBox picker) =>
        (picker.SelectedItem as ComboBoxItem)?.Tag as string is { } category && category != "all"
            ? category : null;

    private static void UpdateCategoryPicker(ComboBox picker, string[] categories, string? selected)
    {
        var ids = new[] { "all" }.Concat(categories).ToArray();
        if (!picker.Items.OfType<ComboBoxItem>().Select(item => item.Tag as string).SequenceEqual(ids)) {
            picker.Items.Clear();
            foreach (var id in ids) picker.Items.Add(new ComboBoxItem {
                Tag = id, Content = UiText.Get("mt_log_" + id.Replace('-', '_'))
            });
        }
        picker.SelectedIndex = Math.Max(0, Array.IndexOf(ids, selected ?? "all"));
    }

    private void DebugCategoryChanged(object sender, SelectionChangedEventArgs args)
    {
        if (DebugText is not null && PhoneText is not null) Render();
    }

    // ListView virtualizes the visible rows; copying retains the complete original text.
    private static string[] DiagnosticLines(string content) =>
        content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private PairedPhone? SelectedPhone() => SupportDevicePicker.SelectedIndex is >= 0 and var index &&
        index < _runtime.State.Phones.Count ? _runtime.State.Phones[index] : null;

    private void SupportDeviceChanged(object sender, SelectionChangedEventArgs args) => Render();
    private void SupportTextChanged(object sender, TextChangedEventArgs args) => Render();
    private void RefreshDebugClicked(object sender, RoutedEventArgs args) { _logRequestKey = null; Render(); }

    private void DebugDayChanged(object sender, SelectionChangedEventArgs args)
    {
        if (DebugText is not null) Render();
    }

    private void PhoneDayChanged(object sender, SelectionChangedEventArgs args)
    {
        if (PhoneText is not null) Render();
    }

    private async void RetentionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (RetentionPicker.SelectedItem is int days && _runtime is not null) {
            await Task.Run(() => _runtime.DebugRetentionDays = days);
            _logRequestKey = null;
            Render();
        }
    }

    private async void DeleteDebugClicked(object sender, RoutedEventArgs args)
    {
        var result = await DialogCoordinator.ShowAsync(new ContentDialog {
            XamlRoot = XamlRoot,
            Title = UiText.Get("mt_log_delete_confirm"),
            PrimaryButtonText = UiText.Get("mt_log_delete"),
            CloseButtonText = UiText.Get("win_close")
        });
        if (result != ContentDialogResult.Primary) return;
        await Task.Run(() => _runtime.DeleteDebugLogs());
        _logRequestKey = null;
        Render();
    }

    private void CopyClicked(object sender, RoutedEventArgs args)
    {
        var package = new DataPackage();
        package.SetText(_debugContent);
        Clipboard.SetContent(package);
    }

    private void CopyPhoneClicked(object sender, RoutedEventArgs args)
    {
        var package = new DataPackage();
        package.SetText(_phoneContent);
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

    private DateTime IncidentDateTime => (IncidentDatePicker.Date ?? DateTimeOffset.Now)
        .Date.Add(IncidentTimePicker.Time);

    private async Task<(string Computer, string? Phone, string[] Logs)> ExportReportAsync()
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
        var logs = new List<string>();
        for (var offset = -2; offset <= 0; offset++) {
            var day = IncidentDateTime.AddDays(offset)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var path = Path.Combine(directory, $"MochiLog-Windows-debug-{day}-{stamp}.log");
            await File.WriteAllTextAsync(path, _runtime.DebugLogForDay(day));
            logs.Add(path);
            if (SelectedPhone() is { } selectedPhone) {
                var phonePath = Path.Combine(directory,
                    $"MochiLog-iPhone-debug-{day}-{stamp}.log");
                await File.WriteAllTextAsync(phonePath,
                    _runtime.PhoneDebugForDay(selectedPhone, day));
                logs.Add(phonePath);
            }
        }
        return (computer, phone, logs.ToArray());
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
                $"{UiText.Get("mt_log_incident")}: {IncidentDateTime:g}\r\n\r\n" +
                $"{UiText.Get("win_support_message")}:\r\n{MessageBox.Text}";
            try {
                var message = new EmailMessage { Subject = subject, Body = body };
                message.To.Add(new EmailRecipient("support@mochilog.ryuya-dev.net"));
                foreach (var file in new[] { files.Computer, files.Phone }.OfType<string>()
                    .Concat(files.Logs)) {
                    var storage = await StorageFile.GetFileFromPathAsync(file);
                    message.Attachments.Add(new EmailAttachment(Path.GetFileName(file),
                        RandomAccessStreamReference.CreateFromFile(storage)));
                }
                await EmailManager.ShowComposeNewEmailAsync(message);
            }
            catch {
                var url = $"mailto:support@mochilog.ryuya-dev.net?subject={Uri.EscapeDataString(subject)}" +
                    $"&body={Uri.EscapeDataString(body + "\r\n\r\n" + UiText.Get("win_mail_body") + "\r\n" + string.Join("\r\n", new[] { files.Computer, files.Phone }.OfType<string>().Concat(files.Logs)))}";
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{files.Computer}\"") {
                    UseShellExecute = true
                });
            }
        }
        catch (Exception error) { await ShowErrorAsync(error.Message); }
    }

    private async Task ShowErrorAsync(string message) =>
        await DialogCoordinator.ShowAsync(new ContentDialog { XamlRoot = XamlRoot,
            Title = UiText.Get("win_setup_failed"), Content = message,
            CloseButtonText = UiText.Get("win_close") });
}
