using System.Windows;

namespace TILageMonitor;

public partial class MainWindow
{
    private string? _availableUpdateDownloadUrl;
    private string? _availableUpdateChecksumUrl;
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

        if (!userInitiated)
        {
            if (_settings.AutoInstallUpdates &&
                !string.IsNullOrWhiteSpace(result.DownloadUrl))
            {
                var automaticDownload = await UpdateService.DownloadAndLaunchAsync(
                    result.DownloadUrl,
                    result.ChecksumUrl,
                    silent: true);

                if (automaticDownload.IsSuccess)
                {
                    CloseApp();
                    return result;
                }

                ToastService.Show(
                    "Automatisches Update fehlgeschlagen",
                    automaticDownload.Message,
                    ToastUrgency.Warning);
            }

            ShowUpdateFooter(result);

            // Respect „Benachrichtigungen aus“ and balloon only once per latestVersion
            if (!ToastService.AreNotificationsEnabled)
                return result;

            var latest = result.LatestVersion ?? string.Empty;
            if (string.Equals(_lastBalloonedUpdateVersion, latest, StringComparison.OrdinalIgnoreCase))
                return result;

            _lastBalloonedUpdateVersion = latest;
            ToastService.Show(
                "Update verfügbar",
                $"{message} In den Einstellungen kannst du es herunterladen.",
                ToastUrgency.Info);
            return result;
        }

        ShowUpdateFooter(result);

        var openDownload = System.Windows.MessageBox.Show(
            $"{message}\n\nJetzt den Setup-Installer herunterladen und starten?",
            "Update verfügbar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (openDownload != MessageBoxResult.Yes || string.IsNullOrWhiteSpace(result.DownloadUrl))
            return result;

        var download = await UpdateService.DownloadAndLaunchAsync(
            result.DownloadUrl,
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
            var openUrl = System.Windows.MessageBox.Show(
                $"{download.Message}\n\nStattdessen die Download-Seite im Browser öffnen?",
                "Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (openUrl == MessageBoxResult.Yes)
                OpenUrl(result.DownloadUrl);
        }

        return result;
    }

    private void ShowUpdateFooter(UpdateCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(result.DownloadUrl))
            return;

        _availableUpdateDownloadUrl = result.DownloadUrl;
        _availableUpdateChecksumUrl = result.ChecksumUrl;
        UpdateFooterText.Text = $"Update {result.LatestVersion} verfügbar";
        InstallUpdateButton.IsEnabled = true;
        InstallUpdateButton.Content = "Update installieren";
        UpdateFooterBorder.Visibility = Visibility.Visible;
    }

    private void HideUpdateFooter()
    {
        _availableUpdateDownloadUrl = null;
        _availableUpdateChecksumUrl = null;
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
