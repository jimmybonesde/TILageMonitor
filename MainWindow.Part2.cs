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
    // =============================================================

    private void Render(
        LageV2 lage,
        IncidentResponse incidents,
        OutageResponse outages,
        bool fromCache = false,
        DateTime? cacheTime = null,
        bool suppressTrayUpdate = false)
    {
        lage.AppStatus ??= new(StringComparer.OrdinalIgnoreCase);
        lage.Cause ??= new();
        incidents.Data ??= new();
        outages.Data ??= new();

        _apps.Clear();
        _messages.Clear();

        var names = ServiceNames;

        var statusChanges = new List<ServiceStatusChange>();
        var newIncidents = new List<(string Title, string Body, bool IsError, List<string> ServiceKeys)>();

        // =========================================================
        // TI-ANWENDUNGEN
        // =========================================================

        foreach (var item in lage.AppStatus)
        {
            var serviceKey = item.Key;
            var name = names.TryGetValue(serviceKey, out var n) ? n : serviceKey;
            var s = item.Value;
            var currentStatus = GetServiceStatus(s);

            if (!_firstLoad)
            {
                if (_knownServiceStatus.TryGetValue(serviceKey, out var previousStatus))
                {
                    if (previousStatus != currentStatus)
                    {
                        statusChanges.Add(
                            new ServiceStatusChange(
                                serviceKey,
                                name,
                                previousStatus,
                                currentStatus,
                                GetAffectedFunctionText(s)));
                    }
                }
            }

            _knownServiceStatus[serviceKey] = currentStatus;

            var statusBrush = currentStatus switch
            {
                "full" => System.Windows.Media.Brushes.Firebrick,
                "partial" => System.Windows.Media.Brushes.DarkOrange,
                "maintenance" => System.Windows.Media.Brushes.DarkOrange,
                _ => System.Windows.Media.Brushes.ForestGreen
            };

            var detail = currentStatus switch
            {
                "full" => "Komplettausfall",
                "partial" => "Teilausfall",
                "maintenance" => "Wartung / Einschränkung",
                _ => "Verfügbar"
            };

            s.AffectedFunctions ??= new();
            foreach (var f in s.AffectedFunctions.Take(3))
            {
                if (!string.IsNullOrWhiteSpace(f.Function))
                    detail += $" – {f.Function}: {f.ImpactDesc}";
            }

            _apps.Add(new AppRow("●", name, detail, statusBrush));
        }

        // =========================================================
        // URSACHEN
        // =========================================================

        foreach (var cause in lage.Cause)
        {
            _messages.Add(
                new MessageRow(
                    "Ursache · " +
                    (string.IsNullOrWhiteSpace(cause.Service) ? "TI-Komponente" : cause.Service),
                    $"{cause.Organization} – {cause.Function} (CI: {cause.Ci})"));
        }

        // =========================================================
        // INCIDENTS
        // =========================================================

        foreach (var incident in incidents.Data
                     .Where(x => x.Status is 1 or 4)
                     .OrderByDescending(x => x.CreatedAt)
                     .Take(10))
        {
            incident.Steps ??= new();
            incident.App ??= new();
            var latest = incident.Steps.OrderByDescending(x => x.Timestamp).FirstOrDefault();
            var body = StripHtml(latest?.Message ?? "Aktuelle Einschränkung.");
            var affectedServices = GetIncidentServices(incident, names);
            var affectedKeys = GetIncidentServiceKeys(incident);
            var serviceText = affectedServices.Count > 0
                ? string.Join(", ", affectedServices)
                : "TI";

            _messages.Add(
                new MessageRow(
                    $"{(incident.Status == 1 ? "Störung" : "Einschränkung")} · {serviceText}",
                    $"{incident.Title}\n{body}"));

            if (!_firstLoad && _knownActiveIncidents.Add(incident.Id.ToString()))
            {
                var shortTitle = Truncate(
                    affectedServices.Count > 0
                        ? $"{string.Join(", ", affectedServices)}: {incident.Title}"
                        : incident.Title,
                    90);

                newIncidents.Add((
                    incident.Status == 1 ? "TI-Status: Störung" : "TI-Status: Änderung",
                    shortTitle,
                    incident.Status == 1,
                    affectedKeys));
            }
        }

        // =========================================================
        // AUTOMATISCH ERKANNTE AUSFÄLLE
        // =========================================================

        foreach (var outage in outages.Data.Take(10))
        {
            outage.Slots ??= new();
            var active = outage.Slots.Any(s => s.EndTimestamp == null);
            if (active)
            {
                _messages.Add(
                    new MessageRow(
                        "Automatisch erkannte Einschränkung · " + outage.Service,
                        $"{outage.Provider}: " +
                        $"{string.Join("; ", outage.Slots.Where(s => s.EndTimestamp == null).Select(s => s.Function))}"));
            }
        }

        // =========================================================
        // GESAMTSTATUS (immer ALLE Dienste – Filter nur für Toasts)
        // =========================================================

        var hasFull = lage.AppStatus.Values.Any(
            x => (x.Outage ?? "").Equals("full", StringComparison.OrdinalIgnoreCase));
        var hasPartial = lage.AppStatus.Values.Any(
            x => (x.Outage ?? "").Equals("partial", StringComparison.OrdinalIgnoreCase));
        var hasMaintenance = lage.AppStatus.Values.Any(
            x => x.HasMaintenance || x.HasSubComponentMaintenance);

        if (hasFull)
        {
            OverallIcon.Text = "●";
            OverallIcon.Foreground = System.Windows.Media.Brushes.Firebrick;
            OverallText.Text = "STÖRUNG";
            OverallText.Foreground = System.Windows.Media.Brushes.Firebrick;
            if (!suppressTrayUpdate)
                SetTrayStatus(_trayIconStoerung, "TI-Status: Störung");
        }
        else if (hasPartial || hasMaintenance)
        {
            OverallIcon.Text = "●";
            OverallIcon.Foreground = System.Windows.Media.Brushes.DarkOrange;
            OverallText.Text = hasPartial ? "EINSCHRÄNKUNG" : "WARTUNG";
            OverallText.Foreground = System.Windows.Media.Brushes.DarkOrange;
            if (!suppressTrayUpdate)
            {
                SetTrayStatus(
                    hasPartial ? _trayIconStoerung : _trayIconBeeintraechtigung,
                    hasPartial ? "TI-Status: Störung" : "TI-Status: Beeinträchtigung");
            }
        }
        else
        {
            OverallIcon.Text = "●";
            OverallIcon.Foreground = System.Windows.Media.Brushes.ForestGreen;
            OverallText.Text = "NORMAL";
            OverallText.Foreground = System.Windows.Media.Brushes.ForestGreen;
            if (!suppressTrayUpdate)
                SetTrayStatus(_trayIconOk, "TI-Status: OK");
        }

        // =========================================================
        // "Alles wieder OK" Digest (nur Live, nicht erster Load)
        // =========================================================

        var nowProblem = hasFull || hasPartial || hasMaintenance;
        var digestFired = false;

        if (!_firstLoad && !fromCache && _hadTiProblem && !nowProblem)
        {
            ToastService.Show(
                "TI-Status: Alles wieder OK",
                "Alle überwachten Dienste wieder normal.",
                ToastUrgency.Info);
            digestFired = true;
        }

        if (!fromCache)
            _hadTiProblem = nowProblem;

        // =========================================================
        // BENACHRICHTIGUNGEN (gefiltert, gebündelt)
        // =========================================================

        if (!_firstLoad && !fromCache)
        {
            var filteredChanges = statusChanges
                .Where(c => _settings.IsNotifyEnabled(c.ServiceKey))
                .ToList();

            var filteredIncidents = newIncidents
                .Where(inc =>
                    inc.ServiceKeys.Count == 0 ||
                    inc.ServiceKeys.Any(k => _settings.IsNotifyEnabled(k)))
                .Select(inc => (inc.Title, inc.Body, inc.IsError))
                .ToList();

            if (filteredChanges.Count > 0)
            {
                // Nur Recoveries: CurrentStatus none, vorher Problem
                var onlyRecoveries = filteredChanges.All(
                    x => x.CurrentStatus == "none" &&
                         x.PreviousStatus is "full" or "partial" or "maintenance");

                // Digest bereits gezeigt → keine zweiten Recovery-Toasts
                if (digestFired && onlyRecoveries)
                {
                    // skip ShowStatusChangeNotification
                }
                else
                {
                    ShowStatusChangeNotification(filteredChanges);
                }
            }
            else if (filteredIncidents.Count > 0)
            {
                ShowIncidentNotification(filteredIncidents);
            }
        }

        // =========================================================
        // ZEITSTEMPEL
        // =========================================================

        TimestampText.Text =
            $"Datenstand gematik: {lage.Timestamp.ToLocalTime():dd.MM.yyyy HH:mm:ss}";

        if (fromCache)
        {
            ConnectionText.Text = "● Offline · letzter Stand";
            ConnectionText.Foreground = System.Windows.Media.Brushes.DarkOrange;
            FooterText.Text = cacheTime.HasValue
                ? $"Offline · Cache vom {cacheTime.Value:dd.MM.yyyy HH:mm:ss}"
                : "Offline · Cache";
        }
        else
        {
            ConnectionText.Text = "● API erreichbar";
            ConnectionText.Foreground = System.Windows.Media.Brushes.ForestGreen;
            FooterText.Text = $"Letzte Abfrage: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
        }

        NoMessagesBorder.Visibility =
            _messages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // =============================================================
    // STATUS EINES DIENSTES
}
