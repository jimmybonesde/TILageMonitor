using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TILageMonitor;

/// <summary>
/// Snapshot der letzten erfolgreichen API-Antwort für Offline-Anzeige.
/// </summary>
public sealed class LageCacheSnapshot
{
    [JsonPropertyName("savedAt")]
    public DateTime SavedAt { get; set; }

    [JsonPropertyName("lage")]
    public LageV2 Lage { get; set; } = new();

    [JsonPropertyName("incidents")]
    public IncidentResponse Incidents { get; set; } = new();

    [JsonPropertyName("outages")]
    public OutageResponse Outages { get; set; } = new();
}

public static class CacheStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string CacheDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TILageMonitor");

    public static string CachePath =>
        Path.Combine(CacheDirectory, "last-lage.json");

    public static bool Exists => File.Exists(CachePath);

    /// <summary>
    /// Soft-fail: when a refresh gets Lage but incidents/outages failed, keep the
    /// previous last-good incidents/outages instead of poisoning the cache with empties.
    /// </summary>
    public static IncidentResponse ResolveIncidents(IncidentResponse? fresh, IncidentResponse? lastGood)
    {
        // Mirror Save: HTTP-success-but-empty-failure shells must not displace last-good.
        if (fresh is null)
            return lastGood ?? new IncidentResponse();
        if (IsEmptyFailure(fresh) && lastGood is not null && !IsEmptyFailure(lastGood))
            return lastGood;
        return fresh;
    }

    public static OutageResponse ResolveOutages(OutageResponse? fresh, OutageResponse? lastGood)
    {
        if (fresh is null)
            return lastGood ?? new OutageResponse();
        if (IsEmptyFailure(fresh) && lastGood is not null && !IsEmptyFailure(lastGood))
            return lastGood;
        return fresh;
    }

    public static void Save(LageV2 lage, IncidentResponse incidents, OutageResponse outages)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);

            // Guard: never overwrite a good incidents/outages half with an empty soft-fail shell.
            var existing = Load();
            if (existing is not null)
            {
                if (IsEmptyFailure(incidents) && !IsEmptyFailure(existing.Incidents))
                    incidents = existing.Incidents;
                if (IsEmptyFailure(outages) && !IsEmptyFailure(existing.Outages))
                    outages = existing.Outages;
            }

            var snapshot = new LageCacheSnapshot
            {
                SavedAt = DateTime.Now,
                Lage = lage,
                Incidents = incidents,
                Outages = outages
            };
            AtomicJsonStore.Write(CachePath, snapshot, JsonOptions);
        }
        catch
        {
            // Cache ist optional
        }
    }

    /// <summary>
    /// True when the response is a soft-fail/null/empty failure shell (not trusted for seeding).
    /// </summary>
    public static bool IsEmptyFailure(IncidentResponse? response) =>
        response is null ||
        (!response.Success && (response.Data is null || response.Data.Count == 0));

    public static bool IsEmptyFailure(OutageResponse? response) =>
        response is null ||
        (!response.Success && (response.Data is null || response.Data.Count == 0));

    /// <summary>
    /// Only a successful API response is trusted for incident seeding. A response marked
    /// unsuccessful must never suppress later incident notifications, even when it carries data.
    /// </summary>
    public static bool IsTrustedIncidents(IncidentResponse? response) =>
        response?.Success == true;

    public static LageCacheSnapshot? Load()
    {
        try
        {
            if (!File.Exists(CachePath))
                return null;

            var json = File.ReadAllText(CachePath);
            var snap = JsonSerializer.Deserialize<LageCacheSnapshot>(json, JsonOptions);
            if (snap is null)
                return null;

            snap.Lage ??= new LageV2();
            snap.Lage.AppStatus ??= new Dictionary<string, AppStatus>(StringComparer.OrdinalIgnoreCase);
            snap.Lage.Cause ??= new List<Cause>();
            snap.Incidents ??= new IncidentResponse();
            snap.Incidents.Data ??= new List<Incident>();
            snap.Outages ??= new OutageResponse();
            snap.Outages.Data ??= new List<Outage>();
            return snap;
        }
        catch
        {
            return null;
        }
    }
}
