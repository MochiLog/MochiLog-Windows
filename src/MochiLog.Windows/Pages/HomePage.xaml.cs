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
            device.UsbConnected ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.DodgerBlue;
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
        StatusTitle.Text = UiText.Get("win_status_title");
        RefreshButton.Content = UiText.Get("mt_015");
        CollectButton.Content = UiText.Get("win_collect_now");
        SetupTitle.Text = UiText.Get("win_setup_title");
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
        DeviceList.Header = UiText.Get("win_found");
        DeviceSelectionHint.Text = UiText.Get("win_select_device_hint");
        DeviceList.SelectionChanged += (_, _) => {
            if (DeviceList.SelectedIndex is >= 0 and var index &&
                index < _runtime.Available.Count)
                _selectedUdid = _runtime.Available[index].Udid;
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
        Unloaded += (_, _) => _runtime.Changed -= RuntimeChanged;
    }

    private void RuntimeChanged() => DispatcherQueue.TryEnqueue(Render);

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
        StatusText.Text = _runtime.Status;
        CollectionText.Text = _runtime.CollectionStatus;
        var available = _runtime.Available.Select(device => new DeviceListRow(device)).ToArray();
        if (DeviceList.ItemsSource is not DeviceListRow[] currentAvailable ||
            !currentAvailable.Select(row => row.Key).SequenceEqual(available.Select(row => row.Key)))
        {
            DeviceList.ItemsSource = available;
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
        UnpairButton.IsEnabled = PairedList.SelectedIndex >= 0 &&
            PairedList.SelectedIndex < _runtime.State.Phones.Count;
    }

    private async void RefreshClicked(object sender, RoutedEventArgs args) =>
        await _runtime.RefreshAsync();
    private async void CollectClicked(object sender, RoutedEventArgs args) =>
        await _runtime.CollectAsync();

    private async void UnpairClicked(object sender, RoutedEventArgs args)
    {
        var index = PairedList.SelectedIndex;
        if (index < 0 || index >= _runtime.State.Phones.Count) return;
        var phone = _runtime.State.Phones[index];
        var answer = await new ContentDialog {
            XamlRoot = XamlRoot,
            Title = UiText.Format("win_unpair_title", phone.Name),
            Content = UiText.Get("win_unpair_detail"),
            PrimaryButtonText = UiText.Get("win_unpair_button"),
            CloseButtonText = UiText.Get("win_close"),
            DefaultButton = ContentDialogButton.Close
        }.ShowAsync();
        if (answer != ContentDialogResult.Primary) return;
        try { _runtime.Unpair(phone); Render(); }
        catch (Exception error) { await ShowMessageAsync(UiText.Get("win_setup_failed"), error.Message); }
    }

    private async void UsbClicked(object sender, RoutedEventArgs args)
    {
        try
        {
            _usbDevice = await _runtime.PrepareUsbAsync(SelectedDevice()?.Udid);
            await ShowMessageAsync(UiText.Get("win_usb_done"), UiText.Get("win_usb_done_detail"));
        }
        catch (Exception error) { await ShowMessageAsync(UiText.Get("win_setup_failed"), error.Message); }
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
            await new ContentDialog
            {
                XamlRoot = XamlRoot, Title = UiText.Format("win_pair_title", device.Name),
                Content = detail, CloseButtonText = UiText.Get("win_close")
            }.ShowAsync();
        }
        catch (Exception error) { await ShowMessageAsync(UiText.Get("win_pair_failed"), error.Message); }
        finally { PairButton.IsEnabled = true; }
    }

    private async Task ShowMessageAsync(string title, string message) =>
        await new ContentDialog { XamlRoot = XamlRoot, Title = title,
            Content = message, CloseButtonText = UiText.Get("win_close") }.ShowAsync();

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
