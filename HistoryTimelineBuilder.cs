using System.Globalization;

namespace TILageMonitor;

/// <summary>
/// Compact incident/outage timeline entries for the HistoryWindow „Ereignisse“ surface.
/// Intervals are derived similarly to HistoryStore incident/outage hour overlaps.
/// </summary>
public sealed class HistoryTimelineEvent
{
    public string ServiceKey { get; }
    public string ServiceName { get; }
    public string KindLabel { get; }
    public DateTime StartLocal { get; }
    public DateTime EndLocal { get; }
    public DateTime DayDate => StartLocal.Date;

    public string Title { get; }
    public string TimeRangeText { get; }
    public string Body { get; }
    public string ClickHint { get; }
    public string? StatusCode { get; }
    public string DisplayLine { get; }

    /// <summary>True when this event is selected/highlighted (e.g. from Stunden click).</summary>
    public bool IsHighlighted { get; set; }

    public System.Windows.Media.Brush AccentBarBrush =>
        HistoryDayCell.BrushForStatus(StatusCode);

    public HistoryTimelineEvent(
        string serviceKey,
        string serviceName,
        string kindLabel,
        DateTime startLocal,
        DateTime endLocal)
    {
        ServiceKey = serviceKey;
        ServiceName = serviceName;
        KindLabel = kindLabel;
        StartLocal = startLocal;
        EndLocal = endLocal;
        StatusCode = StatusCodeFromKind(kindLabel);
        Title = LocalizationService.Translate($"{serviceName} · {kindLabel}");
        TimeRangeText = FormatRange(startLocal, endLocal);
        Body = LocalizationService.Translate(kindLabel switch
        {
            "Störung" => "Vollständige Störung im TI-Status — Stundenansicht für Details.",
            "Teilausfall" => "Einschränkung / Teilausfall — Zeitraum lokal dargestellt.",
            "Wartung" => "Geplante oder laufende Wartung im erfassten Fenster.",
            _ => "Ereignis aus dem TI-Status (lokal dargestellt)."
        });
        ClickHint = LocalizationService.Translate($"Stundenansicht öffnen · {startLocal.ToString("d", LocalizationService.DisplayCulture)}");
        DisplayLine = $"{Title} · {TimeRangeText}";
    }

    private static string? StatusCodeFromKind(string kindLabel) => kindLabel switch
    {
        "Störung" => "full",
        "Teilausfall" => "partial",
        "Wartung" => "maintenance",
        _ => "partial"
    };

    private static string FormatRange(DateTime start, DateTime end)
    {
        var culture = LocalizationService.DisplayCulture;
        var weekday = culture.DateTimeFormat.AbbreviatedDayNames[(int)start.DayOfWeek].TrimEnd('.');
        if (weekday.Length > 0)
            weekday = char.ToUpper(weekday[0], culture) + weekday[1..];

        if (start.Date == end.Date)
            return $"{weekday} {start:HH:mm}–{end:HH:mm}";

        var endWeekday = culture.DateTimeFormat.AbbreviatedDayNames[(int)end.DayOfWeek].TrimEnd('.');
        if (endWeekday.Length > 0)
            endWeekday = char.ToUpper(endWeekday[0], culture) + endWeekday[1..];
        return $"{weekday} {start.ToString("d", culture)} {start:HH:mm} – {endWeekday} {end.ToString("d", culture)} {end:HH:mm}";
    }
}

public static class HistoryTimelineBuilder
{
    private static CultureInfo UiCulture => LocalizationService.DisplayCulture;

