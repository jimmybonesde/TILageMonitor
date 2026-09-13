using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WpfButton = System.Windows.Controls.Button;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace TILageMonitor;

public partial class HistoryWindow : Window
{
    private enum ViewMode
    {
        Overview,
        Hours,
        Events
    }

    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>In-session remembered filter; default = Alle (null). Sticky after chip pick.</summary>
    private static string? s_sessionFilterKey = null;

    private readonly ObservableCollection<HistoryServiceRow> _history = new();
    private readonly ObservableCollection<HistoryServiceRow> _focusHistory = new();
    private readonly ObservableCollection<ZoomServiceRow> _zoomRows = new();
    private readonly List<HistoryServiceRow> _allRows = new();
    private List<HistoryTimelineEvent> _timelineEventsCache = new();
    private DateTime? _zoomedDate;
    private DateTime? _selectedDay;
    private string? _selectedServiceKey = s_sessionFilterKey;
    private bool _hasAnyData;
    private bool _chipsBuilt;
    private ViewMode _viewMode = ViewMode.Overview;
    private IncidentResponse? _incidents;
    private OutageResponse? _outages;
    private HistoryFile _localHistory = new();
    private readonly Dictionary<UIElement, int> _panelAnimTokens = new();

    public HistoryWindow(MainWindow owner)
    {
        InitializeComponent();
        WindowState = WindowState.Maximized;
        // Independent window: stay usable when Main is hidden to tray (no Owner).
        _ = owner;
        Owner = null;
        HistoryList.ItemsSource = _history;
        FocusHistoryList.ItemsSource = _focusHistory;
        ZoomList.ItemsSource = _zoomRows;
        FocusTimelineList.ItemsSource = Array.Empty<HistoryTimelineEvent>();
        MultiTimelineList.ItemsSource = Array.Empty<HistoryTimelineEvent>();
        EventsTimelineList.ItemsSource = Array.Empty<HistoryTimelineEvent>();
        ZoomHourAxis.ItemsSource = Enumerable.Range(0, 24).Select(h => h.ToString()).ToList();
        UpdateLegendColors();
        UpdateSegmentStyles();
        SoftenShadowsForTheme();
        Focusable = true;
        PreviewKeyDown += HistoryWindow_PreviewKeyDown;
        Loaded += (_, _) =>
        {
            BuildServiceFilterChips();
            ApplyViewMode(animated: false);
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

        _localHistory = history;
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
        UpdateKpis();

        var covered = HistoryStore.CountCoveredHours(history);
        var expected = HistoryStore.ExpectedHoursInWindow;
        CoverageHint.Text = $"14 Tage API-Verlauf · {covered} / {expected} Stunden zusätzlich lokal erfasst";

        UpdateLegendColors();
        RefreshFilterChipStyles();
        UpdateHeaderHint();
        ApplyViewMode(animated: false);

        if (_viewMode == ViewMode.Hours)
            EnsureZoomDay();

        // After layout/view switch so template DropShadows are in the visual tree
        SoftenShadowsForTheme();
    }

    private void BuildServiceFilterChips()
    {
        if (_chipsBuilt)
            return;
        _chipsBuilt = true;

        ServiceFilterPanel.Children.Clear();

        foreach (var key in AppSettings.ServiceKeys)
        {
            var name = AppSettings.ServiceDisplayNames.TryGetValue(key, out var n) ? n : key;
            ServiceFilterPanel.Children.Add(CreateFilterChip(name, key));
        }

        RefreshFilterChipStyles();
    }

    private WpfButton CreateFilterChip(string label, string serviceKey)
    {
        var button = new WpfButton
        {
            Content = label,
            Tag = serviceKey,
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
        UpdateKpis();
        UpdateHeaderHint();
        if (_viewMode == ViewMode.Hours)
            EnsureZoomDay();
        else
            ApplyViewMode(animated: false);
    }

    private void AllServices_Click(object sender, RoutedEventArgs e)
    {
        _selectedServiceKey = null;
        s_sessionFilterKey = null;
        RefreshFilterChipStyles();
        ApplyServiceFilter();
        RefreshTimeline();
        UpdateKpis();
        UpdateHeaderHint();
        if (_viewMode == ViewMode.Hours)
            EnsureZoomDay();
        else
            ApplyViewMode(animated: false);
    }

    private void RefreshFilterChipStyles()
    {
        foreach (var child in ServiceFilterPanel.Children)
        {
            if (child is not WpfButton button)
                continue;

            var key = button.Tag as string;
            var selected = !string.IsNullOrWhiteSpace(_selectedServiceKey) &&
                           string.Equals(key, _selectedServiceKey, StringComparison.OrdinalIgnoreCase);

            button.Style = selected
                ? TryFindResource("AccentButtonStyle") as Style
                : null;
            button.Opacity = 1.0;
        }

        var allSelected = _selectedServiceKey is null;
        AllServicesButton.Style = allSelected
            ? TryFindResource("AccentButtonStyle") as Style
            : null;
        AllServicesButton.Opacity = allSelected ? 1.0 : 0.82;
    }

    private void ApplyServiceFilter()
    {
        _history.Clear();
        _focusHistory.Clear();

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
            ApplyDaySelectionFlags(row);
            if (focusMode)
                _focusHistory.Add(row);
            else
                _history.Add(row);
        }

        var filteredEmpty = focusMode ? _focusHistory.Count == 0 : _history.Count == 0;

        if (!_hasAnyData)
        {
            NoHistoryBorder.Visibility = Visibility.Visible;
            FocusOverviewGrid.Visibility = Visibility.Collapsed;
            MultiOverviewPanel.Visibility = Visibility.Collapsed;
            LegendPanel.Visibility = Visibility.Collapsed;
            StableOkBorder.Visibility = Visibility.Collapsed;
            NoHistoryTitle.Text = "Noch keine Verlaufsdaten";
            NoHistorySubtitle.Text =
                "Der 14-Tage-Verlauf kombiniert gematik-API-Daten mit lokal erfassten Stunden. Sobald die App aktualisiert, füllen sich die Kacheln ruhig von selbst.";
        }
        else if (filteredEmpty && focusMode)
        {
            NoHistoryBorder.Visibility = Visibility.Visible;
            FocusOverviewGrid.Visibility = Visibility.Collapsed;
            MultiOverviewPanel.Visibility = Visibility.Collapsed;
            LegendPanel.Visibility = Visibility.Visible;
            StableOkBorder.Visibility = Visibility.Collapsed;
            NoHistoryTitle.Text = "Keine Daten für diesen Dienst";
            NoHistorySubtitle.Text =
                "Für den gewählten Dienst gibt es in diesem Zeitraum keine Einträge. „Alle Dienste“ wählen oder einen anderen Chip tippen.";
        }
        else
        {
            NoHistoryBorder.Visibility = Visibility.Collapsed;
            LegendPanel.Visibility = Visibility.Visible;
            UpdateStableOkCard();
            if (focusMode)
            {
                FocusOverviewGrid.Visibility = Visibility.Visible;
                MultiOverviewPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                FocusOverviewGrid.Visibility = Visibility.Collapsed;
                MultiOverviewPanel.Visibility = Visibility.Visible;
            }
        }
    }

    private void UpdateStableOkCard()
    {
        if (!_hasAnyData)
        {
            StableOkBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var focusRows = GetFocusRows();
        if (focusRows.Count == 0)
        {
            StableOkBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var degraded = 0;
        var knownDays = 0;
        foreach (var row in focusRows)
        {
            foreach (var day in row.Days ?? Enumerable.Empty<HistoryDayGroup>())
            {
                if (day.DayStatus is null)
                    continue;
                knownDays++;
                if (!string.Equals(day.DayStatus, "none", StringComparison.OrdinalIgnoreCase))
                    degraded++;
            }
        }

        if (knownDays > 0 && degraded == 0)
        {
            StableOkBorder.Visibility = Visibility.Visible;
            var name = FocusDisplayName();
            StableOkTitle.Text = "Stabil · Alles ruhig";
            StableOkSubtitle.Text = focusRows.Count == 1
                ? $"In den letzten 14 Tagen blieb {name} ohne Einschränkung oder Störung — ein ruhiges Bild."
                : "In den letzten 14 Tagen waren alle bekannten Tage über die Dienste hinweg ohne Einschränkung oder Störung.";
        }
        else
        {
            StableOkBorder.Visibility = Visibility.Collapsed;
        }
    }

    private List<HistoryServiceRow> GetFocusRows()
    {
        if (!string.IsNullOrWhiteSpace(_selectedServiceKey))
        {
            return _allRows
                .Where(r => string.Equals(r.ServiceKey, _selectedServiceKey, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return _allRows.ToList();
    }

    private string FocusDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(_selectedServiceKey) &&
            AppSettings.ServiceDisplayNames.TryGetValue(_selectedServiceKey, out var n))
            return n;
        return "alle Dienste";
    }

    private void RefreshTimeline()
    {
        if (!_hasAnyData)
        {
            _timelineEventsCache = new List<HistoryTimelineEvent>();
            FocusTimelineList.ItemsSource = _timelineEventsCache;
            MultiTimelineList.ItemsSource = new List<HistoryTimelineEvent>();
            EventsTimelineList.ItemsSource = new List<HistoryTimelineEvent>();
            FocusTimelineEmpty.Visibility = Visibility.Collapsed;
            MultiTimelineEmpty.Visibility = Visibility.Collapsed;
            EventsEmptyCard.Visibility = Visibility.Collapsed;
            return;
        }

        _timelineEventsCache = HistoryTimelineBuilder.Build(_incidents, _outages, _selectedServiceKey);
        // Separate list instances so each ItemsControl has its own binding source
        FocusTimelineList.ItemsSource = _timelineEventsCache.ToList();
        MultiTimelineList.ItemsSource = _timelineEventsCache.ToList();
        EventsTimelineList.ItemsSource = _timelineEventsCache.ToList();

        var empty = _timelineEventsCache.Count == 0;
        FocusTimelineEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        MultiTimelineEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        FocusTimelineList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        MultiTimelineList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        EventsTimelineList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        EventsEmptyCard.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateKpis()
    {
        var rows = GetFocusRows();
        string? todayWorst = null;
        var degradedDays = 0;
        var knownHours = 0;
        var okHours = 0;
        var today = DateTime.Today;

        foreach (var row in rows)
        {
            foreach (var day in row.Days ?? Enumerable.Empty<HistoryDayGroup>())
            {
                if (day.DayStatus is not null &&
                    !string.Equals(day.DayStatus, "none", StringComparison.OrdinalIgnoreCase))
                {
                    degradedDays++;
                }

                if (day.Date.Date == today && day.DayStatus is not null)
                {
                    if (todayWorst is null ||
                        HistoryStore.StatusSeverity(day.DayStatus) > HistoryStore.StatusSeverity(todayWorst))
                    {
                        todayWorst = day.DayStatus;
                    }
                }

                foreach (var hour in day.Hours ?? Enumerable.Empty<HistoryDayCell>())
                {
                    if (hour.Status is null)
                        continue;
                    knownHours++;
                    if (string.Equals(hour.Status, "none", StringComparison.OrdinalIgnoreCase))
                        okHours++;
                }
            }
        }

        // When Alle: count unique calendar days with any degraded service, not sum of rows
        if (_selectedServiceKey is null && rows.Count > 1)
        {
            degradedDays = rows
                .SelectMany(r => r.Days ?? Enumerable.Empty<HistoryDayGroup>())
                .Where(d => d.DayStatus is not null &&
                            !string.Equals(d.DayStatus, "none", StringComparison.OrdinalIgnoreCase))
                .Select(d => d.Date.Date)
                .Distinct()
                .Count();

            todayWorst = null;
            foreach (var day in rows.SelectMany(r => r.Days ?? Enumerable.Empty<HistoryDayGroup>())
                         .Where(d => d.Date.Date == today && d.DayStatus is not null))
            {
                if (todayWorst is null ||
                    HistoryStore.StatusSeverity(day.DayStatus!) > HistoryStore.StatusSeverity(todayWorst))
                {
                    todayWorst = day.DayStatus;
                }
            }
        }

        var todayLabel = todayWorst switch
        {
            "full" => "Störung",
            "partial" => "Einschränkung",
            "maintenance" => "Wartung",
            "none" => "Alles OK",
            _ => _hasAnyData ? "Keine Daten" : "Alles OK"
        };
        KpiTodayValue.Text = todayLabel;
        KpiTodayHint.Text = _selectedServiceKey is null
            ? "Schlechtester Status heute (alle Dienste)"
            : $"Schlechtester Status heute · {FocusDisplayName()}";

        ApplyKpiTodaySurface(todayWorst);

        KpiDaysValue.Text = degradedDays == 1
            ? "1 auffälliger Tag"
            : $"{degradedDays} auffällige Tage";
        KpiDaysHint.Text = "Tage mit Einschränkung, Störung oder Wartung";

        // Prefer availability from local snapshots when present; else row cells (API-fill).
        // Future hours are null from BuildRows and already skipped above.
        var (localKnown, localOk) = HistoryStore.CountLocalAvailability(_localHistory, _selectedServiceKey);
        if (localKnown > 0)
        {
            var pct = Math.Round(100.0 * localOk / localKnown, 1);
            KpiAvailValue.Text = pct.ToString("0.#", DeCulture) + " %";
            KpiAvailHint.Text = $"OK-Stunden: {localOk} von {localKnown} · nur lokal erfasste Stunden";
        }
        else if (knownHours == 0)
        {
            KpiAvailValue.Text = "—";
            KpiAvailHint.Text = "Noch keine bekannten Stunden für die Berechnung";
        }
        else
        {
            var pct = Math.Round(100.0 * okHours / knownHours, 1);
            KpiAvailValue.Text = pct.ToString("0.#", DeCulture) + " %";
            KpiAvailHint.Text = $"OK-Stunden: {okHours} von {knownHours} · inkl. API-gefüllte (ohne Zukunft)";
        }
    }

    private void ApplyKpiTodaySurface(string? status)
    {
        string surface;
        string border;
        switch (status)
        {
            case "full":
                surface = "StatusOutageSurface";
                border = "StatusOutageBorder";
                break;
            case "partial":
                surface = "StatusPartialSurface";
                border = "StatusPartialBorder";
                break;
            case "maintenance":
                surface = "StatusMaintenanceSurface";
                border = "StatusMaintenanceBorder";
                break;
            default:
                surface = "StatusOkSurface";
                border = "StatusOkBorder";
                break;
        }

        if (TryFindResource(surface) is MediaBrush sb)
            KpiTodayCard.Background = sb;
        if (TryFindResource(border) is MediaBrush bb)
            KpiTodayCard.BorderBrush = bb;
    }

    private void UpdateHeaderHint()
    {
        if (_viewMode == ViewMode.Hours)
        {
            HeaderHint.Text =
                "Stundenansicht (0–23) — Esc oder „Zurück zur Übersicht“ kehrt zur Übersicht.";
        }
        else if (_viewMode == ViewMode.Events)
        {
            HeaderHint.Text =
                "Ereignisse als Timeline — Esc kehrt zur Übersicht; Klick öffnet die Stundenansicht für den Starttag.";
        }
        else if (!string.IsNullOrWhiteSpace(_selectedServiceKey))
        {
            HeaderHint.Text =
                $"Fokus: {FocusDisplayName()} — große Tageskacheln links, Ereignisse rechts. Tag oder Ereignis öffnet Stunden.";
        }
        else
        {
            HeaderHint.Text =
                "Alle Dienste in kompakten Zeilen. Ein Dienst-Chip fokussiert die Detailansicht mit Timeline.";
        }
    }

    private void Segment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button || button.Tag is not string tag)
            return;

        var mode = tag switch
        {
            "Hours" => ViewMode.Hours,
            "Events" => ViewMode.Events,
            _ => ViewMode.Overview
        };

        _viewMode = mode;
        if (mode == ViewMode.Hours)
            EnsureZoomDay();
        else if (mode == ViewMode.Overview)
            _zoomedDate = _selectedDay;

        ApplyViewMode(animated: true);
        UpdateHeaderHint();
        UpdateSegmentStyles();
    }

    private void UpdateSegmentStyles()
    {
        StyleSegment(SegOverview, _viewMode == ViewMode.Overview);
        StyleSegment(SegHours, _viewMode == ViewMode.Hours);
        StyleSegment(SegEvents, _viewMode == ViewMode.Events);
    }

    private void StyleSegment(WpfButton button, bool selected)
    {
        if (selected)
        {
            button.Background = TryFindResource("CardBackground") as MediaBrush
                                ?? MediaBrushes.White;
            button.Foreground = TryFindResource("TextMain") as MediaBrush
                                ?? MediaBrushes.Black;
            button.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            button.Background = MediaBrushes.Transparent;
            button.Foreground = TryFindResource("TextMuted") as MediaBrush
                                ?? MediaBrushes.Gray;
            button.FontWeight = FontWeights.SemiBold;
        }
    }

    private void ApplyViewMode(bool animated)
    {
        UpdateSegmentStyles();

        var showOverview = _viewMode == ViewMode.Overview;
        var showHours = _viewMode == ViewMode.Hours;
        var showEvents = _viewMode == ViewMode.Events;

        SetPanelVisible(OverviewPanel, showOverview, animated);
        SetPanelVisible(ZoomPanel, showHours, animated);
        SetPanelVisible(EventsPanel, showEvents, animated);

        if (showHours)
            RefreshZoom();
    }

    private void SetPanelVisible(UIElement panel, bool visible, bool animated)
    {
        _panelAnimTokens.TryGetValue(panel, out var prev);
        var token = prev + 1;
        _panelAnimTokens[panel] = token;

        if (!animated)
        {
            panel.BeginAnimation(UIElement.OpacityProperty, null);
            panel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            panel.Opacity = visible ? 1.0 : 0.0;
            return;
        }

        if (visible)
        {
            panel.Visibility = Visibility.Visible;
            var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            panel.BeginAnimation(UIElement.OpacityProperty, fade);
        }
        else
        {
            var fade = new DoubleAnimation(panel.Opacity, 0.0, TimeSpan.FromMilliseconds(120));
            fade.Completed += (_, _) =>
            {
                // Ignore stale Completed from a superseded fade (panel race).
                if (!_panelAnimTokens.TryGetValue(panel, out var current) || current != token)
                    return;
                if (panel.Opacity <= 0.01)
                    panel.Visibility = Visibility.Collapsed;
            };
            panel.BeginAnimation(UIElement.OpacityProperty, fade);
        }
    }

    private void DayColumn_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HistoryDayGroup day })
        {
            // Skip flash — EnterHours fades Overview immediately (avoids flash vs zoom race).
            SelectDay(day.Date, flash: false);
            EnterHoursForDay(day.Date);
            e.Handled = true;
        }
    }

