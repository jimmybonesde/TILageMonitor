using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace TILageMonitor;

/// <summary>
/// Centralized UI localization. German is the default; every other system language
/// uses the English fallback so the application is never shown half-translated.
/// </summary>
public static class LocalizationService
{
    public static bool IsGerman => IsGermanCulture(CultureInfo.CurrentUICulture);

    public static bool IsGermanCulture(CultureInfo? culture) =>
        string.Equals((culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName, "de", StringComparison.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Über TI-Lage Monitor"] = "About TI-Lage Monitor",
        ["Über TI-Lage Monitor…"] = "About TI-Lage Monitor…",
        ["TI-Lage anzeigen"] = "Show TI status",
        ["Jetzt aktualisieren"] = "Refresh now",
        ["↻  Aktualisieren"] = "↻  Refresh",
        ["📈  Verlauf"] = "📈  History",
        ["TI-Status · 14 Tage"] = "TI Status · 14 Days",
        ["TI-Status · 14 Tage…"] = "TI Status · 14 Days…",
        ["⚙  Einstellungen"] = "⚙  Settings",
        ["Einstellungen…"] = "Settings…",
        ["Einstellungen öffnen"] = "Open settings",
        ["Einstellungen"] = "Settings",
        ["Überwachung der Telematikinfrastruktur und zentraler TI-Dienste."] = "Monitoring the German telematics infrastructure and central TI services.",
        ["Wird geladen …"] = "Loading …",
        ["Status wird ermittelt…"] = "Determining status…",
        ["Verbindung"] = "Connection",
        ["● Prüfe API …"] = "● Checking API …",
        ["Dienste"] = "Services",
        ["Aktuelle Meldungen"] = "Current messages",
        ["Keine aktuellen Meldungen"] = "No current messages",
        ["Aktuell liegen keine Störungen, Ursachen oder Einschränkungen vor. Der Status aktualisiert sich automatisch."] = "There are currently no outages, causes or restrictions. Status updates automatically.",
        ["Update installieren"] = "Install update",
        ["Setup herunterladen und starten"] = "Download and start setup",
        ["gematik TI-Status öffnen"] = "Open gematik TI status",
        ["Beenden"] = "Exit",
        ["Autostart: aus"] = "Autostart: off",
        ["Autostart: an"] = "Autostart: on",
        ["Benachrichtigungen: aus"] = "Notifications: off",
        ["Benachrichtigungen: an"] = "Notifications: on",
        ["Darstellung, Startverhalten, Updates und Benachrichtigungen"] = "Appearance, startup behavior, updates and notifications",
        ["Darstellung"] = "Appearance",
        ["Oberfläche der Anwendung"] = "Application appearance",
        ["Dunkelmodus"] = "Dark mode",
        ["Start"] = "Startup",
        ["Beim Anmelden automatisch starten (mit --tray)"] = "Start automatically when signing in (with --tray)",
        ["Autostart"] = "Autostart",
        ["Updates"] = "Updates",
        ["Beim Start wird automatisch nach einer neuen Version gesucht."] = "A new version is checked automatically at startup.",
        ["Updates automatisch installieren"] = "Install updates automatically",
        ["Neue Releases werden heruntergeladen, per SHA-256 geprüft und nach dem Beenden der App ohne Assistent installiert."] = "New releases are downloaded, verified with SHA-256 and installed after the app closes, without a wizard.",
        ["Aktuelle Version wird beim Start automatisch geprüft."] = "The current version is checked automatically at startup.",
        ["Nach Updates suchen"] = "Check for updates",
        ["Benachrichtigungen"] = "Notifications",
        ["Toast-Meldungen pro Dienst (Tray-Icon-Farbe bleibt unverändert)"] = "Toast notifications per service (tray icon color remains unchanged)",
        ["Schließen"] = "Close",
        ["Funktionen"] = "Features",
        ["• Ampel-Status im Infobereich (Systemtray)"] = "• Traffic-light status in the notification area (system tray)",
        ["• Windows-Toasts bei Störungen und Statuswechseln"] = "• Windows toasts for outages and status changes",
        ["• 14-Tage-Verlauf mit Tages-Kacheln und Ereignisse-Timeline"] = "• 14-day history with daily tiles and event timeline",
        ["• Offline-Cache bei API-Ausfall"] = "• Offline cache when the API is unavailable",
        ["• Autostart, Dunkelmodus, Benachrichtigungsfilter"] = "• Autostart, dark mode and notification filters",
        ["Kontakt / Support"] = "Contact / Support",
        ["Fragen oder Feedback zur Community-App?"] = "Questions or feedback about this community app?",
        ["Homepage öffnen"] = "Open homepage",
        ["E-Mail schreiben"] = "Write email",
        ["Hinweis"] = "Notice",
        ["Kein offizielles Produkt der gematik GmbH. Datenquelle ist die öffentliche TI-Lage-API. Angaben ohne Gewähr."] = "Not an official product of gematik GmbH. Data comes from the public TI status API. No guarantee.",
        ["gematik Fachportal · TI-Status öffnen"] = "Open gematik portal · TI status",
        ["Tageskacheln in der Übersicht, Stunden-Zoom und Ereignisse als eigene Ansichten."] = "Daily tiles in the overview, hourly zoom and events as separate views.",
        ["Heute"] = "Today",
        ["Alles OK"] = "All OK",
        ["Schlechtester Status heute"] = "Worst status today",
        ["14 Tage"] = "14 days",
        ["0 auffällige Tage"] = "0 affected days",
        ["Tage mit Einschränkung, Störung oder Wartung"] = "Days with restrictions, outages or maintenance",
        ["Verfügbarkeit"] = "Availability",
        ["Lokal erfasste / API-gefüllte Stunden (ohne Zukunft)"] = "Locally recorded / API-filled hours (excluding the future)",
        ["Übersicht"] = "Overview",
        ["Stunden"] = "Hours",
        ["Ereignisse"] = "Events",
        ["Alle Dienste"] = "All services",
        ["Alle Dienste nebeneinander anzeigen"] = "Show all services side by side",
        ["Stabil · Alles ruhig"] = "Stable · All clear",
        ["In den letzten 14 Tagen gab es für diesen Fokus keine Einschränkungen oder Störungen."] = "There were no restrictions or outages for this focus in the last 14 days.",
        ["Tagesübersicht"] = "Daily overview",
        ["Große Tageskacheln — Farbe = schlechtester Status. Auswahl und Klick öffnen Stunden."] = "Large daily tiles — color shows the worst status. Select and click to open hours.",
        ["Noch keine Verlaufsdaten"] = "No history data yet",
        ["Der 14-Tage-Verlauf kombiniert gematik-API-Daten mit lokal erfassten Stunden. Sobald die App aktualisiert, füllen sich die Kacheln ruhig von selbst."] = "The 14-day history combines gematik API data with locally recorded hours. Tiles fill automatically as the app updates.",
        ["Bitte wählen Sie in der Übersicht einen Tag — oder es wird automatisch heute genutzt."] = "Please select a day in the overview — or today will be selected automatically.",
        ["← Zurück zur Übersicht"] = "← Back to overview",
        ["Stundenansicht (0–23)"] = "Hourly view (0–23)",
        ["Klick auf eine auffällige Stunde öffnet das passende Ereignis. Esc oder Zurück kehrt zur Übersicht."] = "Click an affected hour to open the matching event. Esc or Back returns to the overview.",
        ["Stunde"] = "Hour",
        ["Vollständige Timeline. Klick auf eine Karte öffnet die Stundenansicht für den Starttag."] = "Complete timeline. Click a card to open the hourly view for its start day.",
        ["Alles ruhig"] = "All clear",
        ["Im gewählten Fokus gibt es im 14-Tage-Fenster keine gemeldeten Ereignisse."] = "There are no reported events for the selected focus in the 14-day window.",
        ["Legende"] = "Legend",
        ["OK"] = "OK",
        ["Einschränkung"] = "Restriction",
        ["Wartung"] = "Maintenance",
        ["Störung"] = "Outage",
        ["keine Daten"] = "no data",
        ["TI-Status: wird geladen…"] = "TI status: loading…",
        ["Die Update-Prüfung ist fehlgeschlagen."] = "The update check failed.",
        ["Teilausfall"] = "Partial outage",
        ["Alles OK"] = "All OK",
        ["Verfügbar"] = "Available",
        ["Keine Daten"] = "No data",
        ["Heute"] = "Today",
        ["auffällige Tage"] = "affected days",
        ["Schlechtester Status heute"] = "Worst status today",
        ["physische Stunden mit Daten"] = "physical hours with data",
        ["Klick öffnet die Stundenansicht."] = "Click to open the hourly view.",
        ["wieder verfügbar"] = "available again"
    };

    public static string Translate(string? value, CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(value) || IsGermanCulture(culture))
            return value ?? string.Empty;
        if (English.TryGetValue(value, out var translated))
            return translated;

        // Dynamic status and tooltip texts are composed at runtime. Translate known
        // phrases inside those strings while preserving values, dates and numbers.
        var result = value;
        foreach (var pair in English.OrderByDescending(x => x.Key.Length))
            result = result.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        return result;
    }

    public static void Apply(Window window)
    {
        if (window is null || IsGerman)
            return;
        window.Title = Translate(window.Title);
        ApplyElement(window);
    }

    private static void ApplyElement(DependencyObject element)
    {
        if (element is TextBlock textBlock) textBlock.Text = Translate(textBlock.Text);
        if (element is Label label) label.Content = Translate(label.Content?.ToString());
        if (element is Button button) button.Content = Translate(button.Content?.ToString());
        if (element is CheckBox checkBox) checkBox.Content = Translate(checkBox.Content?.ToString());
        if (element is RadioButton radioButton) radioButton.Content = Translate(radioButton.Content?.ToString());
        if (element is HeaderedContentControl headered) headered.Header = Translate(headered.Header?.ToString());
        if (element is TabItem tab) tab.Header = Translate(tab.Header?.ToString());
        if (element is MenuItem menu) menu.Header = Translate(menu.Header?.ToString());
        if (element is FrameworkElement fe && fe.ToolTip is string tip) fe.ToolTip = Translate(tip);

        foreach (var child in LogicalTreeHelper.GetChildren(element))
            if (child is DependencyObject dependencyObject)
                ApplyElement(dependencyObject);
    }
}
