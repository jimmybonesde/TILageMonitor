using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TILageMonitor;

/// <summary>
/// Stündlicher Client-Snapshot: schlechtester Status je Dienst für eine lokale Stunde.
/// Der 14-Tage-Verlauf (14×24) baut sich auf, solange die App läuft und erfolgreich aktualisiert.
/// </summary>
public sealed class HistoryHourSnapshot
{
    [JsonPropertyName("hourKey")]
    public string HourKey { get; set; } = ""; // yyyy-MM-dd-HHzzz (offset-aware); legacy yyyy-MM-dd-HH is accepted

    [JsonPropertyName("services")]
    public Dictionary<string, string> Services { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HistoryFile
{
    [JsonPropertyName("hours")]
    public List<HistoryHourSnapshot> Hours { get; set; } = new();

    /// <summary>Legacy daily format – only present when loading old files; not written.</summary>
    [JsonPropertyName("days")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LegacyHistoryDaySnapshot>? Days { get; set; }
}

/// <summary>Old daily snapshot shape for light migration.</summary>
public sealed class LegacyHistoryDaySnapshot
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("services")]
    public Dictionary<string, string> Services { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class HistoryStore
{
    private const int KeepDays = 14;
    private const int HoursPerDay = 24;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string HistoryDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TILageMonitor");

    public static string HistoryPath =>
        Path.Combine(HistoryDirectory, "history.json");

    public static HistoryFile Load()
    {
        try
        {
            if (!File.Exists(HistoryPath))
                return new HistoryFile();

            var json = File.ReadAllText(HistoryPath);
            var file = JsonSerializer.Deserialize<HistoryFile>(json, JsonOptions)
                       ?? new HistoryFile();
            file.Hours ??= new List<HistoryHourSnapshot>();

            // Migrate old daily format: map each day status into hour 12
            var migrated = false;
            if (file.Hours.Count == 0 && file.Days is { Count: > 0 })
            {
                foreach (var day in file.Days)
                {
                    if (string.IsNullOrWhiteSpace(day.Date))
                        continue;
                    file.Hours.Add(new HistoryHourSnapshot
                    {
                        HourKey = $"{day.Date}-12",
                        Services = day.Services ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    });
                }
                migrated = file.Hours.Count > 0;
            }

            file.Days = null; // drop legacy after load/migrate
            if (migrated)
                Save(file); // persist Hours shape so Days is not re-read forever
            return file;
        }
        catch
        {
            return new HistoryFile();
        }
    }

    public static void Save(HistoryFile file)
    {
        try
        {
            Directory.CreateDirectory(HistoryDirectory);
            file.Days = null; // never write legacy shape
            var json = JsonSerializer.Serialize(file, JsonOptions);
            var tempPath = HistoryPath + ".tmp";
            File.WriteAllText(tempPath, json);
            // Atomic replace: write temp then move so a crash mid-write cannot corrupt history.json
            File.Move(tempPath, HistoryPath, overwrite: true);
        }
        catch
        {
            // optional – ignore IO failures (disk full, permissions, etc.)
            try
            {
                var tempPath = HistoryPath + ".tmp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }

    /// <summary>
    /// Schreibt/aktualisiert den Snapshot für die aktuelle lokale Stunde mit dem
    /// schlechtesten bekannten Status je Dienst (Upsert: nur verschlechtern oder neu setzen).
    /// Behält Stunden ab Beginn von (Today − 13 Tage) 00:00.
    /// </summary>
    public static HistoryFile UpsertNow(LageV2 lage)
    {
        var file = Load();
        file.Hours ??= new List<HistoryHourSnapshot>();
        lage.AppStatus ??= new(StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.Now;
        var hourKey = GetHourKey(now);

        var hour = file.Hours.FirstOrDefault(h => h.HourKey == hourKey);
        if (hour is null)
        {
            hour = new HistoryHourSnapshot { HourKey = hourKey };
            file.Hours.Add(hour);
        }

        ApplySnapshot(hour, lage);

        Prune(file, now.LocalDateTime.Date);
        Save(file);
        return file;
    }

    /// <summary>
    /// Merges one successful API snapshot into an hour. A missing service is deliberately
    /// left unknown: absence from a partial API response must never become a green cell.
    /// </summary>
    public static void ApplySnapshot(HistoryHourSnapshot hour, LageV2 lage)
    {
        hour.Services ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        lage.AppStatus ??= new Dictionary<string, AppStatus>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in AppSettings.ServiceKeys)
        {
            var status = lage.AppStatus.FirstOrDefault(kv =>
                string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
            if (status is null)
                continue;

            var current = RankStatus(GetStatusCode(status));
            if (!hour.Services.TryGetValue(key, out var previous) ||
                StatusSeverity(current) > StatusSeverity(previous))
            {
                hour.Services[key] = current;
            }
        }
    }

    /// <summary>Creates an offset-aware key so both repeated local DST hours remain distinct.</summary>
    public static string GetHourKey(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd-HHzzz", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Alias for callers still using the old name.</summary>
    public static HistoryFile UpsertToday(LageV2 lage) => UpsertNow(lage);

    private static void Prune(HistoryFile file, DateTime todayLocal)
    {
        file.Hours ??= new List<HistoryHourSnapshot>();
        var cutoff = todayLocal.AddDays(-(KeepDays - 1)); // start of (Today - 13 days)
        file.Hours = file.Hours
            .Where(h => TryParseHourKey(h.HourKey, out var time) && time.LocalDateTime.Date >= cutoff)
            .OrderBy(h => h.HourKey)
            .ToList();
    }

    private static bool TryParseHourKey(string? key, out DateTimeOffset time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (DateTimeOffset.TryParseExact(key, "yyyy-MM-dd-HHzzz",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out time))
            return true;

        // Legacy files did not include an offset. Preserve them as local wall-clock time.
        if (DateTime.TryParseExact(key, "yyyy-MM-dd-HH",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var legacy))
        {
            time = new DateTimeOffset(DateTime.SpecifyKind(legacy, DateTimeKind.Local));
            return true;
        }

        return false;
    }

    public static string GetStatusCode(AppStatus status)
    {
        var outage = status.Outage ?? "none";
        if (outage.Equals("full", StringComparison.OrdinalIgnoreCase))
            return "full";
        if (outage.Equals("partial", StringComparison.OrdinalIgnoreCase))
            return "partial";
        if (status.HasMaintenance || status.HasSubComponentMaintenance)
            return "maintenance";
        return "none";
    }

    private static string RankStatus(string status) => status switch
    {
        "full" => "full",
        "partial" => "partial",
        "maintenance" => "maintenance",
        _ => "none"
    };

    public static int StatusSeverity(string status) => status switch
    {
        "full" => 3,
        "partial" => 2,
        "maintenance" => 1,
        _ => 0
    };

    /// <summary>
    /// Counts known/OK hours from local snapshots only (excludes future local hours).
    /// Used by HistoryWindow availability KPI when local data exists.
    /// </summary>
    public static (int Known, int Ok) CountLocalAvailability(HistoryFile file, string? serviceKey = null)
    {
        file.Hours ??= new List<HistoryHourSnapshot>();
        var now = DateTime.Now;
        var cutoff = DateTime.Today.AddDays(-(KeepDays - 1));
        var known = 0;
        var ok = 0;

        var seenHours = new HashSet<long>();
        foreach (var hour in file.Hours)
        {
            if (!TryParseHourKey(hour.HourKey, out var hourStart) ||
                hourStart.LocalDateTime.Date < cutoff ||
                hourStart.LocalDateTime > now ||
                !seenHours.Add(hourStart.UtcDateTime.Ticks))
            {
                continue;
            }

            hour.Services ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<KeyValuePair<string, string>> entries = hour.Services;
            if (!string.IsNullOrWhiteSpace(serviceKey))
            {
                var match = LookupService(hour.Services, serviceKey);
                if (match is null)
                    continue;
                entries = [new KeyValuePair<string, string>(serviceKey, match)];
            }
            else
            {
                // Alle: count each known service cell in the snapshot
                entries = hour.Services.Where(kv =>
                    AppSettings.ServiceKeys.Contains(kv.Key, StringComparer.OrdinalIgnoreCase));
            }

            foreach (var kv in entries)
            {
                known++;
                if (string.Equals(kv.Value, "none", StringComparison.OrdinalIgnoreCase))
                    ok++;
            }
        }

        return (known, ok);
    }

    /// <summary>
    /// Anzahl eindeutiger Stunden-Keys im aktuellen 14-Tage-Fenster (max. 336).
    /// </summary>
    public static int CountCoveredHours(HistoryFile file)
    {
        file.Hours ??= new List<HistoryHourSnapshot>();
        var cutoff = DateTime.Today.AddDays(-(KeepDays - 1));
        return file.Hours
            .Select(h => h.HourKey)
            .Where(k => TryParseHourKey(k, out var time) && time.LocalDateTime.Date >= cutoff)
            .Select(k => { TryParseHourKey(k, out var time); return time.UtcDateTime.Ticks; })
            .Distinct()
            .Count();
    }

    /// <summary>Actual physical hours in the local 14-day window (335/337 around DST).</summary>
    public static int ExpectedHoursInWindow => GetExpectedHoursInWindow(DateTime.Today);

    public static int GetExpectedHoursInWindow(DateTime todayLocal)
    {
        var startLocal = DateTime.SpecifyKind(todayLocal.Date.AddDays(-(KeepDays - 1)), DateTimeKind.Local);
        var endLocal = DateTime.SpecifyKind(todayLocal.Date.AddDays(1), DateTimeKind.Local);
        var startUtc = startLocal.ToUniversalTime();
        var endUtc = endLocal.ToUniversalTime();
        return (int)(endUtc - startUtc).TotalHours;
    }

    /// <summary>
    /// Worst known status across a day's hour cells. Null hours are ignored;
    /// if every hour is unknown the day status is null (keine Daten).
    /// </summary>
    public static string? WorstStatusOfDay(IEnumerable<HistoryDayCell> hours)
    {
        string? worst = null;
        foreach (var cell in hours)
        {
            if (cell.Status is null)
                continue;
            if (worst is null || StatusSeverity(cell.Status) > StatusSeverity(worst))
                worst = cell.Status;
        }
        return worst;
    }

    /// <summary>
    /// Liefert für jeden bekannten Dienst 14 Tagesgruppen à 24 Stunden (ältester → heute).
    /// </summary>
    public static List<HistoryServiceRow> BuildRows(
        HistoryFile file,
        IncidentResponse? incidents = null,
        OutageResponse? outages = null)
    {
        file.Hours ??= new List<HistoryHourSnapshot>();
        var today = DateTime.Today;
        var dayDates = Enumerable.Range(0, KeepDays)
            .Select(i => today.AddDays(-(KeepDays - 1 - i)))
            .ToList();

        var byWallHour = file.Hours
            .Where(h => TryParseHourKey(h.HourKey, out _))
            .GroupBy(h =>
            {
                TryParseHourKey(h.HourKey, out var time);
                return time.LocalDateTime.ToString("yyyy-MM-dd-HH", System.Globalization.CultureInfo.InvariantCulture);
            })
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        // A successful incident response covers the full official 14-day window:
        // intervals without a reported restriction are shown as available (green).
        // If the API was unavailable, unknown local hours deliberately stay grey.
        var hasHistoricalApiData = incidents?.Success == true;
        var incidentByServiceHour = BuildIncidentOverlaps(incidents, dayDates);
        var outageByServiceHour = BuildOutageOverlaps(outages, dayDates);

        var rows = new List<HistoryServiceRow>();

        foreach (var key in AppSettings.ServiceKeys)
        {
            var name = AppSettings.ServiceDisplayNames.TryGetValue(key, out var n) ? n : key;
            var dayGroups = new List<HistoryDayGroup>();

            foreach (var date in dayDates)
            {
                var hours = new List<HistoryDayCell>();
                for (var h = 0; h < HoursPerDay; h++)
                {
                    var hourKey = $"{date:yyyy-MM-dd}-{h:D2}";
                    string? status = null;

                    if (byWallHour.TryGetValue(hourKey, out var snapshots))
                    {
                        status = snapshots
                            .Where(snap => snap.Services is not null)
                            .Select(snap => LookupService(snap.Services, key))
                            .Where(value => value is not null)
                            .OrderByDescending(value => StatusSeverity(value!))
                            .FirstOrDefault();
                    }

                    // Green-fill only for hours that have already started locally — future stays grey.
                    if (status is null && hasHistoricalApiData)
                    {
                        var hourStart = date.AddHours(h);
                        if (hourStart <= DateTime.Now)
                            status = "none";
                    }

                    if (incidentByServiceHour.TryGetValue((key, hourKey), out var fromIncident) &&
                        (status is null || StatusSeverity(fromIncident) > StatusSeverity(status)))
                    {
                        status = fromIncident;
                    }

                    if (outageByServiceHour.TryGetValue((key, hourKey), out var fromOutage) &&
                        (status is null || StatusSeverity(fromOutage) > StatusSeverity(status)))
                    {
                        status = fromOutage;
                    }

                    hours.Add(HistoryDayCell.From(date, h, status));
                }

                dayGroups.Add(new HistoryDayGroup(date, hours));
            }

            rows.Add(new HistoryServiceRow(name, key, dayGroups));
        }

        return rows;
    }

    private static string? LookupService(Dictionary<string, string> services, string key)
    {
        if (services.TryGetValue(key, out var s))
            return s;
        foreach (var kv in services)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }
        return null;
    }

    /// <summary>
    /// Maps the official TI-Status incident timeline onto the local 14-day hour grid.
    /// The endpoint retains incidents and their individual status transitions for 14 days.
    /// </summary>
    private static Dictionary<(string Service, string HourKey), string> BuildIncidentOverlaps(
        IncidentResponse? incidents,
        List<DateTime> dayDates)
    {
        var result = new Dictionary<(string, string), string>();
        if (incidents?.Data is null || incidents.Data.Count == 0)
            return result;

        foreach (var incident in incidents.Data)
        {
            var services = (incident.App ?? Enumerable.Empty<string>())
                .Select(MapServiceKey)
                .Where(key => key is not null)
                .Select(key => key!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (services.Count == 0)
                continue;

            var steps = (incident.Steps ?? Enumerable.Empty<IncidentStep>())
                .OrderBy(step => step.Timestamp)
                .ToList();

            // Carry last known severity across status=2 update steps so green-fill
            // does not wipe hours inside an open incident span.
            string? lastKnownSeverity = null;
            for (var i = 0; i < steps.Count; i++)
            {
                var severity = IncidentSeverity(steps[i]);
                if (severity is not null)
                {
                    lastKnownSeverity = severity;
                }
                else if (steps[i].Status == 2 && lastKnownSeverity is not null)
                {
                    severity = lastKnownSeverity;
                }
                else
                {
                    lastKnownSeverity = null;
                    continue;
                }

                var startLocal = steps[i].Timestamp.ToLocalTime();
                var endUtc = i + 1 < steps.Count
                    ? steps[i + 1].Timestamp
                    : incident.ClosedAt ?? DateTime.UtcNow;
                var endLocal = endUtc.ToLocalTime();

                if (endLocal <= startLocal)
                    continue;

                foreach (var service in services)
                    AddOverlappingHours(result, service, severity, startLocal, endLocal, dayDates);
            }
        }

        return result;
    }

    private static string? IncidentSeverity(IncidentStep step)
    {
        // Align with live classifier + HistoryTimelineBuilder: outage before maintenance.
        // Do NOT let HasMaintenance override status 1/4.
        return step.Status switch
        {
            1 => "full",
            4 => "partial",
            _ => step.HasMaintenance ? "maintenance" : null
        };
    }

    private static void AddOverlappingHours(
        Dictionary<(string Service, string HourKey), string> result,
        string service,
        string severity,
        DateTime startLocal,
        DateTime endLocal,
        List<DateTime> dayDates)
    {
        foreach (var date in dayDates)
        {
            for (var h = 0; h < HoursPerDay; h++)
            {
                var hourStart = date.AddHours(h);
                var hourEnd = hourStart.AddHours(1);
                if (hourEnd <= startLocal || hourStart >= endLocal)
                    continue;

                var hourKey = $"{date:yyyy-MM-dd}-{h:D2}";
                var key = (service, hourKey);
                if (!result.TryGetValue(key, out var previous) ||
                    StatusSeverity(severity) > StatusSeverity(previous))
                {
                    result[key] = severity;
                }
            }
        }
    }

    private static Dictionary<(string Service, string HourKey), string> BuildOutageOverlaps(
        OutageResponse? outages,
        List<DateTime> dayDates)
    {
        var result = new Dictionary<(string, string), string>();
        if (outages?.Data is null || outages.Data.Count == 0)
            return result;

        foreach (var outage in outages.Data)
        {
            var serviceKey = MapServiceKey(outage.Service);
            if (serviceKey is null)
                continue;

            foreach (var slot in outage.Slots ?? Enumerable.Empty<OutageSlot>())
            {
                var startLocal = slot.StartTimestamp.ToLocalTime();
                var endLocal = (slot.EndTimestamp ?? DateTime.Now).ToLocalTime();

                AddOverlappingHours(
                    result,
                    serviceKey,
                    "partial",
                    startLocal,
                    endLocal,
                    dayDates);
            }
        }

        return result;
    }

    /// <summary>Maps an API service label to a canonical AppSettings service key.</summary>
    public static string? TryMapServiceKey(string? service) => MapServiceKey(service);

    private static string? MapServiceKey(string? service)
    {
        if (string.IsNullOrWhiteSpace(service))
            return null;

        var normalizedService = NormalizeServiceName(service);
        foreach (var key in AppSettings.ServiceKeys)
        {
            if (string.Equals(NormalizeServiceName(key), normalizedService, StringComparison.Ordinal))
                return key;
        }

        foreach (var kv in AppSettings.ServiceDisplayNames)
        {
            if (string.Equals(NormalizeServiceName(kv.Value), normalizedService, StringComparison.Ordinal) ||
                normalizedService.Contains(NormalizeServiceName(kv.Value), StringComparison.Ordinal) ||
                normalizedService.Contains(NormalizeServiceName(kv.Key), StringComparison.Ordinal))
            {
                return kv.Key;
            }
        }

        return null;
    }

    private static string NormalizeServiceName(string value) =>
        new string(value
            .ToLowerInvariant()
            .Replace('ä', 'a')
            .Replace('ö', 'o')
            .Replace('ü', 'u')
            .Where(char.IsLetterOrDigit)
            .ToArray());
}

public sealed class HistoryServiceRow
{
    public string ServiceName { get; }
    public string ServiceKey { get; }
    /// <summary>14 day groups (oldest → today), each with 24 hour cells.</summary>
    public List<HistoryDayGroup> Days { get; }

    /// <summary>True when the history filter focuses a single service (larger tiles).</summary>
    public bool IsFocusMode { get; set; }

    public double TileMinHeight => IsFocusMode ? 84 : 60;
    public double ServiceLabelWidth => IsFocusMode ? 130 : 110;
    public double ServiceLabelFontSize => IsFocusMode ? 15 : 13;
    public System.Windows.Thickness CardPadding =>
        IsFocusMode
            ? new System.Windows.Thickness(16, 16, 16, 16)
            : new System.Windows.Thickness(14, 12, 14, 12);

    public HistoryServiceRow(string serviceName, string serviceKey, List<HistoryDayGroup> days)
    {
        ServiceName = serviceName;
        ServiceKey = serviceKey;
        Days = days;
    }
}

public sealed class HistoryDayGroup
{
    public DateTime Date { get; }
    public string Label { get; }
    public string WeekdayAbbrev { get; }
    public string DateLabel { get; }
    public List<HistoryDayCell> Hours { get; }
    public string? DayStatus { get; }
    public string DayTooltip { get; }
    public int PhysicalHourCount { get; }
    public System.Windows.Media.Brush DayBrush { get; }
    public System.Windows.Media.Brush DayForeground { get; }
    /// <summary>True when this day is the selected / zoomed day in HistoryWindow.</summary>
    public bool IsSelected { get; set; }

    public HistoryDayGroup(DateTime date, List<HistoryDayCell> hours)
    {
        Date = date.Date;
        var culture = LocalizationService.DisplayCulture;
        var weekday = culture.DateTimeFormat.AbbreviatedDayNames[(int)date.DayOfWeek].TrimEnd('.');
        if (weekday.Length > 0)
            weekday = char.ToUpper(weekday[0], culture) + weekday[1..];
        WeekdayAbbrev = weekday;
        DateLabel = date.ToString("dd.MM", culture);
        Label = $"{WeekdayAbbrev} {DateLabel}";
        Hours = hours;
        DayStatus = HistoryStore.WorstStatusOfDay(hours);
        DayBrush = HistoryDayCell.BrushForStatus(DayStatus);
        DayForeground = DayStatus is null
            ? (System.Windows.Application.Current?.TryFindResource("TextMain") as System.Windows.Media.Brush
               ?? HistoryDayCell.BrushFromHex("#0F172A"))
            : HistoryDayCell.BrushFromHex("#FFFFFF");
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(Date, DateTimeKind.Local));
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(Date.AddDays(1), DateTimeKind.Local));
        PhysicalHourCount = Math.Max(0, (int)(endUtc - startUtc).TotalHours);
        DayTooltip = BuildDayTooltip(Label, DayStatus, hours, PhysicalHourCount);
    }

    private static string BuildDayTooltip(string label, string? dayStatus, List<HistoryDayCell> hours, int physicalHourCount)
    {
        var summary = dayStatus switch
        {
            "full" => "Störung",
            "partial" => "Einschränkung",
            "maintenance" => "Wartung",
            "none" => "OK",
            _ => "keine Daten"
        };

        var known = hours.Count(h => h.Status is not null);
        var ok = hours.Count(h => string.Equals(h.Status, "none", StringComparison.OrdinalIgnoreCase));
        var partial = hours.Count(h => string.Equals(h.Status, "partial", StringComparison.OrdinalIgnoreCase));
        var full = hours.Count(h => string.Equals(h.Status, "full", StringComparison.OrdinalIgnoreCase));
        var maint = hours.Count(h => string.Equals(h.Status, "maintenance", StringComparison.OrdinalIgnoreCase));
        return LocalizationService.Translate($"{label}: {summary}\n{known}/{physicalHourCount} physische Stunden mit Daten · OK {ok} · Einschr. {partial} · Störung {full} · Wartung {maint}\nKlick öffnet die Stundenansicht.");
    }
}

public sealed class HistoryDayCell
{
    public DateTime Date { get; }
    public int Hour { get; }
    public string? Status { get; }
    public string Tooltip { get; }
    public System.Windows.Media.Brush CellBrush { get; }

    private HistoryDayCell(DateTime date, int hour, string? status, string tooltip, System.Windows.Media.Brush brush)
    {
        Date = date;
        Hour = hour;
        Status = status;
        Tooltip = tooltip;
        CellBrush = brush;
    }

    public static HistoryDayCell From(DateTime date, int hour, string? status)
    {
        var tipPrefix = $"{date.ToString("d", LocalizationService.DisplayCulture)} {hour:D2}:00";

        if (status is null)
        {
            return new HistoryDayCell(
                date,
                hour,
                null,
                $"{tipPrefix}: kein Datenpunkt",
                BrushForStatus(null));
        }

        var label = status switch
        {
            "full" => "Störung",
            "partial" => "Einschränkung",
            "maintenance" => "Wartung",
            "none" => "OK",
            _ => status
        };

        return new HistoryDayCell(date, hour, status, LocalizationService.Translate($"{tipPrefix}: {label}"), BrushForStatus(status));
    }

    /// <summary>Legend / status swatch colors matching <see cref="From"/>.</summary>
    public static string HexForStatus(string? status)
    {
        var dark = ThemeService.IsDark;
        return status switch
        {
            "full" => dark ? "#F87171" : "#B22222",
            "partial" => dark ? "#FBBF24" : "#FF8C00",
            // Distinct from partial (orange): blue for Wartung
            "maintenance" => dark ? "#38BDF8" : "#0284C8",
            "none" => dark ? "#34D399" : "#228B22",
            _ => dark ? "#64748B" : "#CBD5E1"
        };
    }

    /// <summary>Prefer live theme Status* brushes; fall back to <see cref="HexForStatus"/>.</summary>
    public static System.Windows.Media.Brush BrushForStatus(string? status)
    {
        var resourceKey = status switch
        {
            "full" => "StatusOutage",
            "partial" => "StatusPartial",
            "maintenance" => "StatusMaintenance",
            "none" => "StatusOk",
            _ => "StatusEmpty"
        };

        if (System.Windows.Application.Current?.TryFindResource(resourceKey) is System.Windows.Media.Brush themed)
            return themed;

        // Legacy fallback when StatusEmpty is absent
        if (status is null &&
            System.Windows.Application.Current?.TryFindResource("ChipBackground") is System.Windows.Media.Brush chip)
            return chip;

        return BrushFromHex(HexForStatus(status));
    }

    public static System.Windows.Media.SolidColorBrush BrushFromHex(string hex)
    {
        var color = (System.Windows.Media.Color)
            System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
        var brush = new System.Windows.Media.SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
