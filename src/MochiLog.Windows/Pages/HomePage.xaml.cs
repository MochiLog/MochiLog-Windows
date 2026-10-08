using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using MochiLog_Windows.Services;
using QRCoder;
using Windows.Storage.Streams;

namespace MochiLog_Windows.Pages;

public sealed class DeviceListRow
{
    public string Name { get; }
    public string Model { get; }
    public string BadgeText { get; }
    public string BadgeGlyph { get; }
    public SolidColorBrush AccentBrush { get; }
    public SolidColorBrush BadgeBackground { get; }
    public string Key { get; }

    public DeviceListRow(ConnectedDevice device)
    {
        Name = device.Name;
        Model = string.IsNullOrWhiteSpace(device.Model)
            ? UiText.Get("win_model_unknown") : device.Model;
        var needsTrust = device.UsbConnected && !device.UsbTrusted;
        BadgeText = UiText.Get(needsTrust ? "win_badge_trust_needed" :
            device.UsbConnected ? "win_badge_trusted" : "win_badge_wireless");
        BadgeGlyph = needsTrust ? "\uE7BA" : device.UsbConnected ? "\uE73E" : "\uE701";
        var color = needsTrust ? Microsoft.UI.Colors.Orange :
            device.UsbConnected ? Microsoft.UI.Colors.LimeGreen : Microsoft.UI.Colors.DodgerBlue;
        AccentBrush = new SolidColorBrush(color);
        BadgeBackground = new SolidColorBrush(color) { Opacity = 0.14 };
        Key = $"{device.Udid}|{device.Name}|{device.Model}|{device.UsbConnected}|{device.UsbTrusted}";
    }
}

public sealed partial class HomePage : Page
{
    private readonly CompanionRuntime _runtime = CompanionRuntime.Shared;
    private ConnectedDevice? _usbDevice;
    private string? _selectedUdid;

    public HomePage()
    {
        InitializeComponent();
        SubtitleText.Text = UiText.Get("win_subtitle");
        LegacyMobileNotice.Title = UiText.Get("win_mobile_update_needed");
        LegacyMobileNotice.Message = UiText.Get("win_mobile_update_detail");
        LegacyMobileLink.Content = UiText.Get("win_mobile_update_link");
        FirstConnectionTitle.Text = UiText.Get("win_intro_title");
        FirstConnectionDescription.Text = UiText.Get("win_intro_detail");
        StatusTitle.Text = UiText.Get("win_status_title");
        RefreshButton.Content = UiText.Get("mt_015");
        CollectButton.Content = UiText.Get("win_collect_now");
        SetupTitle.Text = UiText.Get("win_setup_title");
        SetupStepsTitle.Text = UiText.Get("win_setup_steps_title");
        SetupRequirement.Text = UiText.Get("win_setup_requirement");
        AppleDevicesLink.Content = UiText.Get("win_get_apple_devices");
        ITunesLink.Content = UiText.Get("win_get_itunes");
        SoftwareRefreshButton.Content = UiText.Get("win_check_apple_software");
        SoftwareStatus.Text = UiText.Get("win_software_checking");
        ICloudNote.Text = UiText.Get("win_icloud_note");
        SetupStep1.Text = UiText.Get("win_step_1");
        SetupStep2.Text = UiText.Get("win_step_2");
        SetupStep3.Text = UiText.Get("win_step_3");
        UsbButton.Content = UiText.Get("win_usb");
        RepairUsbButton.Content = UiText.Get("win_usb_repair");
        DeviceHeading.Text = UiText.Get("win_found");
        DeviceSelectionHint.Text = UiText.Get("win_select_device_hint");
        DeviceEmptyText.Text = UiText.Get("win_none_found");
        DeviceList.SelectionChanged += (_, _) => {
            if (DeviceList.SelectedIndex is >= 0 and var index &&
                index < _runtime.Available.Count)
                _selectedUdid = _runtime.Available[index].Udid;
            PairButton.IsEnabled = SelectedDevice() is not null || _usbDevice is not null;
        };
        PairButton.Content = UiText.Get("win_pair");
        PairedTitle.Text = UiText.Get("win_paired");
        UnpairButton.Content = UiText.Get("win_unpair_button");
        ProcessingNote.Text = UiText.Get("win_note");
        ManualAddressHint.Text = UiText.Get("win_manual_device_ip");
        SaveManualAddressButton.Content = UiText.Get("win_save_ip");
        ClearManualAddressButton.Content = UiText.Get("win_clear_ip");
        PairedList.SelectionChanged += (_, _) => {
            if (PairedList.SelectedIndex is >= 0 and var index &&
                index < _runtime.State.Phones.Count)
                ManualAddressBox.Text = _runtime.State.Phones[index].ManualAddress ?? "";
            UnpairButton.IsEnabled = PairedList.SelectedIndex >= 0 &&
                PairedList.SelectedIndex < _runtime.State.Phones.Count;
        };
        Loaded += (_, _) => { _runtime.Changed += RuntimeChanged; Render(); _ = CheckAppleSoftwareAsync(); };
        Unloaded += (_, _) => { _runtime.Changed -= RuntimeChanged; };
    }

