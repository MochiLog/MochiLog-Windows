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
        PairedList.ItemsSource = _runtime.State.Phones.Select(phone =>
            $"{phone.Name} · {phone.Model} · " +
            (phone.ConfirmedAt is null ? "アプリで確認待ち" : "接続済み")).ToArray();
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
            await ShowMessageAsync("USBでの信頼設定", "完了しました。ケーブルを外し、同じWi-Fiで「無線接続を確認」を押してください。");
        }
        catch (Exception error) { await ShowMessageAsync("設定できませんでした", error.Message); }
    }

    private async void VerifyClicked(object sender, RoutedEventArgs args)
    {
        var device = SelectedDevice() ?? _usbDevice;
        if (device is null)
        {
            await ShowMessageAsync("端末を選択", "USBで信頼を登録するか、検出した端末を選択してください。");
            return;
        }
        try
        {
            await _runtime.VerifyWirelessAsync(device);
            await ShowMessageAsync("無線接続を確認", "診断ログへの接続を確認しました。次にQRコードでMochiLogとペアリングしてください。");
        }
        catch (Exception error) { await ShowMessageAsync("無線接続を確認できません", error.Message); }
    }

    private ConnectedDevice? SelectedDevice() => DeviceList.SelectedIndex is >= 0 and var index &&
        index < _runtime.Available.Count ? _runtime.Available[index] : null;

    private async void PairClicked(object sender, RoutedEventArgs args)
    {
        var device = SelectedDevice() ?? _usbDevice;
        if (device is null) { await ShowMessageAsync("端末を選択", "先に端末を検出して無線接続を確認してください。"); return; }
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
            detail.Children.Add(new TextBlock { Text = "MochiLogのPC連携画面で読み取ってください。QRは3分間有効です。", TextWrapping = TextWrapping.Wrap });
            detail.Children.Add(new Image { Source = image, Width = 280, Height = 280 });
            detail.Children.Add(new TextBlock { Text = $"確認コード: {invite.Code}", FontSize = 24 });
            await new ContentDialog
            {
                XamlRoot = XamlRoot, Title = $"{device.Name} とペアリング",
                Content = detail, CloseButtonText = "閉じる"
            }.ShowAsync();
        }
        catch (Exception error) { await ShowMessageAsync("ペアリングを開始できません", error.Message); }
    }

    private async Task ShowMessageAsync(string title, string message) =>
        await new ContentDialog { XamlRoot = XamlRoot, Title = title,
            Content = message, CloseButtonText = "閉じる" }.ShowAsync();
}
