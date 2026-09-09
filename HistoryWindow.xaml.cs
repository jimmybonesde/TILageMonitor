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
        Owner = owner;
        HistoryList.ItemsSource = _history;
        ZoomList.ItemsSource = _zoomRows;
        ZoomHourAxis.ItemsSource = Enumerable.Range(0, 24).Select(h => h.ToString()).ToList();
        UpdateLegendColors();
    }

    public void RefreshView(HistoryFile history, OutageResponse? outages = null)
    {
        history.Hours ??= new List<HistoryHourSnapshot>();
        history.Days = null; // ignore legacy shape in UI path

        _history.Clear();
        var rows = HistoryStore.BuildRows(history, outages);

        var hasAnyData = history.Hours.Count > 0;
        NoHistoryBorder.Visibility = hasAnyData
            ? Visibility.Collapsed
            : Visibility.Visible;

        foreach (var row in rows)
            _history.Add(row);

        UpdateLegendColors();

        if (_zoomedDate is DateTime)
            RefreshZoom();
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
        HeaderHint.Text = "Vergrößerte Tagesansicht — Zurück kehrt zur 7-Tage-Übersicht.";
    }

    private void ExitZoom_Click(object sender, RoutedEventArgs e) => ExitZoom();

    private void ExitZoom()
    {
        _zoomedDate = null;
        _zoomRows.Clear();
        OverviewPanel.Visibility = Visibility.Visible;
        ZoomPanel.Visibility = Visibility.Collapsed;
        HeaderHint.Text =
            "Stündliche Auflösung über die letzten 7 lokalen Kalendertage (baut sich mit der Laufzeit auf). Tag anklicken zum Zoomen.";
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
        LegendOk.Background = HistoryDayCell.BrushFromHex(HistoryDayCell.HexForStatus("none"));
        LegendPartial.Background = HistoryDayCell.BrushFromHex(HistoryDayCell.HexForStatus("partial"));
        LegendFull.Background = HistoryDayCell.BrushFromHex(HistoryDayCell.HexForStatus("full"));
        LegendEmpty.Background = HistoryDayCell.BrushFromHex(HistoryDayCell.HexForStatus(null));
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
