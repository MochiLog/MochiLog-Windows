using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MochiLog_Windows.Services;
using QRCoder;
using Windows.Storage.Streams;

namespace MochiLog_Windows.Pages;

public sealed partial class HomePage : Page
{
    private readonly CompanionRuntime _runtime = CompanionRuntime.Shared;
    private ConnectedDevice? _usbDevice;

    public HomePage()
    {
        InitializeComponent();
        SubtitleText.Text = UiText.Get("win_subtitle");
        StatusTitle.Text = UiText.Get("win_status_title");
        RefreshButton.Content = UiText.Get("mt_015");
        CollectButton.Content = UiText.Get("win_collect_now");
        SetupTitle.Text = UiText.Get("win_setup_title");
        SetupStep1.Text = UiText.Get("win_step_1");
        SetupStep2.Text = UiText.Get("win_step_2");
        SetupStep3.Text = UiText.Get("win_step_3");
        UsbButton.Content = UiText.Get("win_usb");
        DeviceList.Header = UiText.Get("win_found");
        PairButton.Content = UiText.Get("win_pair");
        PairedTitle.Text = UiText.Get("win_paired");
        ProcessingNote.Text = UiText.Get("win_note");
        ManualAddressHint.Text = UiText.Get("win_manual_device_ip");
        SaveManualAddressButton.Content = UiText.Get("win_save_ip");
        ClearManualAddressButton.Content = UiText.Get("win_clear_ip");
        PairedList.SelectionChanged += (_, _) => {
            if (PairedList.SelectedIndex is >= 0 and var index &&
                index < _runtime.State.Phones.Count)
                ManualAddressBox.Text = _runtime.State.Phones[index].ManualAddress ?? "";
        };
        Loaded += (_, _) => { _runtime.Changed += RuntimeChanged; Render(); };
        Unloaded += (_, _) => _runtime.Changed -= RuntimeChanged;
    }

    private void RuntimeChanged() => DispatcherQueue.TryEnqueue(Render);

    private void Render()
    {
        StatusText.Text = _runtime.Status;
        CollectionText.Text = _runtime.CollectionStatus;
        var selected = DeviceList.SelectedIndex;
        DeviceList.ItemsSource = _runtime.Available.Select(device =>
            $"{device.Name} · {device.Model} · {device.Udid}").ToArray();
        if (selected >= 0 && selected < _runtime.Available.Count) DeviceList.SelectedIndex = selected;
        var pairedSelected = PairedList.SelectedIndex;
        PairedList.ItemsSource = _runtime.State.Phones.Select(phone =>
            $"{phone.Name} · {phone.Model} · " +
            (phone.ConfirmedAt is null ? UiText.Get("win_waiting_app") :
                UiText.Get("win_connected"))).ToArray();
        if (pairedSelected >= 0 && pairedSelected < _runtime.State.Phones.Count)
            PairedList.SelectedIndex = pairedSelected;
        CollectButton.IsEnabled = _runtime.State.Phones.Count > 0;
    }

    private async void RefreshClicked(object sender, RoutedEventArgs args) =>
        await _runtime.RefreshAsync();
    private async void CollectClicked(object sender, RoutedEventArgs args) =>
        await _runtime.CollectAsync();

    private async void UsbClicked(object sender, RoutedEventArgs args)
    {
        try
        {
            _usbDevice = await _runtime.PrepareUsbAsync();
            await ShowMessageAsync(UiText.Get("win_usb_done"), UiText.Get("win_usb_done_detail"));
        }
        catch (Exception error) { await ShowMessageAsync(UiText.Get("win_setup_failed"), error.Message); }
    }

    private ConnectedDevice? SelectedDevice() => DeviceList.SelectedIndex is >= 0 and var index &&
        index < _runtime.Available.Count ? _runtime.Available[index] : null;

    private async void PairClicked(object sender, RoutedEventArgs args)
    {
        var device = SelectedDevice() ?? _usbDevice;
        if (device is null) { await ShowMessageAsync(UiText.Get("win_select"), UiText.Get("win_select_pair")); return; }
        try
        {
            var invite = _runtime.BeginPairing(device);
            using var qrData = QRCodeGenerator.GenerateQrCode(invite.Url, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(qrData).GetGraphic(7);
            using var memory = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(memory))
            {
                writer.WriteBytes(png);
                await writer.StoreAsync();
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
