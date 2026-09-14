namespace TILageMonitor;

/// <summary>Prevents a broken release from being downloaded on every refresh cycle.</summary>
public static class AutoUpdateRetryPolicy
{
    public static readonly TimeSpan FailedInstallRetryDelay = TimeSpan.FromHours(6);

    public static DateTime GetNextRetryUtc(DateTime utcNow) =>
        utcNow.ToUniversalTime().Add(FailedInstallRetryDelay);

    public static bool ShouldAttempt(
        string? availableVersion,
        string? lastFailedVersion,
        DateTime retryAfterUtc,
        DateTime utcNow) =>
        !string.Equals(availableVersion, lastFailedVersion, StringComparison.OrdinalIgnoreCase) ||
        utcNow.ToUniversalTime() >= retryAfterUtc;
}
