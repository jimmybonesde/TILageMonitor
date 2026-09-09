using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace TILageMonitor;

public partial class MainWindow
{

    private void Settings_Click(object sender, RoutedEventArgs e) =>
        OpenSettingsWindow();

    private void OpenHistoryWindow()
    {
        if (_historyWindow is not null)
        {
            _historyWindow.RefreshView(_lastHistory, _lastOutages);
            if (!_historyWindow.IsVisible)
                _historyWindow.Show();
            _historyWindow.Activate();
            return;
        }

        _historyWindow = new HistoryWindow(this);
        _historyWindow.Closed += (_, _) => _historyWindow = null;
        _historyWindow.RefreshView(_lastHistory, _lastOutages);
        _historyWindow.Show();
    }

    private void History_Click(object sender, RoutedEventArgs e) =>
        OpenHistoryWindow();

    private void ToggleAutostartFromMenu()
    {
        var enabled = !_settings.AutoStart;
        _settings.AutoStart = enabled;
        SettingsStore.Save(_settings);
        AutostartService.SetEnabled(enabled);
        SyncAutostartMenuItem();
        _settingsWindow?.SyncFrom(_settings);
    }

    private void SyncAutostartMenuItem()
    {
        if (_autostartMenuItem is null)
            return;

        var on = _settings.AutoStart;
        _autostartMenuItem.Checked = on;
        _autostartMenuItem.Text = on ? "Autostart: an" : "Autostart: aus";
    }

    // =============================================================
    // ÜBER / EINSTELLUNGEN
    // =============================================================

    private void ShowAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version is null
            ? "1.0"
            : $"{version.Major}.{version.Minor}";

        var dark = ThemeService.IsDark ? "an" : "aus";
        var auto = _settings.AutoStart ? "an" : "aus";