    private void TimelineEvent_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: HistoryTimelineEvent ev })
        {
            SelectDay(ev.DayDate, flash: false);
            EnterHoursForDay(ev.DayDate);
            e.Handled = true;
        }
    }

    private void EnterHoursForDay(DateTime date)
    {
        _zoomedDate = date.Date;
        _viewMode = ViewMode.Hours;
        RefreshZoom();
        ApplyViewMode(animated: true);
        UpdateHeaderHint();
        UpdateSegmentStyles();
        Focus();
    }

    private void EnsureZoomDay()
    {
        var day = _zoomedDate ?? _selectedDay ?? DateTime.Today;
        _zoomedDate = day.Date;
        SelectDay(day.Date, flash: false);
        RefreshZoom();
        ZoomHintBorder.Visibility = Visibility.Collapsed;
    }

    private void SelectDay(DateTime date, bool flash)
    {
        _selectedDay = date.Date;
        foreach (var row in _allRows)
            ApplyDaySelectionFlags(row);

        // Force ItemsControl refresh for IsSelected DataTriggers
        RefreshBoundRows();

        if (flash && OverviewPanel.Visibility == Visibility.Visible)
        {
            // brief opacity pulse on overview is enough; avoid heavy animation
            var pulse = new DoubleAnimation(1.0, 0.88, TimeSpan.FromMilliseconds(90))
            {
                AutoReverse = true
            };
            OverviewPanel.BeginAnimation(UIElement.OpacityProperty, pulse);
        }
    }

    private void ApplyDaySelectionFlags(HistoryServiceRow row)
    {
        foreach (var day in row.Days ?? Enumerable.Empty<HistoryDayGroup>())
            day.IsSelected = _selectedDay is DateTime sel && day.Date.Date == sel.Date;
    }

    private void RefreshBoundRows()
    {
        var focus = !string.IsNullOrWhiteSpace(_selectedServiceKey);
        if (focus)
        {
            var snap = _focusHistory.ToList();
            _focusHistory.Clear();
            foreach (var r in snap)
                _focusHistory.Add(r);
        }
        else
        {
            var snap = _history.ToList();
            _history.Clear();
            foreach (var r in snap)
                _history.Add(r);
        }
    }

    private void ExitZoom_Click(object sender, RoutedEventArgs e) => ExitZoom();

    private void ExitZoom()
    {
        _viewMode = ViewMode.Overview;
        ApplyViewMode(animated: true);
        UpdateHeaderHint();
        UpdateSegmentStyles();
        Focus();
    }

    private void HistoryWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape &&
            (_viewMode == ViewMode.Hours || _viewMode == ViewMode.Events))
        {
            ExitZoom();
            e.Handled = true;
        }
    }

    private void RefreshZoom()
    {
        if (_zoomedDate is not DateTime date)
        {
            ZoomHintBorder.Visibility = Visibility.Visible;
            ZoomTitle.Text = "Kein Tag gewählt";
            _zoomRows.Clear();
            return;
        }

        ZoomHintBorder.Visibility = Visibility.Collapsed;
        ZoomTitle.Text = date.ToString("dddd, dd.MM.yyyy", DeCulture);
        if (ZoomTitle.Text.Length > 0)
            ZoomTitle.Text = char.ToUpper(ZoomTitle.Text[0], DeCulture) + ZoomTitle.Text[1..];

        _zoomRows.Clear();
        var source = !string.IsNullOrWhiteSpace(_selectedServiceKey) ? _focusHistory : _history;
        // If collections empty (e.g. mid-refresh), fall back to all filtered rows
        if (source.Count == 0)
            source = new ObservableCollection<HistoryServiceRow>(GetFocusRows());

        foreach (var row in source)
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

    /// <summary>
    /// Re-apply theme-dependent chrome after Hell/Dunkel toggle without requiring a full data rebuild.
    /// </summary>
    public void ApplyThemeRefresh()
    {
        SoftenShadowsForTheme();
        RefreshFilterChipStyles();
        UpdateLegendColors();
        UpdateSegmentStyles();
        UpdateKpis();
    }

    private void SoftenShadowsForTheme()
    {
        var opacity = ThemeService.IsDark ? 0.16 : 0.10;
        // Named KPI shadow first (no NRE if missing / not yet connected)
        if (KpiShadow1 is not null)
            KpiShadow1.Opacity = opacity;

        ApplyShadowOpacityWalk(this, opacity);
    }

    private static void ApplyShadowOpacityWalk(DependencyObject? root, double opacity)
    {
        if (root is null)
            return;

        if (root is UIElement ue && ue.Effect is DropShadowEffect shadow)
        {
            // Slightly softer for compact card/timeline shadows
            shadow.Opacity = shadow.BlurRadius <= 12
                ? Math.Max(0.06, opacity - 0.04)
                : opacity;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
            ApplyShadowOpacityWalk(VisualTreeHelper.GetChild(root, i), opacity);
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
