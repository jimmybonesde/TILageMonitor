namespace TILageMonitor;

/// <summary>
/// Tracks which incident IDs were already seen as active (status 1/4) so first-load
/// and reopen do not spam toasts. Mirrors service-status seeding: seed silently on
/// first successful render, toast only newly appearing IDs later, prune resolved IDs.
/// </summary>
public static class ActiveIncidentTracker
{
    /// <summary>
    /// Synchronizes <paramref name="knownActiveIds"/> with the current active set.
    /// Returns IDs that newly appeared (candidates for toast) when not seeding.
    /// </summary>
    public static IReadOnlyList<string> Sync(
        HashSet<string> knownActiveIds,
        IEnumerable<string> currentActiveIds,
        bool seedWithoutToast)
    {
        ArgumentNullException.ThrowIfNull(knownActiveIds);
        var current = new HashSet<string>(
            currentActiveIds ?? Enumerable.Empty<string>(),
            StringComparer.Ordinal);

        if (seedWithoutToast)
        {
            foreach (var id in current)
                knownActiveIds.Add(id);
            knownActiveIds.RemoveWhere(id => !current.Contains(id));
            return Array.Empty<string>();
        }

        var newlyAppeared = new List<string>();
        foreach (var id in current)
        {
            if (knownActiveIds.Add(id))
                newlyAppeared.Add(id);
        }

        knownActiveIds.RemoveWhere(id => !current.Contains(id));
        return newlyAppeared;
    }
}
