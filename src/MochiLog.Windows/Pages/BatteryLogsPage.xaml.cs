using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MochiLog_Windows.Services;
using Windows.Storage.Pickers;

namespace MochiLog_Windows.Pages;

public sealed class BatteryLogRow
{
    public StoredBatteryLog Log { get; }
    public string DeviceName => Log.DeviceName;
    public string Name => Log.Name;
    public string KindAndDay => (Log.Kind == "Watch" ? "Apple Watch" : "iPhone / iPad") +
        " · " + Log.LogDay;
    public string SizeText => Log.Size >= 1_000_000
        ? $"{Log.Size / 1_000_000.0:F1} MB" : $"{Log.Size / 1_000.0:F0} KB";
    public string Status => UiText.Get(Log.Pending ? "mt_battery_pending" : "mt_battery_saved");
    public BatteryLogRow(StoredBatteryLog log) => Log = log;
}

public sealed partial class BatteryLogsPage : Page
{
    private readonly CompanionRuntime _runtime = CompanionRuntime.Shared;
    private List<BatteryLogRow> _all = [];
    private bool _ready;

    public BatteryLogsPage()
    {
        InitializeComponent();
        PageTitle.Text = UiText.Get("mt_battery_logs");
        PageIntro.Text = UiText.Get("mt_battery_pending_hint");
        StorageTitle.Text = UiText.Get("mt_battery_storage_policy");
        KeepToggle.Header = UiText.Get("mt_battery_keep_after_send");
        KeepHint.Text = UiText.Get("mt_battery_keep_hint");
        LimitBox.Header = UiText.Get("mt_battery_limit") + " (MB)";
        MonthsBox.Header = UiText.Get("mt_battery_months");
        SearchBox.PlaceholderText = UiText.Get("mt_battery_search");
        RefreshButton.Content = UiText.Get("mt_015");
        ExportButton.Content = UiText.Get("mt_battery_export");
        ResendButton.Content = UiText.Get("mt_battery_resend");
        DeleteButton.Content = UiText.Get("mt_battery_delete");
        PendingHint.Text = UiText.Get("mt_battery_pending_hint");
        var settings = BatteryLogStorage.Settings;
        KeepToggle.IsOn = settings.KeepAfterDelivery;
        LimitBox.Value = settings.LimitMB;
        MonthsBox.Value = settings.RetentionMonths;
        _ready = true;
        Loaded += (_, _) => Refresh();
        SelectionChanged(null!, null!);
    }

    private void SettingsChanged(object sender, RoutedEventArgs args) => SaveSettings();
    private void NumberSettingsChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) =>
        SaveSettings();

    private void SaveSettings()
    {
        if (!_ready || double.IsNaN(LimitBox.Value) || double.IsNaN(MonthsBox.Value)) return;
        try
        {
            BatteryLogStorage.UpdateSettings(KeepToggle.IsOn, (int)LimitBox.Value,
                (int)MonthsBox.Value);
            Refresh();
        }
        catch (Exception error) { NoticeText.Text = error.Message; }
    }

    private void Refresh()
    {
        try
        {
            BatteryLogStorage.Prune();
            _all = BatteryLogStorage.List(_runtime.State.Phones).Select(log => new BatteryLogRow(log)).ToList();
            ApplySearch();
            var archived = _all.Where(row => !row.Log.Pending).Sum(row => row.Log.Size);
            var pending = _all.Where(row => row.Log.Pending).Sum(row => row.Log.Size);
            UsageText.Text = $"{UiText.Get("mt_battery_archive_usage")}: " +
                $"{archived / 1_000_000.0:F1} / {BatteryLogStorage.Settings.LimitMB} MB  ·  " +
                $"{UiText.Get("mt_battery_pending_usage")}: {pending / 1_000_000.0:F1} MB";
        }
        catch (Exception error) { NoticeText.Text = error.Message; }
    }

    private void ApplySearch()
    {
        var query = SearchBox.Text?.Trim() ?? "";
        LogsList.ItemsSource = string.IsNullOrEmpty(query) ? _all : _all.Where(row =>
            row.DeviceName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            row.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            row.KindAndDay.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        SelectionChanged(null!, null!);
    }

    private StoredBatteryLog[] Selected() =>
        LogsList.SelectedItems.Cast<BatteryLogRow>().Select(row => row.Log).ToArray();

    private void SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        var selected = Selected();
        ExportButton.IsEnabled = selected.Length > 0;
        ResendButton.IsEnabled = selected.Any(row => !row.Pending &&
            _runtime.State.Phones.Any(phone => phone.PhysicalDeviceId == row.DeviceId));
        DeleteButton.IsEnabled = selected.Any(row => !row.Pending);
    }

    private void SearchChanged(object sender, TextChangedEventArgs args) => ApplySearch();
    private void RefreshClicked(object sender, RoutedEventArgs args) => Refresh();

    private async void ExportClicked(object sender, RoutedEventArgs args)
    {
        var selected = Selected();
        if (selected.Length == 0) return;
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            var window = ((App)Microsoft.UI.Xaml.Application.Current).MainWindow;
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(window));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return;
            BatteryLogStorage.Export(selected, folder.Path);
            NoticeText.Text = $"{selected.Length} {UiText.Get("mt_battery_exported")}";
        }
        catch (Exception error) { NoticeText.Text = error.Message; }
    }

    private void ResendClicked(object sender, RoutedEventArgs args)
    {
        try
        {
            var count = BatteryLogStorage.Requeue(Selected(), _runtime.State.Phones);
            if (count > 0) _runtime.Server.AnnounceQueuedFiles();
            NoticeText.Text = $"{count} {UiText.Get("mt_battery_requeued")}";
            Refresh();
        }
        catch (Exception error) { NoticeText.Text = error.Message; }
    }

    private async void DeleteClicked(object sender, RoutedEventArgs args)
    {
        var selected = Selected().Where(row => !row.Pending).ToArray();
        if (selected.Length == 0) return;
        var result = await DialogCoordinator.ShowAsync(new ContentDialog {
            XamlRoot = XamlRoot,
            Title = UiText.Get("mt_battery_delete"),
            Content = UiText.Get("mt_battery_delete_confirm"),
            PrimaryButtonText = UiText.Get("mt_battery_delete"),
            CloseButtonText = UiText.Get("win_close")
        });
        if (result != ContentDialogResult.Primary) return;
        try { BatteryLogStorage.Delete(selected); Refresh(); }
        catch (Exception error) { NoticeText.Text = error.Message; }
    }
}
