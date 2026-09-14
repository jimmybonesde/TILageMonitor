using System.Windows;

namespace TILageMonitor;

public partial class MainWindow
{
    private string? _availableUpdateDownloadUrl;
    private string? _availableUpdateChecksumUrl;
    private string? _lastAutoInstallFailureVersion;
    private DateTime _autoInstallRetryAfterUtc;

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool userInitiated)
    {
        var result = await UpdateService.CheckAsync();

        if (!result.IsSuccess)
        {
            if (userInitiated)
            {
                System.Windows.MessageBox.Show(
                    result.ErrorMessage ?? "Die Update-Prüfung ist fehlgeschlagen.",
                    "Updates prüfen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return result;
        }

        if (!result.IsUpdateAvailable)
        {
            HideUpdateFooter();

            if (userInitiated)
            {
                System.Windows.MessageBox.Show(
                    $"TI-Lage Monitor ist aktuell (Version {result.CurrentVersion}).",
                    "Updates prüfen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return result;
        }

        var message = $"Version {result.LatestVersion} ist verfügbar. Aktuell installiert: {result.CurrentVersion}.";
        var installerAvailable = !string.IsNullOrWhiteSpace(result.DownloadUrl);

        // GitHub releases can be visible shortly before Actions attaches the Setup.exe.
        // Keep this state informative, but never offer a non-installable HTML release page.
        if (!installerAvailable)
        {
            ShowUpdateFooter(result);

            if (userInitiated)
            {
                System.Windows.MessageBox.Show(
                    $"{message}\n\nDer Installer wird gerade noch erstellt. Bitte in wenigen Minuten erneut prüfen.",
                    "Update wird vorbereitet",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                ShowReleasePreparingNotification(result);
            }

            return result;
        }

        if (!userInitiated)
        {
            if (_settings.AutoInstallUpdates &&
                AutoUpdateRetryPolicy.ShouldAttempt(
                    result.LatestVersion,
                    _lastAutoInstallFailureVersion,
                    _autoInstallRetryAfterUtc,
                    DateTime.UtcNow))
            {
                var automaticDownload = await UpdateService.DownloadAndLaunchAsync(
                    result.DownloadUrl!,
                    result.ChecksumUrl,
                    silent: true);

                if (automaticDownload.IsSuccess)
                {
                    CloseApp();
                    return result;
                }

                _lastAutoInstallFailureVersion = result.LatestVersion;
                _autoInstallRetryAfterUtc = AutoUpdateRetryPolicy.GetNextRetryUtc(DateTime.UtcNow);
                ToastService.Show(
                    "Automatisches Update fehlgeschlagen",
                    $"{automaticDownload.Message}\nNächster Versuch frühestens in 6 Stunden.",
                    ToastUrgency.Warning);
            }

            ShowUpdateFooter(result);
            ShowAvailableUpdateNotification(result, message);
            return result;
        }

        ShowUpdateFooter(result);

        var openDownload = System.Windows.MessageBox.Show(
            $"{message}\n\nJetzt den Setup-Installer herunterladen und starten?",
            "Update verfügbar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (openDownload != MessageBoxResult.Yes)
            return result;

        var download = await UpdateService.DownloadAndLaunchAsync(
            result.DownloadUrl!,
            result.ChecksumUrl);
        if (download.IsSuccess)
        {
            System.Windows.MessageBox.Show(
                download.Message,
                "Update",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            System.Windows.MessageBox.Show(
                download.Message,
                "Update",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        return result;
    }

    private void ShowAvailableUpdateNotification(UpdateCheckResult result, string message)
    {
        if (!ToastService.AreNotificationsEnabled)
            return;

        var latest = result.LatestVersion ?? string.Empty;
        if (string.Equals(_lastBalloonedUpdateVersion, latest, StringComparison.OrdinalIgnoreCase))
            return;

        _lastBalloonedUpdateVersion = latest;
        ToastService.Show(
            "Update verfügbar",
            $"{message} Im Footer kannst du es installieren.",
            ToastUrgency.Info);
    }

    private void ShowReleasePreparingNotification(UpdateCheckResult result)
    {
        if (!ToastService.AreNotificationsEnabled)
            return;

        var latest = result.LatestVersion ?? string.Empty;
        if (string.Equals(_lastBalloonedUpdateVersion, latest, StringComparison.OrdinalIgnoreCase))
            return;

        _lastBalloonedUpdateVersion = latest;
        ToastService.Show(
            "Update wird vorbereitet",
            $"Version {latest} wurde gefunden. Der Installer wird noch erstellt.",
            ToastUrgency.Info);
    }

    private void ShowUpdateFooter(UpdateCheckResult result)
    {
        _availableUpdateDownloadUrl = result.DownloadUrl;
        _availableUpdateChecksumUrl = result.ChecksumUrl;
        UpdateFooterBorder.Visibility = Visibility.Visible;

        if (string.IsNullOrWhiteSpace(result.DownloadUrl))
        {
            UpdateFooterText.Text = $"Update {result.LatestVersion} wird vorbereitet";
            InstallUpdateButton.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateFooterText.Text = $"Update {result.LatestVersion} verfügbar";
        InstallUpdateButton.Visibility = Visibility.Visible;
        InstallUpdateButton.IsEnabled = true;
        InstallUpdateButton.Content = "Update installieren";
    }

    private void HideUpdateFooter()
    {
        _availableUpdateDownloadUrl = null;
        _availableUpdateChecksumUrl = null;
        InstallUpdateButton.Visibility = Visibility.Visible;
        UpdateFooterBorder.Visibility = Visibility.Collapsed;
    }

    private async void InstallUpdateFooter_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_availableUpdateDownloadUrl))
            return;

        InstallUpdateButton.IsEnabled = false;
        InstallUpdateButton.Content = "Lade Update …";

        var download = await UpdateService.DownloadAndLaunchAsync(
            _availableUpdateDownloadUrl,
            _availableUpdateChecksumUrl);

        if (download.IsSuccess)
        {
            InstallUpdateButton.Content = "Setup gestartet";
            System.Windows.MessageBox.Show(
                download.Message,
                "Update",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        InstallUpdateButton.IsEnabled = true;
        InstallUpdateButton.Content = "Erneut versuchen";
        System.Windows.MessageBox.Show(
            download.Message,
            "Update",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
