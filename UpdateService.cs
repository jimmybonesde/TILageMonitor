using System.Diagnostics;
using System.IO;
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

public sealed record UpdateDownloadResult(
    bool IsSuccess,
    string Message,
    string? LocalPath = null);

public static class UpdateService
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/jimmybonesde/TILageMonitor/releases/latest";

    private static readonly HttpClient Client = CreateClient();
    private static readonly HttpClient DownloadClient = CreateDownloadClient();

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

    /// <summary>
    /// Downloads a Setup.exe asset to a temp folder and launches it.
    /// Rejects html_url pages that are not direct .exe downloads.
    /// </summary>
    public static async Task<UpdateDownloadResult> DownloadAndLaunchAsync(
        string downloadUrl,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
            return new UpdateDownloadResult(false, "Keine Download-URL vorhanden.");

        if (!downloadUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return new UpdateDownloadResult(
                false,
                "Die Update-URL verweist nicht auf eine Setup.exe. Bitte die Release-Seite manuell öffnen.",
                null);
        }

        try
        {
            var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName) ||
                !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                fileName = "TILageMonitor-Setup.exe";
            }

            var folder = Path.Combine(Path.GetTempPath(), "TILageMonitor-Update");
            Directory.CreateDirectory(folder);
            var localPath = Path.Combine(folder, fileName);

            progress?.Report("Lade Setup herunter …");

            using var response = await DownloadClient.GetAsync(
                downloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                ct);
            response.EnsureSuccessStatusCode();

            await using (var input = await response.Content.ReadAsStreamAsync(ct))
            await using (var output = File.Create(localPath))
            {
                await input.CopyToAsync(output, ct);
            }

            progress?.Report("Starte Setup …");

            Process.Start(new ProcessStartInfo(localPath)
            {
                UseShellExecute = true
            });

            return new UpdateDownloadResult(
                true,
                "Setup wurde gestartet. Du kannst die App für die Installation schließen.",
                localPath);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UpdateDownloadResult(
                false,
                $"Download oder Start fehlgeschlagen: {ex.Message}");
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

    private static HttpClient CreateDownloadClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TILageMonitor-UpdateDownload");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/octet-stream");
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
