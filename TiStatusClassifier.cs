namespace TILageMonitor;

public enum TiOverallStatus
{
    Ok,
    Maintenance,
    Partial,
    Full
}

public static class TiStatusClassifier
{
    public static string Classify(AppStatus status)
    {
        var outage = status.Outage ?? "none";
        if (outage.Equals("full", StringComparison.OrdinalIgnoreCase))
            return "full";
        if (outage.Equals("partial", StringComparison.OrdinalIgnoreCase))
            return "partial";
        return status.HasMaintenance || status.HasSubComponentMaintenance
            ? "maintenance"
            : "none";
    }

    public static TiOverallStatus ClassifyOverall(IEnumerable<AppStatus> statuses)
    {
        var states = statuses.Select(Classify).ToArray();
        if (states.Contains("full", StringComparer.OrdinalIgnoreCase))
            return TiOverallStatus.Full;
        if (states.Contains("partial", StringComparer.OrdinalIgnoreCase))
            return TiOverallStatus.Partial;
        if (states.Contains("maintenance", StringComparer.OrdinalIgnoreCase))
            return TiOverallStatus.Maintenance;
        return TiOverallStatus.Ok;
    }
}
