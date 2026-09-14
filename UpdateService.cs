using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace TILageMonitor;

public sealed record UpdateCheckResult(
    bool IsSuccess,
    bool IsUpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string? DownloadUrl,
    string? ChecksumUrl,
    string? ErrorMessage);

public sealed record UpdateDownloadResult(
    bool IsSuccess,
    string Message,
    string? LocalPath = null);

public static class UpdateService
{
    public const long MaxInstallerBytes = 500L * 1024 * 1024;
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
            // A release can be public before the build has attached its installer.
            // Do not mistake the HTML release page for an installable update.
            var downloadUrl = FindSetupDownload(root);
            var checksumUrl = FindChecksumDownload(root);

            if (!Version.TryParse(current, out var currentVersion) ||
                !Version.TryParse(latest, out var latestVersion))
            {
                return new UpdateCheckResult(
                    false, false, current, latest, downloadUrl, checksumUrl,
                    "Die Versionsnummer des Releases konnte nicht gelesen werden.");
            }

            return new UpdateCheckResult(
                true,
                latestVersion > currentVersion,
                current,
                latest,
                downloadUrl,
                checksumUrl,
                null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(
                false, false, current, null, null, null,
                $"Update-Prüfung fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Downloads a Setup.exe asset to a temp folder and launches it.
    /// Rejects html_url pages that are not direct .exe downloads.
    /// </summary>
    public static async Task<UpdateDownloadResult> DownloadAndLaunchAsync(
        string downloadUrl,
        string? checksumUrl,
        bool silent = false,
        string? updateVersion = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
            return new UpdateDownloadResult(false, "Keine Download-URL vorhanden.");

        if (!IsSetupDownloadUrl(downloadUrl))
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
            var declaredLength = response.Content.Headers.ContentLength;
            if (declaredLength is > MaxInstallerBytes)
                return new UpdateDownloadResult(false, "Der Installer ist ungewöhnlich groß und wurde aus Sicherheitsgründen abgebrochen.");

            await using (var input = await response.Content.ReadAsStreamAsync(ct))
            await using (var output = File.Create(localPath))
            {
                var buffer = new byte[80 * 1024];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                {
                    total += read;
                    if (total > MaxInstallerBytes)
                        throw new InvalidDataException("Der Installer überschreitet das Größenlimit von 500 MB.");
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }

            progress?.Report("Prüfe Setup-Datei …");
            var verification = await VerifyChecksumAsync(checksumUrl, localPath, ct);
            if (!verification.IsSuccess)
            {
                TryDelete(localPath);
                return verification;
            }

            progress?.Report(silent ? "Installiere Update …" : "Starte Setup …");

            if (silent)
            {
                QueueSilentInstallAfterCurrentProcessExits(localPath, updateVersion);
                return new UpdateDownloadResult(
                    true,
                    "Das Update wird nach dem Beenden der App automatisch installiert und anschließend gestartet.",
                    localPath);
            }

            Process.Start(new ProcessStartInfo(localPath)
            {
                UseShellExecute = true
            });

            return new UpdateDownloadResult(
                true,
                "Setup wurde gestartet.",
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

    /// <summary>
    /// Starts a hidden helper that waits for this process to end before running Inno Setup.
    /// If setup fails, the previous app is reopened with a visible error instead of leaving
    /// the user with a silently failed update.
    /// </summary>
    private static void QueueSilentInstallAfterCurrentProcessExits(string installerPath, string? updateVersion)
    {
        var applicationPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(applicationPath))
            throw new InvalidOperationException("Der Pfad der laufenden Anwendung konnte nicht bestimmt werden.");

        var helperPath = Path.Combine(
            Path.GetTempPath(),
            $"TILageMonitor-Update-{Guid.NewGuid():N}.cmd");
        var logPath = Path.Combine(SettingsStore.SettingsDirectoryPath, "update-install.log");
        var script = UpdateInstallerLauncher.BuildScript(
            Environment.ProcessId,
            installerPath,
            applicationPath,
            logPath,
            Path.Combine(Path.GetTempPath(), $"TILageMonitor-Update-Backup-{Guid.NewGuid():N}"),
            updateVersion);

        File.WriteAllText(helperPath, script);

        var launcher = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        launcher.ArgumentList.Add("/c");
        launcher.ArgumentList.Add(helperPath);

        if (Process.Start(launcher) is null)
            throw new InvalidOperationException("Der Update-Starter konnte nicht gestartet werden.");
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

    private static async Task<UpdateDownloadResult> VerifyChecksumAsync(
        string? checksumUrl,
        string localPath,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(checksumUrl))
            return new UpdateDownloadResult(false, "Die Prüfsumme des Updates fehlt.");

        try
        {
            var fileName = Path.GetFileName(localPath);
            var checksumText = await DownloadClient.GetStringAsync(checksumUrl, ct);
            var expected = checksumText
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .FirstOrDefault(parts => parts.Length >= 2 &&
                    string.Equals(parts[^1], fileName, StringComparison.OrdinalIgnoreCase))?
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(expected))
                return new UpdateDownloadResult(false, "Für den Installer wurde keine passende Prüfsumme gefunden.");

            await using var stream = File.OpenRead(localPath);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                return new UpdateDownloadResult(false, "Die Prüfsumme des Updates stimmt nicht überein.");

            var authenticode = AuthenticodeVerifier.Check(localPath);
            if (!authenticode.IsValid)
                return new UpdateDownloadResult(false, authenticode.Message);

            return new UpdateDownloadResult(true, authenticode.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new UpdateDownloadResult(false, $"Prüfsumme konnte nicht geprüft werden: {ex.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup of a failed update download.
        }
    }

    /// <summary>True only for a direct installer asset, never for a GitHub release page.</summary>
    public static bool IsSetupDownloadUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.AbsolutePath.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase);

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

    private static string? FindChecksumDownload(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets))
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            if (string.Equals(asset.GetProperty("name").GetString(), "checksums.txt", StringComparison.OrdinalIgnoreCase))
                return asset.GetProperty("browser_download_url").GetString();
        }

        return null;
    }

    private static string? NormalizeVersion(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().TrimStart('v', 'V');
}