        System.Windows.MessageBox.Show(
            "TI-Lage Monitor\n" +
            "Live-Überwachung der gematik TI-Lage\n\n" +
            "• Ampel-Status im Infobereich\n" +
            "• Windows-Toasts bei Störungen\n" +
            "• 7-Tage-Verlauf (lokal)\n" +
            "• Offline-Cache bei API-Ausfall\n" +
            "• Einstellungen (Darstellung, Autostart, Filter)\n\n" +
            $"Datenquelle: öffentliche gematik TI-Lage-API\n" +
            $"Version {versionText}\n" +
            "Autor: Randy Carter\n\n" +
            $"Dunkelmodus: {dark} · Autostart: {auto}\n\n" +
            $"Einstellungen:\n{SettingsStore.SettingsPath}",
            "Über TI-Lage Monitor",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    // =============================================================
    // DISPOSE
    // =============================================================

    public void Dispose() => DisposeRuntime();

    /// <summary>Idempotent cleanup for Closing, CloseApp, and App.OnExit.</summary>
    private void DisposeRuntime()
    {
        if (_disposed)
            return;
        _disposed = true;

        try { _timer.Stop(); } catch { /* ignore */ }
        StopShowWindowListener();

        try
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        catch
        {
            // already disposed
        }

        DisposeTrayIcons();
    }


    private async Task<IncidentResponse> GetIncidentsSoftAsync(CancellationToken ct)
    {
        try
        {
            return await _api.GetIncidentsAsync(ct);
        }
        catch
        {
            return new IncidentResponse();
        }
    }

    private async Task<OutageResponse> GetOutagesSoftAsync(CancellationToken ct)
    {
        try
        {
            return await _api.GetOutagesAsync(ct);
        }
        catch
        {
            return new OutageResponse();
        }
    }

    // =============================================================
    // API AKTUALISIEREN
    // =============================================================

    private async Task RefreshAsync(bool notify)
    {
        if (_loading)
            return;

        _loading = true;
        RefreshButton.IsEnabled = false;

        try
        {
            var ct = CancellationToken.None;

            // Parallel: Lage kritisch; Incidents/Outages soft-fail (leere Antwort)
            var lageTask = _api.GetLageAsync(ct);
            var incidentsTask = GetIncidentsSoftAsync(ct);
            var outagesTask = GetOutagesSoftAsync(ct);

            // WhenAny(lage) wirft nicht — Original-Exception bleibt an lageTask
            await Task.WhenAll(incidentsTask, outagesTask, Task.WhenAny(lageTask));

            var incidents = await incidentsTask;
            var outages = await outagesTask;
            var lage = await lageTask;

            // Last-known-good Cache speichern
            CacheStore.Save(lage, incidents, outages);

            // Client-seitigen 7-Tage-Verlauf upserten
            var history = HistoryStore.UpsertNow(lage);

            // API wieder da nach wiederholten Fehlern
            if (_consecutiveApiFailures >= 3 && !_firstLoad)
            {
                ToastService.Show(
                    "TI-Lage Monitor",
                    "API wieder erreichbar",
                    ToastUrgency.Info);
            }

            _consecutiveApiFailures = 0;
            _apiDownBalloonShown = false;

            Render(lage, incidents, outages, fromCache: false);
            RenderHistory(history, outages);

            _firstLoad = false;
        }
        catch (Exception ex)
        {
            _consecutiveApiFailures++;

            var cache = CacheStore.Load();
            if (cache is not null)
            {
                // Bei persistentem API-down Tray nicht aus Cache auf Grün setzen
                Render(
                    cache.Lage,
                    cache.Incidents,
                    cache.Outages,
                    fromCache: true,
                    cache.SavedAt,
                    suppressTrayUpdate: _consecutiveApiFailures >= 3);
                RenderHistory(HistoryStore.Load(), cache.Outages);

                ConnectionText.Text = "● Offline · letzter Stand";
                ConnectionText.Foreground = ThemeBrush("StatusPartial");

                FooterText.Text =
                    $"Offline · Cache vom {cache.SavedAt:dd.MM.yyyy HH:mm:ss} · {ex.Message}";
            }
            else
            {
                OverallIcon.Text = "●";
                OverallIcon.Foreground = ThemeBrush("StatusOutage");
                OverallText.Text = "API nicht erreichbar";
                OverallText.Foreground = ThemeBrush("StatusOutage");
                ConnectionText.Text = "● Keine Verbindung";
                ConnectionText.Foreground = ThemeBrush("StatusOutage");
                FooterText.Text = ex.Message;
            }

            // Toasts: nur bei 1. Fail und beim 3. Fail (persistent API-down)
            if (!_firstLoad &&
                (_consecutiveApiFailures == 1 ||
                 (_consecutiveApiFailures == 3 && !_apiDownBalloonShown)))
            {
                if (_consecutiveApiFailures == 1)
                {
                    ToastService.Show(
                        "TI-Lage Monitor",
                        cache is not null
                            ? "API nicht erreichbar · letzter Stand wird angezeigt."
                            : "API nicht erreichbar.",
                        cache is not null
                            ? ToastUrgency.Warning
                            : ToastUrgency.Error);
                }
                else
                {
                    _apiDownBalloonShown = true;
                    ToastService.Show(
                        "TI-Status: API down",
                        "Die gematik API ist wiederholt nicht erreichbar.",
                        ToastUrgency.Error);
                }
            }

            // Ab 3 Fehlern: Tray dauerhaft API down — IMMER NACH Render,
            // damit ein Cache-Render (grün/OK) den Tray nicht überschreibt.
            if (_consecutiveApiFailures >= 3)
            {
                SetTrayStatus(_trayIconStoerung, "TI-Status: API down");
                if (cache is null)
                {
                    OverallIcon.Text = "●";
                    OverallIcon.Foreground = ThemeBrush("StatusOutage");
                    OverallText.Text = "API nicht erreichbar";
                    OverallText.Foreground = ThemeBrush("StatusOutage");
                }
            }

            _firstLoad = false;
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            _loading = false;
        }
    }

    // =============================================================
    // VERLAUF DARSTELLEN
    // =============================================================

    private void RenderHistory(HistoryFile history, OutageResponse? outages) =>
        NotifyHistoryUpdated(history, outages);

    private void NotifyHistoryUpdated(HistoryFile history, OutageResponse? outages)
    {
        _lastHistory = history;
        _lastOutages = outages;

        if (_historyWindow is not null && _historyWindow.IsVisible)
            _historyWindow.RefreshView(history, outages);
    }


    private static System.Windows.Media.Brush ThemeBrush(string key) =>
        (System.Windows.Media.Brush)System.Windows.Application.Current.Resources[key];

}
