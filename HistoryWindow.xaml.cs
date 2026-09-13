using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TILageMonitor;

public partial class HistoryWindow : Window
{
    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

    private readonly ObservableCollection<HistoryServiceRow> _history = new();
    private readonly ObservableCollection<ZoomServiceRow> _zoomRows = new();
    private readonly List<HistoryServiceRow> _allRows = new();
    private DateTime? _zoomedDate;
    private string? _selectedServiceKey;
    private bool _hasAnyData;
    private bool _chipsBuilt;

    public HistoryWindow(MainWindow owner)
    {
        InitializeComponent();
        WindowState = WindowState.Maximized;
        Owner = owner;
        HistoryList.ItemsSource = _history;
        ZoomList.ItemsSource = _zoomRows;
        ZoomHourAxis.ItemsSource = Enumerable.Range(0, 24).Select(h => h.ToString()).ToList();
        UpdateLegendColors();
        Focusable = true;
        PreviewKeyDown += HistoryWindow_PreviewKeyDown;
        Loaded += (_, _) =>
        {
            BuildServiceFilterChips();
            Activate();
            Focus();
        };
    }

    public void RefreshView(
        HistoryFile history,
        IncidentResponse? incidents = null,
        OutageResponse? outages = null)
    {
        history.Hours ??= new List<HistoryHourSnapshot>();
        history.Days = null; // ignore legacy shape in UI path

        _allRows.Clear();
        _hasAnyData = history.Hours.Count > 0 ||
                      incidents?.Data?.Count > 0 ||
                      outages?.Data?.Count > 0;

        if (_hasAnyData)
        {
            var rows = HistoryStore.BuildRows(history, incidents, outages);
            _allRows.AddRange(rows);
        }

        ApplyServiceFilter();

        var covered = HistoryStore.CountCoveredHours(history);
        var expected = HistoryStore.ExpectedHoursInWindow;
        CoverageHint.Text = $"14 Tage API-Verlauf · {covered} / {expected} Stunden zusätzlich lokal erfasst";

        UpdateLegendColors();
        UpdateHeaderHint();

        if (_zoomedDate is DateTime)
            RefreshZoom();
    }

    private void BuildServiceFilterChips()
    {
        if (_chipsBuilt)
            return;
        _chipsBuilt = true;

        ServiceFilterPanel.Children.Clear();
        ServiceFilterPanel.Children.Add(CreateFilterChip("Alle", null));

        foreach (var key in AppSettings.ServiceKeys)
        {
            var name = AppSettings.ServiceDisplayNames.TryGetValue(key, out var n) ? n : key;
            ServiceFilterPanel.Children.Add(CreateFilterChip(name, key));
        }

        RefreshFilterChipStyles();
    }

    private Button CreateFilterChip(string label, string? serviceKey)
    {
        var button = new Button
        {
            Content = label,
            Tag = serviceKey ?? "",
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12,
            Cursor = Cursors.Hand
        };
        button.Click += ServiceFilterChip_Click;
        return button;
    }

