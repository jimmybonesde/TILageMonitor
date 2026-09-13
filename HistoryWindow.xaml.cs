using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfButton = System.Windows.Controls.Button;

namespace TILageMonitor;

public partial class HistoryWindow : Window
{
    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>In-session remembered filter; default = first service (eRezept).</summary>
    private static string? s_sessionFilterKey = AppSettings.ServiceKeys[0];

    private readonly ObservableCollection<HistoryServiceRow> _history = new();
    private readonly ObservableCollection<ZoomServiceRow> _zoomRows = new();
    private readonly ObservableCollection<HistoryTimelineEvent> _timeline = new();
    private readonly List<HistoryServiceRow> _allRows = new();
    private DateTime? _zoomedDate;
    private string? _selectedServiceKey = s_sessionFilterKey;
    private bool _hasAnyData;
    private bool _chipsBuilt;
    private IncidentResponse? _incidents;
    private OutageResponse? _outages;

    public HistoryWindow(MainWindow owner)
    {
        InitializeComponent();
        WindowState = WindowState.Maximized;
        Owner = owner;
        HistoryList.ItemsSource = _history;
        ZoomList.ItemsSource = _zoomRows;
        TimelineList.ItemsSource = _timeline;
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

        _incidents = incidents;
        _outages = outages;

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
        RefreshTimeline();

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

        // Service chips first (focus mode default); „Alle“ secondary at the end
        foreach (var key in AppSettings.ServiceKeys)
        {
            var name = AppSettings.ServiceDisplayNames.TryGetValue(key, out var n) ? n : key;
            ServiceFilterPanel.Children.Add(CreateFilterChip(name, key));
        }

        ServiceFilterPanel.Children.Add(CreateFilterChip("Alle", null));
        RefreshFilterChipStyles();
    }

    private WpfButton CreateFilterChip(string label, string? serviceKey)
    {
        var button = new WpfButton
        {
            Content = label,
            Tag = serviceKey ?? "",
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += ServiceFilterChip_Click;
        return button;
    }

    private void ServiceFilterChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button)
            return;

        var tag = button.Tag as string;
        _selectedServiceKey = string.IsNullOrWhiteSpace(tag) ? null : tag;
        s_sessionFilterKey = _selectedServiceKey;
        RefreshFilterChipStyles();
        ApplyServiceFilter();
        RefreshTimeline();
        UpdateHeaderHint();
        if (_zoomedDate is DateTime)
            RefreshZoom();
    }

    private void RefreshFilterChipStyles()
    {
        foreach (var child in ServiceFilterPanel.Children)
        {
            if (child is not WpfButton button)
                continue;

            var key = button.Tag as string;
            var selected = string.IsNullOrWhiteSpace(key)
                ? _selectedServiceKey is null
                : string.Equals(key, _selectedServiceKey, StringComparison.OrdinalIgnoreCase);

            button.Style = selected
                ? TryFindResource("AccentButtonStyle") as Style
                : null;

            // „Alle“ stays visually secondary when not selected
            if (string.IsNullOrWhiteSpace(key) && !selected)
                button.Opacity = 0.78;
            else
                button.Opacity = 1.0;
        }
    }

    private void ApplyServiceFilter()
    {
        _history.Clear();

        var focusMode = !string.IsNullOrWhiteSpace(_selectedServiceKey);
        IEnumerable<HistoryServiceRow> rows = _allRows;
        if (focusMode)
        {
            rows = _allRows.Where(r =>
                string.Equals(r.ServiceKey, _selectedServiceKey, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var row in rows)
        {
            row.IsFocusMode = focusMode;
            _history.Add(row);
        }

        var filteredEmpty = _history.Count == 0;
        var filterActive = focusMode;

        if (!_hasAnyData)
        {
            NoHistoryBorder.Visibility = Visibility.Visible;
            HistoryList.Visibility = Visibility.Collapsed;
            LegendPanel.Visibility = Visibility.Collapsed;
            TimelineCard.Visibility = Visibility.Collapsed;
            NoHistoryTitle.Text = "Keine Verlaufsdaten";
            NoHistorySubtitle.Text =
                "Der 14-Tage-Verlauf kombiniert gematik-API-Daten mit lokal erfassten Stunden. Es gibt noch keine gespeicherten Einträge.";
        }
        else if (filteredEmpty && filterActive)
        {
            NoHistoryBorder.Visibility = Visibility.Visible;
            HistoryList.Visibility = Visibility.Collapsed;
            LegendPanel.Visibility = Visibility.Visible;
            TimelineCard.Visibility = Visibility.Visible;
            NoHistoryTitle.Text = "Keine Daten für diesen Dienst";
            NoHistorySubtitle.Text =
                "Für den gewählten Dienst gibt es in diesem Zeitraum keine Einträge. Filter auf „Alle“ setzen oder einen anderen Dienst wählen.";
        }
        else
        {
            NoHistoryBorder.Visibility = Visibility.Collapsed;
            HistoryList.Visibility = Visibility.Visible;
            LegendPanel.Visibility = Visibility.Visible;
            TimelineCard.Visibility = Visibility.Visible;
        }
    }

    private void RefreshTimeline()
    {
        _timeline.Clear();
        if (!_hasAnyData)
        {
            TimelineEmpty.Visibility = Visibility.Collapsed;
            return;
        }

        var events = HistoryTimelineBuilder.Build(_incidents, _outages, _selectedServiceKey);
        foreach (var ev in events)
            _timeline.Add(ev);

        TimelineEmpty.Visibility = _timeline.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TimelineList.Visibility = _timeline.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateHeaderHint()
    {
        if (_zoomedDate is not null)
        {
            HeaderHint.Text =
                "Stundenansicht (0–23) — Esc oder „Zurück zur Übersicht“ kehrt zur 14-Tage-Tagesübersicht.";
        }
        else if (!string.IsNullOrWhiteSpace(_selectedServiceKey))
        {
            var name = AppSettings.ServiceDisplayNames.TryGetValue(_selectedServiceKey, out var n)
                ? n
                : _selectedServiceKey;
            HeaderHint.Text =
                $"Fokus: {name} — Tageskacheln der letzten 14 Tage (schlechtester Status je Tag). Tag anklicken für Stundenzoom.";
        }
        else
        {
            HeaderHint.Text =
                "Tagesübersicht aller Dienste (14 Tage). Farbe = schlechtester Status des Tages. Tag anklicken für Stundenzoom.";
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

    private void TimelineEvent_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HistoryTimelineEvent ev })
        {
            EnterZoom(ev.DayDate);
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
