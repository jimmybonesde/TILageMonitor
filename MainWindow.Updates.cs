using System.Windows;

namespace TILageMonitor;

public partial class MainWindow
{
    private string? _availableUpdateDownloadUrl;
    private string? _availableUpdateChecksumUrl;
    private readonly SemaphoreSlim _updateCheckGate = new(1, 1);
    private readonly SemaphoreSlim _updateInstallGate = new(1, 1);

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool userInitiated)
    {
        await _updateCheckGate.WaitAsync();
        try
        {
        var result = await UpdateService.CheckAsync();

        if (!result.IsSuccess)
        {
            if (userInitiated)
            {
                System.Windows.MessageBox.Show(
                    LocalizationService.TranslateMessage(result.ErrorMessage ?? "Die Update-Prüfung ist fehlgeschlagen."),
                    LocalizationService.Translate("Updates prüfen"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return result;
        }

        if (!result.IsUpdateAvailable)
        {
            HideUpdateFooter();
            ClearAutomaticUpdateRetry();

            if (userInitiated)
            {
                System.Windows.MessageBox.Show(
                    LocalizationService.IsGerman ? $"TI-Lage Monitor ist aktuell (Version {result.CurrentVersion})." : $"TI-Lage Monitor is up to date (version {result.CurrentVersion}).",
                    LocalizationService.Translate("Updates prüfen"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return result;
        }

        var message =
            $"{LocalizationService.Translate("Version")} {result.LatestVersion} {LocalizationService.Translate("ist verfügbar. Aktuell installiert:")} {result.CurrentVersion}.";
        var installerAvailable = !string.IsNullOrWhiteSpace(result.DownloadUrl);

        // GitHub releases can be visible shortly before Actions attaches the Setup.exe.
        // Keep this state informative, but never offer a non-installable HTML release page.
        if (!installerAvailable)
        {
            ShowUpdateFooter(result);

            if (userInitiated)
            {
                System.Windows.MessageBox.Show(
                    $"{message}\n\n{LocalizationService.Translate("Der Installer wird gerade noch erstellt. Bitte in wenigen Minuten erneut prüfen.")}",
                    LocalizationService.Translate("Update wird vorbereitet"),
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
                    _settings.LastAutoInstallFailureVersion,
                    _settings.AutoInstallRetryAfterUtc ?? DateTime.MinValue,
                    DateTime.UtcNow))
            {
                var automaticDownload = await DownloadAndLaunchSerializedAsync(
                    result.DownloadUrl!,
                    result.ChecksumUrl,
                    silent: true,
                    updateVersion: result.LatestVersion);

                if (automaticDownload.IsSuccess)
                {
                    CloseApp();
                    return result;
                }

                _settings.LastAutoInstallFailureVersion = result.LatestVersion;
                _settings.AutoInstallRetryAfterUtc = AutoUpdateRetryPolicy.GetNextRetryUtc(DateTime.UtcNow);
                SettingsStore.Save(_settings);
                ToastService.Show(
                    LocalizationService.Translate("Automatisches Update fehlgeschlagen"),
                    $"{LocalizationService.TranslateMessage(automaticDownload.Message)}\n{LocalizationService.Translate("Nächster Versuch frühestens in 6 Stunden.")}",
                    ToastUrgency.Warning);
            }

            ShowUpdateFooter(result);
            ShowAvailableUpdateNotification(result, message);
            return result;
        }

        ShowUpdateFooter(result);

        var openDownload = System.Windows.MessageBox.Show(
            $"{message}\n\n{LocalizationService.Translate("Jetzt den Setup-Installer herunterladen und starten?")}",
            LocalizationService.Translate("Update verfügbar"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (openDownload != MessageBoxResult.Yes)
            return result;

        var download = await DownloadAndLaunchSerializedAsync(
            result.DownloadUrl!,
            result.ChecksumUrl);
        if (download.IsSuccess)
        {
            System.Windows.MessageBox.Show(
                LocalizationService.TranslateMessage(download.Message),
                LocalizationService.Translate("Update"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            System.Windows.MessageBox.Show(
                LocalizationService.TranslateMessage(download.Message),
                LocalizationService.Translate("Update"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        return result;
        }
        finally
        {
            _updateCheckGate.Release();
        }
    }

    private async Task<UpdateDownloadResult> DownloadAndLaunchSerializedAsync(
        string downloadUrl,
        string? checksumUrl,
        bool silent = false,
        string? updateVersion = null)
    {
        await _updateInstallGate.WaitAsync();
        try
        {
            return await UpdateService.DownloadAndLaunchAsync(downloadUrl, checksumUrl, silent, updateVersion);
        }
        finally
        {
            _updateInstallGate.Release();
        }
    }

    private void ClearAutomaticUpdateRetry()
    {
        if (_settings.LastAutoInstallFailureVersion is null &&
            _settings.AutoInstallRetryAfterUtc is null)
        {
            return;
        }

        _settings.LastAutoInstallFailureVersion = null;
        _settings.AutoInstallRetryAfterUtc = null;
        SettingsStore.Save(_settings);
    }

    public void ShowUpdateFailureAfterRestart()
    {
        ShowWindow();
        System.Windows.MessageBox.Show(
            LocalizationService.Translate("Das automatische Update konnte nicht abgeschlossen werden. Die bisherige Version wurde wieder gestartet. Details stehen in %AppData%\\TILageMonitor\\update-install.log."),
            LocalizationService.Translate("Update fehlgeschlagen"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
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
            LocalizationService.Translate("Update verfügbar"),
            $"{message} {LocalizationService.Translate("Im Footer kannst du es installieren.")}",
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
            LocalizationService.Translate("Update wird vorbereitet"),
            $"{LocalizationService.Translate("Version")} {latest} {LocalizationService.Translate("wurde gefunden. Der Installer wird noch erstellt.")}",
            ToastUrgency.Info);
    }

    private void ShowUpdateFooter(UpdateCheckResult result)
    {
        _availableUpdateDownloadUrl = result.DownloadUrl;
        _availableUpdateChecksumUrl = result.ChecksumUrl;
        UpdateFooterBorder.Visibility = Visibility.Visible;

        if (string.IsNullOrWhiteSpace(result.DownloadUrl))
        {
            UpdateFooterText.Text =
                LocalizationService.IsGerman
                    ? $"Update {result.LatestVersion} wird vorbereitet"
                    : $"Update {result.LatestVersion} is being prepared";
            InstallUpdateButton.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateFooterText.Text =
            LocalizationService.IsGerman
                ? $"Update {result.LatestVersion} verfügbar"
                : $"Update {result.LatestVersion} available";
        InstallUpdateButton.Visibility = Visibility.Visible;
        InstallUpdateButton.IsEnabled = true;
        InstallUpdateButton.Content = LocalizationService.Translate("Update installieren");
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
        InstallUpdateButton.Content = LocalizationService.Translate("Lade Update …");

        var download = await DownloadAndLaunchSerializedAsync(
            _availableUpdateDownloadUrl,
            _availableUpdateChecksumUrl);

        if (download.IsSuccess)
        {
            InstallUpdateButton.Content = LocalizationService.Translate("Setup gestartet");
            System.Windows.MessageBox.Show(
                LocalizationService.TranslateMessage(download.Message),
                LocalizationService.Translate("Update"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        InstallUpdateButton.IsEnabled = true;
        InstallUpdateButton.Content = LocalizationService.Translate("Erneut versuchen");
        System.Windows.MessageBox.Show(
            LocalizationService.TranslateMessage(download.Message),
            LocalizationService.Translate("Update"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
