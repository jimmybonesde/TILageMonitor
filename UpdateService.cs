using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace TILageMonitor;

public sealed record UpdateCheckResult(
    bool IsSuccess,
    bool IsUpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string? DownloadUrl,
    string? ErrorMessage);

public static class UpdateService
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/jimmybonesde/TILageMonitor/releases/latest";

    private static readonly HttpClient Client = CreateClient();

    public static string CurrentVersion => GetCurrentVersion();

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var current = CurrentVersion;

        try
        {
            using var response = await Client.GetAsync(LatestReleaseUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var release = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var root = release.RootElement;
            var tag = root.GetProperty("tag_name").GetString();
            var latest = NormalizeVersion(tag);
            var downloadUrl = FindSetupDownload(root) ??
                              root.GetProperty("html_url").GetString();

            if (!Version.TryParse(current, out var currentVersion) ||
                !Version.TryParse(latest, out var latestVersion))
            {
                return new UpdateCheckResult(
                    false, false, current, latest, downloadUrl,
                    "Die Versionsnummer des Releases konnte nicht gelesen werden.");
            }

            return new UpdateCheckResult(
                true,
                latestVersion > currentVersion,
                current,
                latest,
                downloadUrl,
                null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                false, false, current, null, null,
                $"Update-Prüfung fehlgeschlagen: {ex.Message}");
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TILageMonitor-UpdateCheck");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static string GetCurrentVersion()
    {
        var informationalVersion = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        var version = informationalVersion?.Split('+')[0];
        return NormalizeVersion(version) ??
               Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ??
               "0.0.0";
    }

    private static string? FindSetupDownload(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets))
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name?.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase) == true)
                return asset.GetProperty("browser_download_url").GetString();
        }

        return null;
    }

    private static string? NormalizeVersion(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().TrimStart('v', 'V');
}
