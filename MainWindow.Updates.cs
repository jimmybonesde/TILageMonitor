using System.Windows;
using Forms = System.Windows.Forms;

namespace TILageMonitor;

public partial class MainWindow
{
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
            _tray.ShowBalloonTip(
                10000,
                "Update verfügbar",
                $"{message} In den Einstellungen kannst du es herunterladen.",
                Forms.ToolTipIcon.Info);
            return result;
        }

        var openDownload = System.Windows.MessageBox.Show(
            $"{message}\n\nJetzt den Setup-Installer herunterladen?",
            "Update verfügbar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (openDownload == MessageBoxResult.Yes && !string.IsNullOrWhiteSpace(result.DownloadUrl))
            OpenUrl(result.DownloadUrl);

        return result;
    }
}
