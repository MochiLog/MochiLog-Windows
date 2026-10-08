using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MochiLog_Windows.Services;

namespace MochiLog_Windows.Pages;

public sealed partial class LiveBatteryPage : Page
{
    private readonly CompanionRuntime _runtime = CompanionRuntime.Shared;
    private readonly HashSet<Guid> _liveDetailsExpanded = [];
    private readonly HashSet<(Guid Device, string Group)> _liveGroupsExpanded = [];

    public LiveBatteryPage()
    {
        InitializeComponent();
        LiveBatteryTitle.Text = UiText.Get("live_title");
        LiveBatteryNote.Text = UiText.Get("live_note") + "\n" + UiText.Get("live_network_note");
        LiveBatteryReceive.Content = UiText.Get("live_receive");
        LiveBatterySend.Content = UiText.Get("live_send");
        Loaded += (_, _) => { _runtime.Changed += RuntimeChanged; RenderLiveBattery(); _runtime.WatchBattery(true); };
        Unloaded += (_, _) => { _runtime.WatchBattery(false); _runtime.Changed -= RuntimeChanged; };
    }

    private void RuntimeChanged() => DispatcherQueue.TryEnqueue(RenderLiveBattery);

    private void ContentViewportSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var viewport = (Application.Current as App)?.MainWindow?.ContentViewportWidth ?? args.NewSize.Width;
        ContentColumn.Width = Math.Min(1500, Math.Max(480, viewport - 32));
    }

    private async void LiveBatteryReceiveClicked(object sender, RoutedEventArgs args) =>
        await _runtime.RefreshAllBatteryAsync();
    private async void LiveBatterySendClicked(object sender, RoutedEventArgs args) =>
        await _runtime.SendBatteryNowAsync();

    private void RenderLiveBattery()
    {
        LiveBatteryCards.Children.Clear();
        if (_runtime.State.Phones.Count == 0)
            LiveBatteryCards.Children.Add(new TextBlock { Text = UiText.Get("mt_nav_no_devices"), TextWrapping = TextWrapping.Wrap });
        foreach (var phone in _runtime.State.Phones.ToArray())
        {
            _runtime.LiveBatterySnapshots.TryGetValue(phone.PhysicalDeviceId, out var snapshot);
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = phone.Name + " · " + phone.Model,
                FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var table = new Grid { ColumnSpacing = 24, RowSpacing = 12 };
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var summary = BatteryPresentation.Summary(snapshot?.Values ?? new(), snapshot?.Charging, snapshot?.Fields ?? []);
            void AddRow(int index, string label, string value, bool heading = false) {
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var left = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Opacity = heading ? 0.68 : 1 };
                var right = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = !heading,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Opacity = heading ? 0.68 : 1 };
                Grid.SetRow(left, index); Grid.SetRow(right, index); Grid.SetColumn(right, 1);
                table.Children.Add(left); table.Children.Add(right);
            }
            AddRow(0, UiText.Get("live_field"), UiText.Get("live_value"), true);
            for (var index = 0; index < summary.Length; index++)
                AddRow(index + 1, UiText.Get("live_" + summary[index].Key), summary[index].Display(UiText.Get));
            panel.Children.Add(table);
            if (snapshot is not null) panel.Children.Add(new TextBlock {
                Text = UiText.Get("live_last") + " · " + snapshot.AcquiredAt.ToLocalTime().ToString("G"), Opacity = 0.68 });
            if (snapshot is not null) {
                var details = new StackPanel { Spacing = 12 };
                details.Children.Add(new TextBlock { Text = UiText.Get("live_details_note"), TextWrapping = TextWrapping.Wrap, Opacity = 0.68 });
                var fields = BatteryPresentation.Details(snapshot.Values, snapshot.Charging, snapshot.Fields);
                if (snapshot.Fields.Length == 0) details.Children.Add(new TextBlock { Text = UiText.Get("live_details_missing"), TextWrapping = TextWrapping.Wrap });
                if (snapshot.Fields.Length > 0 && fields.Length == 0) details.Children.Add(new TextBlock { Text = UiText.Get("live_details_empty"), TextWrapping = TextWrapping.Wrap });
                foreach (var group in fields.GroupBy(f => f.Group).OrderBy(g => g.Key, StringComparer.Ordinal)) {
                    var rows = new StackPanel { Spacing = 12 };
                    foreach (var field in group) {
                        var row = new Grid { ColumnSpacing = 16 };
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        row.Children.Add(new TextBlock { Text = field.Label, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
                        var value = new TextBlock { Text = field.Kind == "boolean" ? UiText.Get(field.Value == "true" ? "live_true" : "live_false")
                            : (field.Kind == "data" ? "Base64 · " : "") + field.Value,
                            TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true,
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono") };
                        Grid.SetColumn(value, 1); row.Children.Add(value); rows.Children.Add(row);
                    }
                    var groupKey = (phone.PhysicalDeviceId, group.Key);
                    var groupExpander = new Expander { Header = (group.Key.Length == 0 ? UiText.Get("live_details_general") : group.Key) + " · " + group.Count(),
                        Content = rows, IsExpanded = _liveGroupsExpanded.Contains(groupKey),
                        HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                    groupExpander.Expanding += (_, _) => _liveGroupsExpanded.Add(groupKey);
                    groupExpander.Collapsed += (_, _) => _liveGroupsExpanded.Remove(groupKey);
                    details.Children.Add(groupExpander);
                }
                var detailExpander = new Expander { Header = UiText.Get("live_details"), Content = details,
                    IsExpanded = _liveDetailsExpanded.Contains(phone.PhysicalDeviceId),
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                detailExpander.Expanding += (_, _) => _liveDetailsExpanded.Add(phone.PhysicalDeviceId);
                detailExpander.Collapsed += (_, _) => _liveDetailsExpanded.Remove(phone.PhysicalDeviceId);
                panel.Children.Add(detailExpander);
            }
            if (_runtime.LiveBatteryFailures.ContainsKey(phone.PhysicalDeviceId))
                panel.Children.Add(new TextBlock { Text = UiText.Get("live_unavailable"), TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange) });
            LiveBatteryCards.Children.Add(new Border { Child = panel, Padding = new Thickness(16), CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.08 } });
        }
        LiveBatteryReceive.IsEnabled = _runtime.LiveBatteryBusy.IsEmpty && _runtime.State.Phones.Count > 0;
        LiveBatterySend.IsEnabled = LiveBatteryReceive.IsEnabled;
    }

}