    private void ServiceFilterChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var tag = button.Tag as string;
        _selectedServiceKey = string.IsNullOrWhiteSpace(tag) ? null : tag;
        RefreshFilterChipStyles();
        ApplyServiceFilter();
        if (_zoomedDate is DateTime)
            RefreshZoom();
    }

    private void RefreshFilterChipStyles()
    {
        foreach (var child in ServiceFilterPanel.Children)
        {
            if (child is not Button button)
                continue;

            var key = button.Tag as string;
            var selected = string.IsNullOrWhiteSpace(key)
                ? _selectedServiceKey is null
                : string.Equals(key, _selectedServiceKey, StringComparison.OrdinalIgnoreCase);

            button.Style = selected
                ? TryFindResource("AccentButtonStyle") as Style
                : null;
        }
    }

    private void ApplyServiceFilter()
    {
        _history.Clear();

        IEnumerable<HistoryServiceRow> rows = _allRows;
        if (!string.IsNullOrWhiteSpace(_selectedServiceKey))
        {
            rows = _allRows.Where(r =>
                string.Equals(r.ServiceKey, _selectedServiceKey, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var row in rows)
            _history.Add(row);

        var filteredEmpty = _history.Count == 0;
        var filterActive = !string.IsNullOrWhiteSpace(_selectedServiceKey);

        if (!_hasAnyData)
        {
            NoHistoryBorder.Visibility = Visibility.Visible;
            HistoryList.Visibility = Visibility.Collapsed;
            LegendPanel.Visibility = Visibility.Collapsed;
            NoHistoryTitle.Text = "Keine Verlaufsdaten";
            NoHistorySubtitle.Text =
                "Der 14-Tage-Verlauf kombiniert gematik-API-Daten mit lokal erfassten Stunden. Es gibt noch keine gespeicherten Einträge.";
        }
        else if (filteredEmpty && filterActive)
        {
            NoHistoryBorder.Visibility = Visibility.Visible;
            HistoryList.Visibility = Visibility.Collapsed;
            LegendPanel.Visibility = Visibility.Visible;
            NoHistoryTitle.Text = "Keine Daten für diesen Dienst";
            NoHistorySubtitle.Text =
                "Für den gewählten Dienst gibt es in diesem Zeitraum keine Einträge. Filter auf „Alle“ setzen oder einen anderen Dienst wählen.";
        }
        else
        {
            NoHistoryBorder.Visibility = Visibility.Collapsed;
            HistoryList.Visibility = Visibility.Visible;
            LegendPanel.Visibility = Visibility.Visible;
        }
    }

    private void UpdateHeaderHint()
    {
        if (_zoomedDate is not null)
        {
            HeaderHint.Text = "Vergrößerte Tagesansicht — Esc oder Zurück kehrt zur 14-Tage-Übersicht.";
        }
        else
        {
            HeaderHint.Text =
                "Stündliche Übersicht der letzten 14 Tage aus der gematik-API; lokale Daten ergänzen die Anzeige. Tag anklicken zum Zoomen.";
        }
    }

    private void DayColumn_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HistoryDayGroup day })
        {
            EnterZoom(day.Date);
            e.Handled = true;
        }
    }

    private void EnterZoom(DateTime date)
    {
        _zoomedDate = date.Date;
        RefreshZoom();
        OverviewPanel.Visibility = Visibility.Collapsed;
        ZoomPanel.Visibility = Visibility.Visible;
        UpdateHeaderHint();
        Focus();
    }

    private void ExitZoom_Click(object sender, RoutedEventArgs e) => ExitZoom();

    private void ExitZoom()
    {
        _zoomedDate = null;
        _zoomRows.Clear();
        OverviewPanel.Visibility = Visibility.Visible;
        ZoomPanel.Visibility = Visibility.Collapsed;
        UpdateHeaderHint();
        Focus();
    }

    private void HistoryWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _zoomedDate is not null)
        {
            ExitZoom();
            e.Handled = true;
        }
    }

    private void RefreshZoom()
    {
        if (_zoomedDate is not DateTime date)
            return;

        ZoomTitle.Text = date.ToString("dddd, dd.MM.yyyy", DeCulture);
        // Capitalize weekday if culture returns lowercase
        if (ZoomTitle.Text.Length > 0)
            ZoomTitle.Text = char.ToUpper(ZoomTitle.Text[0], DeCulture) + ZoomTitle.Text[1..];

        _zoomRows.Clear();
        foreach (var row in _history)
        {
            var day = (row.Days ?? Enumerable.Empty<HistoryDayGroup>())
                .FirstOrDefault(d => d.Date.Date == date.Date);
            if (day is null)
                continue;
            _zoomRows.Add(new ZoomServiceRow(row.ServiceName, day.Hours ?? new List<HistoryDayCell>()));
        }
    }

    private void UpdateLegendColors()
    {
        LegendOk.Background = HistoryDayCell.BrushForStatus("none");
        LegendPartial.Background = HistoryDayCell.BrushForStatus("partial");
        LegendMaintenance.Background = HistoryDayCell.BrushForStatus("maintenance");
        LegendFull.Background = HistoryDayCell.BrushForStatus("full");
        LegendEmpty.Background = HistoryDayCell.BrushForStatus(null);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

/// <summary>One service row in day-zoom mode (24 hour cells).</summary>
public sealed class ZoomServiceRow
{
    public string ServiceName { get; }
    public List<HistoryDayCell> Hours { get; }

    public ZoomServiceRow(string serviceName, List<HistoryDayCell> hours)
    {
        ServiceName = serviceName;
        Hours = hours;
    }
}
