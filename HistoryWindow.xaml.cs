using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace TILageMonitor;

public partial class HistoryWindow : Window
{
    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

    private readonly ObservableCollection<HistoryServiceRow> _history = new();
    private readonly ObservableCollection<ZoomServiceRow> _zoomRows = new();
    private DateTime? _zoomedDate;

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

        _history.Clear();
        var hasAnyData = history.Hours.Count > 0 ||
                         incidents?.Data?.Count > 0 ||
                         outages?.Data?.Count > 0;

        NoHistoryBorder.Visibility = hasAnyData
            ? Visibility.Collapsed
            : Visibility.Visible;
        HistoryList.Visibility = hasAnyData
            ? Visibility.Visible
            : Visibility.Collapsed;
        LegendPanel.Visibility = hasAnyData
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Build heatmap only when there is data — avoid fake full grey 14×24 grid
        if (hasAnyData)
        {
            var rows = HistoryStore.BuildRows(history, incidents, outages);
            foreach (var row in rows)
                _history.Add(row);
        }

        var covered = HistoryStore.CountCoveredHours(history);
        var expected = HistoryStore.ExpectedHoursInWindow;
        CoverageHint.Text = $"{covered} / {expected} Stunden lokal erfasst · Ausfälle zusätzlich aus der gematik-API";

        UpdateLegendColors();
        UpdateHeaderHint();

        if (_zoomedDate is DateTime)
            RefreshZoom();
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
                "Stündliche Übersicht der letzten 14 Tage: lokale Verfügbarkeit plus gematik-Ausfälle aus der API. Tag anklicken zum Zoomen.";
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