    private void RuntimeChanged() => DispatcherQueue.TryEnqueue(Render);

    private async void LegacyMobileClicked(object sender, RoutedEventArgs args) =>
        await Windows.System.Launcher.LaunchUriAsync(
            new Uri("https://apps.apple.com/app/mochilog/id6756904240"));

    private void ContentViewportSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var viewport = (Application.Current as MochiLog_Windows.App)?.MainWindow?.ContentViewportWidth
            ?? args.NewSize.Width;
        ContentColumn.Width = Math.Min(1500, Math.Max(480, viewport - 32));
        var usable = ContentColumn.Width - 72;
        DashboardGrid.Width = usable;
        StatusCard.Width = usable;
        FirstConnectionCard.Width = usable;
        var showIllustration = usable >= 640;
        FirstIllustrationColumn.Width = new GridLength(showIllustration ? 250 : 0);
        FirstConnectionIllustration.Visibility = showIllustration
            ? Visibility.Visible : Visibility.Collapsed;

        var wide = usable >= 1120;
        var left = wide ? Math.Round((usable - 20) * 0.53) : usable;
        var right = wide ? usable - 20 - left : 0;
        MainColumn.Width = new GridLength(left);
        SideColumn.Width = new GridLength(right);
        PairedCard.Width = left;
        DeviceCard.Width = wide ? right : usable;
        GuideCard.Width = left;
        StepsCard.Width = wide ? right : usable;
        Grid.SetColumn(PairedCard, 0);
        Grid.SetRow(PairedCard, 0);
        Grid.SetColumn(DeviceCard, wide ? 1 : 0);
        Grid.SetRow(DeviceCard, wide ? 0 : 1);
        Grid.SetColumn(GuideCard, 0);
        Grid.SetRow(GuideCard, wide ? 1 : 2);
        Grid.SetColumn(StepsCard, wide ? 1 : 0);
        Grid.SetRow(StepsCard, wide ? 1 : 3);
        Grid.SetColumn(StatusActions, wide ? 2 : 1);
        Grid.SetRow(StatusActions, wide ? 0 : 1);
    }

    private async Task CheckAppleSoftwareAsync()
    {
        SoftwareRefreshButton.IsEnabled = false;
        SoftwareStatus.Text = UiText.Get("win_software_checking");
        try
        {
            var installed = await Task.Run(AppleDeviceSoftwareDetector.Detect);
            SoftwareStatus.Text = UiText.Get(installed switch
            {
                { AppleDevices: true, ClassicITunes: true } => "win_software_both",
                { AppleDevices: true } => "win_software_apple_devices",
                { ClassicITunes: true } => "win_software_itunes",
                { AppleDevicesCheckFailed: true } => "win_software_unknown",
                _ => "win_software_missing"
            });
            var needed = !installed.AppleDevices && !installed.ClassicITunes;
            SoftwareInfoBar.Severity = needed ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
            AppleDevicesLink.Visibility = needed ? Visibility.Visible : Visibility.Collapsed;
            ITunesLink.Visibility = needed ? Visibility.Visible : Visibility.Collapsed;
            ICloudNote.Visibility = needed ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { SoftwareRefreshButton.IsEnabled = true; }
    }

    private async void SoftwareRefreshClicked(object sender, RoutedEventArgs args) =>
        await CheckAppleSoftwareAsync();

    private void Render()
    {
        LegacyMobileNotice.IsOpen = _runtime.LegacyPhoneIds.Count > 0;
        FirstConnectionCard.Visibility = _runtime.State.Phones.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
        var usbDevices = _runtime.Available.Where(device => device.UsbConnected).ToArray();
        var introStatusKey = usbDevices.Any(device => !device.UsbTrusted)
            ? "win_intro_trust" : usbDevices.Length > 0
                ? "win_intro_connected" : _runtime.Available.Count > 0
                    ? "win_intro_wireless" : "win_intro_waiting";
        FirstConnectionStatus.Text = UiText.Get(introStatusKey);
        FirstConnectionStatusIcon.Glyph = introStatusKey == "win_intro_waiting"
            ? "\uE946" : introStatusKey == "win_intro_trust" ? "\uE7BA" : "\uE73E";
        StatusText.Text = _runtime.Status;
        CollectionText.Text = _runtime.CollectionStatus;
        var available = _runtime.Available.Select(device => new DeviceListRow(device)).ToArray();
        DeviceList.Visibility = available.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        DeviceEmptyPanel.Visibility = available.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (DeviceList.ItemsSource is not DeviceListRow[] currentAvailable ||
            !currentAvailable.Select(row => row.Key).SequenceEqual(available.Select(row => row.Key)))
        {
            DeviceList.ItemsSource = available;
            if (available.Length == 1) _selectedUdid = _runtime.Available[0].Udid;
            var selected = _runtime.Available.ToList().FindIndex(device => device.Udid == _selectedUdid);
            if (selected >= 0) DeviceList.SelectedIndex = selected;
        }
        var pairedSelected = PairedList.SelectedIndex;
        var paired = _runtime.State.Phones.Select(phone =>
            $"{phone.Name} · {phone.Model} · " +
            (phone.ConfirmedAt is null ? UiText.Get("win_waiting_app") :
                UiText.Get("win_connected"))).ToArray();
        if (PairedList.ItemsSource is not string[] currentPaired ||
            !currentPaired.SequenceEqual(paired))
        {
            PairedList.ItemsSource = paired;
            if (pairedSelected >= 0 && pairedSelected < paired.Length)
                PairedList.SelectedIndex = pairedSelected;
        }
        CollectButton.IsEnabled = _runtime.State.Phones.Count > 0;
        PairButton.IsEnabled = _runtime.Available.Any(device => device.Udid == _selectedUdid) ||
            _usbDevice is not null;
        UnpairButton.IsEnabled = PairedList.SelectedIndex >= 0 &&
            PairedList.SelectedIndex < _runtime.State.Phones.Count;
    }

    private async void RefreshClicked(object sender, RoutedEventArgs args) =>
        await _runtime.RefreshAsync();
    private async void CollectClicked(object sender, RoutedEventArgs args) =>
        await _runtime.CollectAsync(manual: true);

    private async void UnpairClicked(object sender, RoutedEventArgs args)
    {
        var index = PairedList.SelectedIndex;
        if (index < 0 || index >= _runtime.State.Phones.Count) return;
        var phone = _runtime.State.Phones[index];
        var answer = await DialogCoordinator.ShowAsync(new ContentDialog {
            XamlRoot = XamlRoot,
            Title = UiText.Format("win_unpair_title", phone.Name),
            Content = UiText.Get("win_unpair_detail"),
            PrimaryButtonText = UiText.Get("win_unpair_button"),
            CloseButtonText = UiText.Get("win_close"),
            DefaultButton = ContentDialogButton.Close
        });
        if (answer != ContentDialogResult.Primary) return;
        try { _runtime.Unpair(phone); Render(); }
        catch (Exception error) { await ShowMessageAsync(UiText.Get("win_setup_failed"), error.Message); }
    }

    private async void UsbClicked(object sender, RoutedEventArgs args) =>
        await RunUsbSetupAsync(repair: false);

    private async void RepairUsbClicked(object sender, RoutedEventArgs args) =>
        await RunUsbSetupAsync(repair: true);

    private async Task RunUsbSetupAsync(bool repair)
    {
        if (!UsbButton.IsEnabled) return;
        UsbButton.IsEnabled = false;
        RepairUsbButton.IsEnabled = false;
        UsbResultBar.Title = UiText.Get("win_usb_done");
        UsbResultBar.Message = UiText.Get("win_usb_wait");
        UsbResultBar.Severity = InfoBarSeverity.Informational;
        UsbResultBar.IsOpen = true;
        UsbProgressRing.Visibility = Visibility.Visible;
        UsbProgressRing.IsActive = true;
        try
        {
            if (repair)
            {
                UsbResultBar.Message = UiText.Get("win_usb_repairing");
                await Task.Run(() => AppleUsbRecovery.RepairAsync(CancellationToken.None));
            }
            _usbDevice = await _runtime.PrepareUsbAsync(SelectedDevice()?.Udid,
                (step, message) => DispatcherQueue.TryEnqueue(() =>
                    UsbResultBar.Message = $"{step}/4 · {message} {UiText.Get("win_usb_wait_note")}"));
            RepairUsbButton.Visibility = Visibility.Collapsed;
            UsbResultBar.Message = UiText.Get("win_usb_done_detail");
            UsbResultBar.Severity = InfoBarSeverity.Success;
        }
        catch (Exception error)
        {
            UsbResultBar.Title = UiText.Get("win_setup_failed");
            UsbResultBar.Message = error.Message;
            UsbResultBar.Severity = InfoBarSeverity.Error;
            RepairUsbButton.Visibility = error is AppleUsbBridgeUnavailableException
                ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            UsbProgressRing.IsActive = false;
            UsbProgressRing.Visibility = Visibility.Collapsed;
            UsbButton.IsEnabled = true;
            RepairUsbButton.IsEnabled = true;
        }
    }

    private ConnectedDevice? SelectedDevice() => _runtime.Available.FirstOrDefault(device =>
        device.Udid == _selectedUdid);

    private async void PairClicked(object sender, RoutedEventArgs args)
    {
        var device = SelectedDevice() ?? _usbDevice;
        if (device is null) { await ShowMessageAsync(UiText.Get("win_select"), UiText.Get("win_select_pair")); return; }
        PairButton.IsEnabled = false;
        try
        {
            if (device.UsbConnected && !device.UsbTrusted)
            {
                device = await _runtime.PrepareUsbAsync(device.Udid);
                _usbDevice = device;
            }
            var invite = _runtime.BeginPairing(device);
            using var qrData = QRCodeGenerator.GenerateQrCode(invite.Url, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(qrData).GetGraphic(7);
            using var memory = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(memory))
            {
                writer.WriteBytes(png);
                await writer.StoreAsync();
                writer.DetachStream();
            }
            memory.Seek(0);
            var image = new BitmapImage();
            await image.SetSourceAsync(memory);
            var detail = new StackPanel { Spacing = 12 };
            detail.Children.Add(new TextBlock { Text = UiText.Get("win_qr_instruction"), TextWrapping = TextWrapping.Wrap });
            detail.Children.Add(new Image { Source = image, Width = 280, Height = 280 });
            detail.Children.Add(new TextBlock { Text = UiText.Format("win_code", invite.Code), FontSize = 24 });
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot, Title = UiText.Format("win_pair_title", device.Name),
                Content = detail, CloseButtonText = UiText.Get("win_close")
            };
            var confirmedHandled = false;
            void OnConfirmed(PairedPhone phone)
            {
                if (phone.Udid != device.Udid || confirmedHandled) return;
                confirmedHandled = true;
                DispatcherQueue.TryEnqueue(() =>
                {
                    dialog.Hide();
                    UsbResultBar.Title = UiText.Get("win_connected");
                    UsbResultBar.Message = UiText.Format("win_pair_confirmed", phone.Name);
                    UsbResultBar.Severity = InfoBarSeverity.Success;
                    UsbResultBar.IsOpen = true;
                    Render();
                    PairedCard.StartBringIntoView();
                });
            }
            _runtime.PairingConfirmed += OnConfirmed;
            ContentDialogResult? presented;
            try { presented = await DialogCoordinator.ShowAsync(dialog); }
            finally { _runtime.PairingConfirmed -= OnConfirmed; }
            if (presented is null)
                ShowInlineError(UiText.Get("win_pair_failed"), UiText.Get("win_setup_failed"));
        }
        catch (Exception error) { await ShowMessageAsync(UiText.Get("win_pair_failed"), error.Message); }
        finally { PairButton.IsEnabled = true; }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var presented = await DialogCoordinator.ShowAsync(new ContentDialog {
            XamlRoot = XamlRoot, Title = title, Content = message,
            CloseButtonText = UiText.Get("win_close")
        });
        if (presented is null) ShowInlineError(title, message);
    }

    private void ShowInlineError(string title, string message)
    {
        UsbResultBar.Title = title;
        UsbResultBar.Message = message;
        UsbResultBar.Severity = InfoBarSeverity.Error;
        UsbResultBar.IsOpen = true;
    }

    private void SaveManualAddressClicked(object sender, RoutedEventArgs args)
    {
        var index = PairedList.SelectedIndex;
        if (index < 0 || index >= _runtime.State.Phones.Count) return;
        try { _runtime.SetManualAddress(_runtime.State.Phones[index], ManualAddressBox.Text); }
        catch (Exception error) { _ = ShowMessageAsync(UiText.Get("win_manual_device_ip"), error.Message); }
    }

    private void ClearManualAddressClicked(object sender, RoutedEventArgs args)
    {
        var index = PairedList.SelectedIndex;
        if (index < 0 || index >= _runtime.State.Phones.Count) return;
        _runtime.SetManualAddress(_runtime.State.Phones[index], null);
        ManualAddressBox.Text = "";
    }
}