    /// <summary>
    /// Builds recent timeline events from official incident steps and outage slots,
    /// clipped to the current 14-day local window. Newest first.
    /// </summary>
    public static List<HistoryTimelineEvent> Build(
        IncidentResponse? incidents,
        OutageResponse? outages,
        string? serviceFilterKey = null,
        int maxEvents = 40)
    {
        var windowStart = DateTime.Today.AddDays(-13);
        var windowEnd = DateTime.Today.AddDays(1);
        var events = new List<HistoryTimelineEvent>();

        if (incidents?.Data is { Count: > 0 })
        {
            foreach (var incident in incidents.Data)
            {
                var services = (incident.App ?? Enumerable.Empty<string>())
                    .Select(HistoryStore.TryMapServiceKey)
                    .Where(key => key is not null)
                    .Select(key => key!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(key => serviceFilterKey is null ||
                                  string.Equals(key, serviceFilterKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (services.Count == 0)
                    continue;

                var steps = (incident.Steps ?? Enumerable.Empty<IncidentStep>())
                    .OrderBy(step => step.Timestamp)
                    .ToList();

                string? lastKind = null;
                for (var i = 0; i < steps.Count; i++)
                {
                    var kind = KindFromIncidentStep(steps[i]);
                    if (kind is not null)
                    {
                        lastKind = kind;
                    }
                    else if (steps[i].Status == 2 && lastKind is not null)
                    {
                        kind = lastKind;
                    }
                    else
                    {
                        lastKind = null;
                        continue;
                    }

                    var startLocal = steps[i].Timestamp.ToLocalTime();
                    var endUtc = i + 1 < steps.Count
                        ? steps[i + 1].Timestamp
                        : incident.ClosedAt ?? DateTime.UtcNow;
                    var endLocal = endUtc.ToLocalTime();

                    if (endLocal <= startLocal)
                        continue;
                    if (endLocal <= windowStart || startLocal >= windowEnd)
                        continue;

                    var clippedStart = startLocal < windowStart ? windowStart : startLocal;
                    var clippedEnd = endLocal > windowEnd ? windowEnd : endLocal;
                    if (clippedEnd <= clippedStart)
                        continue;

                    foreach (var key in services)
                    {
                        var name = AppSettings.ServiceDisplayNames.TryGetValue(key, out var n) ? n : key;
                        events.Add(new HistoryTimelineEvent(key, name, kind, clippedStart, clippedEnd));
                    }
                }
            }
        }

        if (outages?.Data is { Count: > 0 })
        {
            foreach (var outage in outages.Data)
            {
                var serviceKey = HistoryStore.TryMapServiceKey(outage.Service);
                if (serviceKey is null)
                    continue;
                if (serviceFilterKey is not null &&
                    !string.Equals(serviceKey, serviceFilterKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = AppSettings.ServiceDisplayNames.TryGetValue(serviceKey, out var n) ? n : serviceKey;

                foreach (var slot in outage.Slots ?? Enumerable.Empty<OutageSlot>())
                {
                    var startLocal = slot.StartTimestamp.ToLocalTime();
                    var endLocal = (slot.EndTimestamp ?? DateTime.Now).ToLocalTime();
                    if (endLocal <= startLocal)
                        continue;
                    if (endLocal <= windowStart || startLocal >= windowEnd)
                        continue;

                    var clippedStart = startLocal < windowStart ? windowStart : startLocal;
                    var clippedEnd = endLocal > windowEnd ? windowEnd : endLocal;
                    if (clippedEnd <= clippedStart)
                        continue;

                    events.Add(new HistoryTimelineEvent(
                        serviceKey, name, "Teilausfall", clippedStart, clippedEnd));
                }
            }
        }

        return MergeAdjacentSameKind(DedupeOverlapping(events))
            .OrderByDescending(e => e.StartLocal)
            .ThenBy(e => e.ServiceName, StringComparer.Create(UiCulture, ignoreCase: true))
            .Take(Math.Max(1, maxEvents))
            .ToList();
    }

    private static string? KindFromIncidentStep(IncidentStep step)
    {
        // Align with HistoryStore.IncidentSeverity / live classifier: outage before maintenance.
        return step.Status switch
        {
            1 => "Störung",
            4 => "Teilausfall",
            _ => step.HasMaintenance ? "Wartung" : null
        };
    }

    /// <summary>
    /// Merge adjacent/overlapping intervals with the same ServiceKey + KindLabel
    /// (e.g. after status-2 carry splits one outage into many tiny cards).
    /// Prefers earliest StartLocal and extends EndLocal.
    /// </summary>
    private static List<HistoryTimelineEvent> MergeAdjacentSameKind(List<HistoryTimelineEvent> events)
    {
        var merged = new List<HistoryTimelineEvent>();
        foreach (var group in events.GroupBy(
                     e => (Service: e.ServiceKey.ToLowerInvariant(), e.KindLabel)))
        {
            HistoryTimelineEvent? current = null;
            foreach (var ev in group.OrderBy(e => e.StartLocal).ThenBy(e => e.EndLocal))
            {
                if (current is null)
                {
                    current = ev;
                    continue;
                }

                // Adjacent or overlapping: extend the open interval.
                if (ev.StartLocal <= current.EndLocal)
                {
                    var start = current.StartLocal <= ev.StartLocal ? current.StartLocal : ev.StartLocal;
                    var end = current.EndLocal >= ev.EndLocal ? current.EndLocal : ev.EndLocal;
                    current = new HistoryTimelineEvent(
                        current.ServiceKey,
                        current.ServiceName,
                        current.KindLabel,
                        start,
                        end);
                }
                else
                {
                    merged.Add(current);
                    current = ev;
                }
            }

            if (current is not null)
                merged.Add(current);
        }

        return merged;
    }

    /// <summary>
    /// Drop outage Teilausfall entries that overlap an incident event for the same service.
    /// </summary>
    private static List<HistoryTimelineEvent> DedupeOverlapping(List<HistoryTimelineEvent> events)
    {
        var kept = new List<HistoryTimelineEvent>();
        foreach (var ev in events
                     .OrderByDescending(e => HistoryStore.StatusSeverity(e.StatusCode ?? "none"))
                     .ThenByDescending(e => e.StartLocal))
        {
            var redundantOutage = string.Equals(ev.KindLabel, "Teilausfall", StringComparison.Ordinal) &&
                kept.Any(k =>
                    !string.Equals(k.KindLabel, "Teilausfall", StringComparison.Ordinal) &&
                    string.Equals(k.ServiceKey, ev.ServiceKey, StringComparison.OrdinalIgnoreCase) &&
                    k.StartLocal < ev.EndLocal && ev.StartLocal < k.EndLocal);
            if (redundantOutage)
                continue;
            kept.Add(ev);
        }

        return kept;
    }
}
