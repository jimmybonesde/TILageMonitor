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
        Title = $"{serviceName} · {kindLabel}";
        TimeRangeText = FormatRange(startLocal, endLocal);
        Body = kindLabel switch
        {
            "Störung" => "Vollständige Störung im TI-Status — Stundenansicht für Details.",
            "Teilausfall" => "Einschränkung / Teilausfall — Zeitraum lokal dargestellt.",
            "Wartung" => "Geplante oder laufende Wartung im erfassten Fenster.",
            _ => "Ereignis aus dem TI-Status (lokal dargestellt)."
        };
        ClickHint = $"Stundenansicht öffnen · {startLocal:dd.MM.yyyy}";
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
        var culture = CultureInfo.GetCultureInfo("de-DE");
        var weekday = culture.DateTimeFormat.AbbreviatedDayNames[(int)start.DayOfWeek].TrimEnd('.');
        if (weekday.Length > 0)
            weekday = char.ToUpper(weekday[0], culture) + weekday[1..];

        if (start.Date == end.Date)
            return $"{weekday} {start:HH:mm}–{end:HH:mm}";

        var endWeekday = culture.DateTimeFormat.AbbreviatedDayNames[(int)end.DayOfWeek].TrimEnd('.');
        if (endWeekday.Length > 0)
            endWeekday = char.ToUpper(endWeekday[0], culture) + endWeekday[1..];
        return $"{weekday} {start:dd.MM. HH:mm} – {endWeekday} {end:dd.MM. HH:mm}";
    }
}

public static class HistoryTimelineBuilder
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

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

                for (var i = 0; i < steps.Count; i++)
                {
                    var kind = KindFromIncidentStep(steps[i]);
                    if (kind is null)
                        continue;

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

        return events
            .OrderByDescending(e => e.StartLocal)
            .ThenBy(e => e.ServiceName, StringComparer.Create(De, ignoreCase: true))
            .Take(Math.Max(1, maxEvents))
            .ToList();
    }

    private static string? KindFromIncidentStep(IncidentStep step)
    {
        if (step.HasMaintenance)
            return "Wartung";
        return step.Status switch
        {
            1 => "Störung",
            4 => "Teilausfall",
            _ => null
        };
    }
}
