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

    private static string GetServiceStatus(AppStatus status)
    {
        var outage = status.Outage ?? "none";
        if (outage.Equals("full", StringComparison.OrdinalIgnoreCase))
            return "full";
        if (outage.Equals("partial", StringComparison.OrdinalIgnoreCase))
            return "partial";
        if (status.HasMaintenance || status.HasSubComponentMaintenance)
            return "maintenance";
        return "none";
    }

    private static string GetAffectedFunctionText(AppStatus status)
    {
        var functions = (status.AffectedFunctions ?? new())
            .Where(x => !string.IsNullOrWhiteSpace(x.Function))
            .Take(3)
            .Select(x =>
                string.IsNullOrWhiteSpace(x.ImpactDesc)
                    ? x.Function
                    : $"{x.Function}: {x.ImpactDesc}")
            .ToList();

        return functions.Count == 0 ? "" : string.Join("\n", functions);
    }

    // =============================================================
    // BENACHRICHTIGUNGEN
    // =============================================================

    private void ShowStatusChangeNotification(List<ServiceStatusChange> changes)
    {
        if (changes.Count == 0)
            return;

        var hasFull = changes.Any(x => x.CurrentStatus == "full");
        var hasPartial = changes.Any(x => x.CurrentStatus == "partial");
        var hasMaintenance = changes.Any(x => x.CurrentStatus == "maintenance");
        var allRecovered = changes.All(
            x => x.CurrentStatus == "none" &&
                 x.PreviousStatus is "full" or "partial" or "maintenance");

        string title;
        ToastUrgency urgency;

        if (hasFull)
        {
            title = "TI-Status: Störung";
            urgency = ToastUrgency.Error;
        }
        else if (hasPartial)
        {
            title = "TI-Status: Teilausfall";
            urgency = ToastUrgency.Warning;
        }
        else if (hasMaintenance)
        {
            title = "TI-Status: Beeinträchtigung";
            urgency = ToastUrgency.Warning;
        }
        else if (allRecovered)
        {
            title = "TI-Status: Wiederhergestellt";
            urgency = ToastUrgency.Info;
        }
        else
        {
            title = "TI-Status: Änderung";
            urgency = ToastUrgency.Info;
        }

        string body;
        if (changes.Count == 1)
        {
            var c = changes[0];
            var statusLabel = FormatStatusShort(c.CurrentStatus);
            // Mockup-style: "eRezept · Teilausfall · …"
            var hint = c.CurrentStatus switch
            {
                "full" => "gematik meldet Ausfall",
                "partial" => "gematik meldet Teilausfall",
                "maintenance" => "Wartung gemeldet",
                "none" => "Dienst wieder verfügbar",
                _ => statusLabel
            };
            body = $"{c.ServiceName} · {statusLabel} · {hint}";
        }
        else
        {
            var preview = changes
                .Take(2)
                .Select(c => $"{c.ServiceName}: {FormatStatusShort(c.CurrentStatus)}");
            body = $"{changes.Count} Dienste geändert · {string.Join(", ", preview)}";
            if (changes.Count > 2)
                body += " …";
        }

        body = Truncate(body, 110);
        ToastService.Show(title, body, urgency);
    }

    private void ShowIncidentNotification(
        List<(string Title, string Body, bool IsError)> incidents)
    {
        if (incidents.Count == 0)
            return;

        var worst = incidents.FirstOrDefault(x => x.IsError);
        var primary = worst.Title != null ? worst : incidents[0];

        string title;
        string body;
        ToastUrgency urgency;

        if (incidents.Count == 1)
        {
            title = primary.Title;
            body = Truncate(primary.Body, 110);
            urgency = primary.IsError ? ToastUrgency.Error : ToastUrgency.Warning;
        }
        else
        {
            var hasError = incidents.Any(x => x.IsError);
            title = hasError ? "TI-Status: Störung" : "TI-Status: Änderung";
            var names = incidents.Take(2).Select(x => Truncate(x.Body, 40));
            body = Truncate(
                $"{incidents.Count} neue Meldungen · {string.Join("; ", names)}",
                110);
            urgency = hasError ? ToastUrgency.Error : ToastUrgency.Warning;
        }

        ToastService.Show(title, body, urgency);
    }

    private static string FormatStatusShort(string status) =>
        status switch
        {
            "full" => "Komplettausfall",
            "partial" => "Teilausfall",
            "maintenance" => "Wartung",
            "none" => "wieder verfügbar",
            _ => status
        };

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text ?? "";
        return text[..(max - 1)].TrimEnd() + "…";
    }

    private static List<string> GetIncidentServices(
        Incident incident,
        Dictionary<string, string> names)
    {
        var result = new List<string>();
        foreach (var app in incident.App ?? Enumerable.Empty<string>())
        {
            if (names.TryGetValue(app, out var name))
                result.Add(name);
            else if (!string.IsNullOrWhiteSpace(app))
                result.Add(app);
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> GetIncidentServiceKeys(Incident incident)
    {
        var result = new List<string>();
        foreach (var app in incident.App ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(app))
                continue;

            foreach (var key in AppSettings.ServiceKeys)
            {
                if (string.Equals(key, app, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(key);
                    break;
                }
            }
        }
        return result;
    }

    private static string StripHtml(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return System.Text.RegularExpressions.Regex
            .Replace(text, "<.*?>", " ")
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Trim();
    }

    // =============================================================
    // BUTTONS
    // =============================================================

    private void Refresh_Click(object sender, RoutedEventArgs e) =>
        _ = RefreshAsync(false);

    private void OpenGematik_Click(object sender, RoutedEventArgs e) =>
        OpenUrl("https://fachportal.gematik.de/ti-status#TI-Anschluss");

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    // =============================================================
    // FENSTER
    // =============================================================

    private void ShowWindow()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();
    }

    private void ToggleWindow()
    {
        if (IsVisible && WindowState != WindowState.Minimized)
            HideToTray();
        else
            ShowWindow();
    }

    private void SetTrayStatus(Drawing.Icon icon, string text)
    {
        _tray.Icon = icon;
        if (text.Length > 63)
            text = text[..63];
        _tray.Text = text;
    }
}
