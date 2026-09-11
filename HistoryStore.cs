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
    public string HourKey { get; set; } = ""; // yyyy-MM-dd-HH lokal

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
        var now = DateTime.Now;
        var hourKey = now.ToString("yyyy-MM-dd-HH");

        var hour = file.Hours.FirstOrDefault(h => h.HourKey == hourKey);
        if (hour is null)
        {
            hour = new HistoryHourSnapshot { HourKey = hourKey };
            file.Hours.Add(hour);
        }

        hour.Services ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in AppSettings.ServiceKeys)
        {
            AppStatus? status = null;
            if (lage.AppStatus.TryGetValue(key, out var direct))
            {
                status = direct;
            }
            else
            {
                foreach (var kv in lage.AppStatus)
                {
                    if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        status = kv.Value;
                        break;
                    }
                }
            }

            if (status is null)
            {
                if (!hour.Services.ContainsKey(key))
                    hour.Services[key] = "none";
                continue;
            }

            var current = RankStatus(GetStatusCode(status));

            if (!hour.Services.TryGetValue(key, out var previous) ||
                StatusSeverity(current) > StatusSeverity(previous))
            {
                hour.Services[key] = current;
            }
        }

        Prune(file, now.Date);
        Save(file);
        return file;
    }

    /// <summary>Alias for callers still using the old name.</summary>
    public static HistoryFile UpsertToday(LageV2 lage) => UpsertNow(lage);

    private static void Prune(HistoryFile file, DateTime todayLocal)
    {
        file.Hours ??= new List<HistoryHourSnapshot>();
        var cutoff = todayLocal.AddDays(-(KeepDays - 1)); // start of (Today - 13 days)
        file.Hours = file.Hours
            .Where(h => TryParseHourKey(h.HourKey, out var dt) && dt.Date >= cutoff)
            .OrderBy(h => h.HourKey)
            .ToList();
    }

    private static bool TryParseHourKey(string? key, out DateTime dt)
    {
        dt = default;
        if (string.IsNullOrWhiteSpace(key))
            return false;
        // yyyy-MM-dd-HH
        if (DateTime.TryParseExact(key, "yyyy-MM-dd-HH",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out dt))
            return true;
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
    /// Anzahl eindeutiger Stunden-Keys im aktuellen 14-Tage-Fenster (max. 336).
    /// </summary>
    public static int CountCoveredHours(HistoryFile file)
    {
        file.Hours ??= new List<HistoryHourSnapshot>();
        var cutoff = DateTime.Today.AddDays(-(KeepDays - 1));
        return file.Hours
            .Select(h => h.HourKey)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.Ordinal)
            .Count(k => TryParseHourKey(k, out var dt) && dt.Date >= cutoff);
    }

    public static int ExpectedHoursInWindow => KeepDays * HoursPerDay;

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

        var byHour = file.Hours
            .Where(h => !string.IsNullOrWhiteSpace(h.HourKey))
            .GroupBy(h => h.HourKey)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

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

                    if (byHour.TryGetValue(hourKey, out var snap) &&
                        snap.Services is not null)
                    {
                        status = LookupService(snap.Services, key);
                    }

                    if (status is null && hasHistoricalApiData)
                        status = "none";

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

            for (var i = 0; i < steps.Count; i++)
            {
                var severity = IncidentSeverity(steps[i]);
                if (severity is null)
                    continue;

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

    private static string? IncidentSeverity(IncidentStep step) => step.Status switch
    {
        1 => "full",
        4 => "partial",
        _ => null
    };

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
    public List<HistoryDayCell> Hours { get; }

    public HistoryDayGroup(DateTime date, List<HistoryDayCell> hours)
    {
        Date = date.Date;
        var culture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        var weekday = culture.DateTimeFormat.AbbreviatedDayNames[(int)date.DayOfWeek].TrimEnd('.');
        Label = $"{weekday} {date:dd.MM}";
        Hours = hours;
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
        var tipPrefix = $"{date:dd.MM.yyyy} {hour:D2}:00";

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

        return new HistoryDayCell(date, hour, status, $"{tipPrefix}: {label}", BrushForStatus(status));
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
            _ => null
        };

        if (resourceKey is not null &&
            System.Windows.Application.Current?.TryFindResource(resourceKey) is System.Windows.Media.Brush themed)
        {
            return themed;
        }

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
