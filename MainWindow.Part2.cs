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

    private void Render(
        LageV2 lage,
        IncidentResponse incidents,
        OutageResponse outages,
        bool fromCache = false,
        DateTime? cacheTime = null,
        bool suppressTrayUpdate = false,
        bool incidentsTrusted = false)
    {
        lage.AppStatus ??= new(StringComparer.OrdinalIgnoreCase);
        lage.Cause ??= new();
        incidents.Data ??= new();
        outages.Data ??= new();

        _lastLage = lage;
        _lastFromCache = fromCache;
        _lastCacheTime = cacheTime;
        _lastSuppressTrayUpdate = suppressTrayUpdate;

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
                "full" => ThemeBrush("StatusOutage"),
                "partial" => ThemeBrush("StatusPartial"),
                "maintenance" => ThemeBrush("StatusMaintenance"),
                _ => ThemeBrush("StatusOk")
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

            var icon = currentStatus switch
            {
                "full" or "partial" or "maintenance" => "!",
                _ => "✓"
            };

            _apps.Add(new AppRow(icon, name, detail, statusBrush, serviceKey));
        }

        // =========================================================
        // URSACHEN
        // =========================================================

        foreach (var cause in lage.Cause)
        {
            var causeFocus = string.IsNullOrWhiteSpace(cause.Service)
                ? "TI-Komponente"
                : cause.Service;
            _messages.Add(
                new MessageRow(
                    LocalizationService.Translate("Ursache · ") + causeFocus,
                    $"{cause.Organization} – {cause.Function} (CI: {cause.Ci})",
                    FormatMessageTimestamp(lage.Timestamp),
                    causeFocus,
                    translateChrome: false));
        }

        // =========================================================
        // INCIDENTS
        // =========================================================

        // Track ALL active 1/4 IDs for seed/prune (not only the UI Take(10) slice).
        var activeIncidentIds = incidents.Data
            .Where(x => x.Status is 1 or 4)
            .Select(x => x.Id.ToString())
            .ToHashSet(StringComparer.Ordinal);
        var activeIncidentRows = incidents.Data
            .Where(x => x.Status is 1 or 4)
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .ToList();

        foreach (var incident in activeIncidentRows)
        {
            incident.Steps ??= new();
            incident.App ??= new();
            var incidentId = incident.Id.ToString();

            var latest = incident.Steps.OrderByDescending(x => x.Timestamp).FirstOrDefault();
            var body = StripHtml(latest?.Message ?? "Aktuelle Einschränkung.");
            var affectedServices = GetIncidentServices(incident, names);
            var affectedKeys = GetIncidentServiceKeys(incident);
            var serviceText = affectedServices.Count > 0
                ? string.Join(", ", affectedServices)
                : "TI";

            var incidentWhen = incident.CreatedAt;
            if (latest is not null && latest.Timestamp > incidentWhen)
                incidentWhen = latest.Timestamp;

            var incidentFocus = affectedKeys.FirstOrDefault() ?? serviceText;
            var incidentKind = LocalizationService.Translate(
                incident.Status == 1 ? "Störung" : "Einschränkung");
            _messages.Add(
                new MessageRow(
                    $"{incidentKind} · {serviceText}",
                    $"{incident.Title}\n{body}",
                    FormatMessageTimestamp(incidentWhen),
                    incidentFocus,
                    translateChrome: false));
        }

        // Seed silently until first Sync of a *trusted* active set (_incidentSeedDone),
        // independent of _firstLoad — so first-API-fail / soft-fail empty incidents do not
        // toast-storm when real IDs arrive later. Prune resolved IDs so a later reopen can toast.
        var seedIncidentsWithoutToast = !_incidentSeedDone;
        var newlyAppearedIncidentIds = ActiveIncidentTracker.Sync(
            _knownActiveIncidents,
            activeIncidentIds,
            seedWithoutToast: seedIncidentsWithoutToast);
        // Only mark seed done on trusted (non-soft-fail) incident responses.
        // Soft-fail/null/empty shells must leave the gate open.
        if (incidentsTrusted)
            _incidentSeedDone = true;

        if (!seedIncidentsWithoutToast)
        {
            // Toast any newly appeared active ID (not limited to UI Take(10)).
            foreach (var incident in incidents.Data.Where(x => x.Status is 1 or 4))
            {
                var incidentId = incident.Id.ToString();
                if (!newlyAppearedIncidentIds.Contains(incidentId))
                    continue;

                incident.Steps ??= new();
                incident.App ??= new();
                var affectedServices = GetIncidentServices(incident, names);
                var affectedKeys = GetIncidentServiceKeys(incident);
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
                var activeSlots = outage.Slots.Where(s => s.EndTimestamp == null).ToList();
                var outageWhen = activeSlots[0].StartTimestamp;
                _messages.Add(
                    new MessageRow(
                        LocalizationService.Translate("Automatisch erkannte Einschränkung · ") + outage.Service,
                        $"{outage.Provider}: " +
                        $"{string.Join("; ", activeSlots.Select(s => s.Function))}",
                        FormatMessageTimestamp(outageWhen),
                        outage.Service,
                        translateChrome: false));
            }
        }

        // =========================================================
        // GESAMTSTATUS (immer ALLE Dienste – Filter nur für Toasts)
        // =========================================================

        var overallStatus = TiStatusClassifier.ClassifyOverall(lage.AppStatus.Values);
        var hasFull = overallStatus == TiOverallStatus.Full;
        var hasPartial = overallStatus == TiOverallStatus.Partial;
        var hasMaintenance = overallStatus == TiOverallStatus.Maintenance;

        if (hasFull)
        {
            ApplyOverallStatusVisual(
                "outage",
                "Störung",
                "Mindestens ein TI-Dienst meldet einen Komplettausfall.");
            if (!suppressTrayUpdate)
                SetTrayStatus(_trayIconStoerung, "TI-Status: Störung");
        }
        else if (hasPartial || hasMaintenance)
        {
            if (hasPartial)
            {
                ApplyOverallStatusVisual(
                    "partial",
                    "Einschränkung",
                    "Mindestens ein TI-Dienst ist teilweise beeinträchtigt.");
            }
            else
            {
                ApplyOverallStatusVisual(
                    "maintenance",
                    "Wartung",
                    "Es läuft eine Wartung oder Einschränkung an einem TI-Dienst.");
            }
            if (!suppressTrayUpdate)
            {
                // Teilausfall: amber (nicht rot wie Vollausfall)
                SetTrayStatus(
                    _trayIconBeeintraechtigung,
                    hasPartial ? "TI-Status: Teilausfall" : "TI-Status: Beeinträchtigung");
            }
        }
        else
        {
            ApplyOverallStatusVisual(
                "ok",
                "Alles OK",
                "Alle Dienste sind erreichbar und arbeiten einwandfrei.");
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
                .Select(inc => (inc.Title, inc.Body, inc.IsError, inc.ServiceKeys))
                .ToList();

            if (filteredChanges.Count > 0 || filteredIncidents.Count > 0)
            {
                // Nur Recoveries: CurrentStatus none, vorher Problem
                var onlyRecoveries = filteredChanges.Count > 0 && filteredChanges.All(
                    x => x.CurrentStatus == "none" &&
                         x.PreviousStatus is "full" or "partial" or "maintenance");

                // Digest bereits gezeigt → keine zweiten Recovery-Toasts (status-only recoveries)
                if (digestFired && onlyRecoveries && filteredIncidents.Count == 0)
                {
                    // skip
                }
                else if (filteredChanges.Count > 0 && filteredIncidents.Count > 0)
                {
                    // Prefer one digest covering status + incident changes (no toast storm).
                    ShowCombinedStatusAndIncidentNotification(filteredChanges, filteredIncidents);
                }
                else if (filteredChanges.Count > 0)
                {
                    ShowStatusChangeNotification(filteredChanges);
                }
                else
                {
                    ShowIncidentNotification(filteredIncidents);
                }
            }
        }

        // =========================================================
        // ZEITSTEMPEL
        // =========================================================

        TimestampText.Text =
            $"{LocalizationService.Translate("Datenstand gematik:")} {lage.Timestamp.ToLocalTime().ToString("g", LocalizationService.DisplayCulture)}";

        if (fromCache)
        {
            ConnectionText.Text = LocalizationService.Translate("● Offline · letzter Stand");
            ConnectionText.Foreground = ThemeBrush("StatusPartial");
            FooterText.Text = cacheTime.HasValue
                ? $"{LocalizationService.Translate("Offline · Cache vom")} {cacheTime.Value.ToString("g", LocalizationService.DisplayCulture)}"
                : LocalizationService.Translate("Offline · Cache");
        }
        else
        {
            ConnectionText.Text = LocalizationService.Translate("● API erreichbar");
            ConnectionText.Foreground = ThemeBrush("StatusOk");
            FooterText.Text =
                $"{LocalizationService.Translate("Letzte Abfrage:")} {DateTime.Now.ToString("g", LocalizationService.DisplayCulture)}";
        }

        var empty = _messages.Count == 0;
        NoMessagesBorder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        MessagesList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

        ApplyPendingToastFocus();
    }

    // =============================================================
    // STATUS EINES DIENSTES
    // =============================================================
}
