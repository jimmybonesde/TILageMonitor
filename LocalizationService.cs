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
    private static string _language = "system";

    public static bool IsGerman => _language switch
    {
        "de" => true,
        "en" => false,
        _ => IsGermanCulture(CultureInfo.CurrentUICulture)
    };

    public static CultureInfo DisplayCulture => _language switch
    {
        "de" => CultureInfo.GetCultureInfo("de-DE"),
        "en" => CultureInfo.GetCultureInfo("en-US"),
        _ => CultureInfo.CurrentUICulture
    };

    public static void Configure(string? language)
    {
        _language = language?.Trim().ToLowerInvariant() switch
        {
            "de" => "de",
            "en" => "en",
            _ => "system"
        };
    }

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
        ["Sprache"] = "Language",
        ["Sprache der Oberfläche auswählen"] = "Choose the interface language",
        ["Automatisch (Windows-Systemsprache)"] = "Automatic (Windows display language)",
        ["Deutsch"] = "German",
        ["Die Auswahl wird nach einem Neustart der App übernommen."] = "Your selection will take effect after restarting the app.",
        ["Die Auswahl wurde gespeichert und wird nach einem Neustart der App übernommen."] = "Your selection was saved and will take effect after restarting the app.",
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
        ["kein Datenpunkt"] = "no data point",
        ["TI-Status: wird geladen…"] = "TI status: loading…",
        ["Die Update-Prüfung ist fehlgeschlagen."] = "The update check failed.",
        ["Teilausfall"] = "Partial outage",
        ["Verfügbar"] = "Available",
        ["Keine Daten"] = "No data",
        ["auffällige Tage"] = "affected days",
        ["physische Stunden mit Daten"] = "physical hours with data",
        ["Klick öffnet die Stundenansicht."] = "Click to open the hourly view.",
        ["wieder verfügbar"] = "available again",
        ["API nicht erreichbar"] = "API unavailable",
        ["Die gematik API antwortet nicht. Bitte später erneut versuchen."] = "The gematik API is not responding. Please try again later.",
        ["● Keine Verbindung"] = "● No connection",
        ["API nicht erreichbar · letzter Stand wird angezeigt."] = "API unavailable · showing the last known status.",
        ["TI-Status: API down"] = "TI status: API down",
        ["Die gematik API ist wiederholt nicht erreichbar."] = "The gematik API is repeatedly unavailable.",
        ["Wartung / Einschränkung"] = "Maintenance / restriction",
        ["Aktuelle Einschränkung."] = "Current restriction.",
        ["TI-Status: Änderung"] = "TI status: change",
        ["Automatisch erkannte Einschränkung · "] = "Automatically detected restriction · ",
        ["Mindestens ein TI-Dienst meldet einen Komplettausfall."] = "At least one TI service reports a full outage.",
        ["Mindestens ein TI-Dienst ist teilweise beeinträchtigt."] = "At least one TI service is partially affected.",
        ["Es läuft eine Wartung oder Einschränkung an einem TI-Dienst."] = "Maintenance or a restriction is in progress for a TI service.",
        ["TI-Status: Teilausfall"] = "TI status: partial outage",
        ["TI-Status: Beeinträchtigung"] = "TI status: degraded",
        ["Alle Dienste sind erreichbar und arbeiten einwandfrei."] = "All services are available and working correctly.",
        ["TI-Status: Alles wieder OK"] = "TI status: all clear again",
        ["Alle überwachten Dienste wieder normal."] = "All monitored services are normal again.",
        ["TI-Status: Wiederhergestellt"] = "TI status: restored",
        ["Wartung gemeldet"] = "Maintenance reported",
        ["Dienst wieder verfügbar"] = "Service available again",
        ["Updates prüfen"] = "Check updates",
        ["Der Installer wird gerade noch erstellt. Bitte in wenigen Minuten erneut prüfen."] = "The installer is still being created. Please check again in a few minutes.",
        ["Update wird vorbereitet"] = "Preparing update",
        ["Automatisches Update fehlgeschlagen"] = "Automatic update failed",
        ["Nächster Versuch frühestens in 6 Stunden."] = "The next attempt will be made in 6 hours at the earliest.",
        ["Update verfügbar"] = "Update available",
        ["Das automatische Update konnte nicht abgeschlossen werden. Die bisherige Version wurde wieder gestartet. Details stehen in %AppData%\\TILageMonitor\\update-install.log."] = "The automatic update could not be completed. The previous version was restarted. Details are in %AppData%\\TILageMonitor\\update-install.log.",
        ["Update fehlgeschlagen"] = "Update failed",
        ["Der Installer wird noch erstellt."] = "The installer is still being created.",
        ["wird vorbereitet"] = "is being prepared",
        ["Lade Update …"] = "Downloading update …",
        ["Erneut versuchen"] = "Try again",
        ["Keine Daten für diesen Dienst"] = "No data for this service",
        ["Für den gewählten Dienst gibt es in diesem Zeitraum keine Einträge. „Alle Dienste“ wählen oder einen anderen Chip tippen."] = "There are no entries for the selected service in this period. Select “All services” or choose another chip.",
        ["alle Dienste"] = "all services",
        ["In den letzten 14 Tagen blieb"] = "In the last 14 days",
        ["ohne Einschränkung oder Störung — ein ruhiges Bild."] = "without any restriction or outage — a quiet picture.",
        ["In den letzten 14 Tagen waren alle bekannten Tage über die Dienste hinweg ohne Einschränkung oder Störung."] = "Across all services, every known day in the last 14 days had no restriction or outage.",
        ["Schlechtester Status heute (alle Dienste)"] = "Worst status today (all services)",
        ["1 auffälliger Tag"] = "1 affected day",
        ["Noch keine bekannten Stunden für die Berechnung"] = "No known hours for the calculation yet",
        ["OK-Stunden:"] = "OK hours:",
        ["von"] = "of",
        ["inkl. API-gefüllte (ohne Zukunft)"] = "including API-filled hours (excluding the future)",
        ["nur lokal erfasste Stunden"] = "locally recorded hours only",
        ["Stundenansicht (0–23) — Klick auf auffällige Stunden öffnet das Ereignis; Esc zurück zur Übersicht."] = "Hourly view (0–23) — click an affected hour to open its event; Esc returns to the overview.",
        ["Ereignisse als Timeline — Esc kehrt zur Übersicht; Klick öffnet die Stundenansicht für den Starttag."] = "Events as a timeline — Esc returns to the overview; click to open the hourly view for the start day.",
        ["Fokus:"] = "Focus:",
        ["große Tageskacheln. Tag öffnet Stunden; Ereignisse über den Segment-Umschalter."] = "large daily tiles. A day opens its hours; events are available via the segment switcher.",
        ["Alle Dienste in kompakten Zeilen. Ein Dienst-Chip fokussiert die Tageskacheln; Ereignisse separat."] = "All services in compact rows. A service chip focuses the daily tiles; events are shown separately.",
        ["Kein Tag gewählt"] = "No day selected",
        ["kein Ereignis für diese Stunde"] = "no event for this hour",
        ["Einschr."] = "Restr.",
        ["Vollständige Störung im TI-Status — Stundenansicht für Details."] = "Full TI status outage — open the hourly view for details.",
        ["Einschränkung / Teilausfall — Zeitraum lokal dargestellt."] = "Restriction / partial outage — period shown locally.",
        ["Geplante oder laufende Wartung im erfassten Fenster."] = "Scheduled or ongoing maintenance in the captured window.",
        ["Ereignis aus dem TI-Status (lokal dargestellt)."] = "Event from TI status (shown locally).",
        ["Stundenansicht öffnen"] = "Open hourly view",
        ["Die Versionsnummer des Releases konnte nicht gelesen werden."] = "The release version could not be read.",
        ["Update-Prüfung fehlgeschlagen:"] = "Update check failed:",
        ["Download oder Start fehlgeschlagen:"] = "Download or launch failed:",
        ["Authenticode-Prüfung fehlgeschlagen:"] = "Authenticode check failed:",
        ["Starte Setup …"] = "Starting setup …",
        ["Setup gestartet"] = "Setup started",
        ["Keine Download-URL vorhanden."] = "No download URL is available.",
        ["Die Update-URL verweist nicht auf eine Setup.exe. Bitte die Release-Seite manuell öffnen."] = "The update URL does not point to a setup executable. Please open the release page manually.",
        ["Lade Setup herunter …"] = "Downloading setup …",
        ["Der Installer ist ungewöhnlich groß und wurde aus Sicherheitsgründen abgebrochen."] = "The installer is unusually large and was cancelled for security reasons.",
        ["Der Installer überschreitet das Größenlimit von 500 MB."] = "The installer exceeds the 500 MB size limit.",
        ["Prüfe Setup-Datei …"] = "Checking setup file …",
        ["Installiere Update …"] = "Installing update …",
        ["Das Update wird nach dem Beenden der App automatisch installiert und anschließend gestartet."] = "The update will be installed automatically after the app closes, then started again.",
        ["Setup wurde gestartet."] = "Setup was started.",
        ["Der Pfad der laufenden Anwendung konnte nicht bestimmt werden."] = "The path of the running application could not be determined.",
        ["Der Update-Starter konnte nicht gestartet werden."] = "The update launcher could not be started.",
        ["Die Prüfsumme des Updates fehlt."] = "The update checksum is missing.",
        ["Für den Installer wurde keine passende Prüfsumme gefunden."] = "No matching checksum was found for the installer.",
        ["Die Prüfsumme des Updates stimmt nicht überein."] = "The update checksum does not match.",
        ["Prüfsumme konnte nicht geprüft werden:"] = "Checksum could not be checked:",
        ["Datenstand gematik:"] = "gematik data as of:",
        ["● Offline · letzter Stand"] = "● Offline · last known status",
        ["Offline · Cache vom"] = "Offline · cache from",
        ["Offline · Cache"] = "Offline · cache",
        ["● API erreichbar"] = "● API available",
        ["Letzte Abfrage:"] = "Last checked:",
        ["Ursache ·"] = "Cause ·",
        ["Komplettausfall"] = "Full outage",
        ["Jetzt den Setup-Installer herunterladen und starten?"] = "Download and start the setup installer now?",
        ["Im Footer kannst du es installieren."] = "You can install it from the footer.",
        ["API wieder erreichbar"] = "API reachable again",
        ["API nicht erreichbar."] = "API unavailable.",
        ["TI-Status: Störung"] = "TI status: outage",
        ["TI-Status: OK"] = "TI status: OK",
        ["Prüfe GitHub-Release …"] = "Checking GitHub release …",
        ["gematik meldet Ausfall"] = "gematik reports an outage",
        ["gematik meldet Teilausfall"] = "gematik reports a partial outage",
        ["Dienste geändert"] = "services changed",
        ["neue Meldungen"] = "new messages",
        ["TI-Komponente"] = "TI component",
        ["Die Prüfsummen-URL ist nicht erlaubt."] = "The checksum URL is not allowed.",
        ["Update-Prüfung fehlgeschlagen."] = "Update check failed.",
        ["Benachrichtigungen aktiviert"] = "Notifications enabled",
        ["Pro Dienst"] = "Per service",
        ["Autor: Randy Carter / R.C.  ·  © 2026"] = "Author: Randy Carter / R.C.  ·  © 2026",
        ["Update"] = "Update",
        ["Version"] = "Version",
        ["ist verfügbar. Aktuell installiert:"] = "is available. Currently installed:",
        ["wurde gefunden. Der Installer wird noch erstellt."] = "was found. The installer is still being created.",
        ["Kompakte Zeilen je Dienst. Ein Dienst-Chip oben fokussiert die Tageskacheln; Ereignisse über den Segment-Umschalter."] = "Compact rows per service. A service chip above focuses the daily tiles; events are available via the segment switcher.",
        ["14 Tage API-Verlauf · {0} / {1} Stunden zusätzlich lokal erfasst"] = "14-day API history · {0} / {1} hours recorded locally",
        ["Stundenkacheln mit Daten"] = "hour tiles with data",
        ["In den letzten 14 Tagen blieb {0} ohne Einschränkung oder Störung — ein ruhiges Bild."] = "Over the last 14 days, {0} had no restriction or outage — a quiet picture.",
        ["Störung ·"] = "Outage ·",
        ["Einschränkung ·"] = "Restriction ·"
    };

    /// <summary>
    /// Exact catalog lookup only for UI chrome. Never substring-replaces free-form or
    /// API text (avoids corrupting words like <c>davon</c> via key <c>von</c>).
    /// </summary>
    public static string Translate(string? value, CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(value) || (culture is null ? IsGerman : IsGermanCulture(culture)))
            return value ?? string.Empty;
        return English.TryGetValue(value, out var translated) ? translated : value;
    }

    /// <summary>
    /// Translate an AppRow status label. When AffectedFunctions are appended after
    /// " – ", only the leading status fragment is exact-matched; the API suffix stays raw.
    /// </summary>
    public static string TranslateAppDetail(string? value, CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(value) || (culture is null ? IsGerman : IsGermanCulture(culture)))
            return value ?? string.Empty;

        const string sep = " – ";
        var idx = value.IndexOf(sep, StringComparison.Ordinal);
        if (idx < 0)
            return Translate(value, culture);

        return Translate(value[..idx], culture) + value[idx..];
    }

    /// <summary>
    /// Known update/error prefixes that may be followed by a dynamic exception suffix.
    /// Leading-prefix only — never mid-string replace.
    /// </summary>
    private static readonly string[] DynamicMessagePrefixes =
    [
        "Update-Prüfung fehlgeschlagen:",
        "Prüfsumme konnte nicht geprüft werden:",
        "Download oder Start fehlgeschlagen:",
        "Authenticode-Prüfung fehlgeschlagen:"
    ];

    /// <summary>
    /// Exact match, or translate a known leading update/error prefix and keep the suffix.
    /// Use for updater messages — not for API/incident prose.
    /// </summary>
    public static string TranslateMessage(string? value, CultureInfo? culture = null)
    {
        if (string.IsNullOrEmpty(value) || (culture is null ? IsGerman : IsGermanCulture(culture)))
            return value ?? string.Empty;
        if (English.TryGetValue(value, out var exact))
            return exact;

        foreach (var prefix in DynamicMessagePrefixes.OrderByDescending(p => p.Length))
        {
            if (value.StartsWith(prefix, StringComparison.Ordinal))
                return Translate(prefix, culture) + value[prefix.Length..];
        }

        return value;
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
        // A TextBlock with inline content can contain hyperlinks or dynamic Runs (for
        // example, the footer's clickable R.C. and version number). Assigning Text
        // would clear those inlines, so translate only plain TextBlocks.
        if (element is TextBlock textBlock && textBlock.Inlines.Count == 0)
            textBlock.Text = Translate(textBlock.Text);
        if (element is System.Windows.Controls.Label label) label.Content = Translate(label.Content?.ToString());
        if (element is System.Windows.Controls.Button button) button.Content = Translate(button.Content?.ToString());
        if (element is System.Windows.Controls.CheckBox checkBox) checkBox.Content = Translate(checkBox.Content?.ToString());
        if (element is System.Windows.Controls.RadioButton radioButton) radioButton.Content = Translate(radioButton.Content?.ToString());
        if (element is HeaderedContentControl headered) headered.Header = Translate(headered.Header?.ToString());
        if (element is TabItem tab) tab.Header = Translate(tab.Header?.ToString());
        if (element is System.Windows.Controls.MenuItem menu) menu.Header = Translate(menu.Header?.ToString());
        if (element is FrameworkElement fe && fe.ToolTip is string tip) fe.ToolTip = Translate(tip);

        foreach (var child in LogicalTreeHelper.GetChildren(element))
            if (child is DependencyObject dependencyObject)
                ApplyElement(dependencyObject);
    }
}
