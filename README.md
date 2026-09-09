# TI-Lage Monitor

![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows&logoColor=white) ![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)

**Eine schlanke Windows-Tray-App, die den Zustand der Telematikinfrastruktur über die öffentlichen TI-Status-APIs der gematik im Blick behält.**

> [!IMPORTANT]
> TI-Lage Monitor ist ein privates Community-Projekt und **kein offizielles Produkt der gematik**. Die angezeigten Informationen stammen aus den öffentlichen gematik-Endpunkten; maßgeblich ist das [gematik Fachportal](https://fachportal.gematik.de/ti-status#TI-Anschluss).

## Funktionen

### Status auf einen Blick

- Überwacht eRezept, ePA, KIM, WANDA, OGD, VSDM und TI-Anschluss.
- App-Icon mit Status-Badge: **grün**, **orange** oder **rot**.
- Tooltip mit dem aktuellen Gesamtstatus.
- Ein Klick auf das Tray-Icon blendet das Hauptfenster ein oder aus.
- Aktualisierung im gematik-Rhythmus mit leichtem Offset (`:01`, `:06`, `:11`, …).
- Nach drei aufeinanderfolgenden Fehlern wird ein eigener API-down-Zustand angezeigt.

### Benachrichtigungen und Wiederherstellung

- Klickbare Windows-Toasts bei Statusänderungen, Incidents und API-Ausfällen; ein Klick öffnet das Hauptfenster.
- NotifyIcon-Ballons dienen als Fallback, wenn Toasts nicht verfügbar sind.
- Benachrichtigungen lassen sich in den Einstellungen pro Dienst filtern.
- Nach einer Erholung fasst ein „Alles wieder OK“-Digest die Rückkehr zum Normalzustand zusammen.
- Der letzte erfolgreiche Stand bleibt bei Verbindungsproblemen über einen Offline-Cache sichtbar.

### Desktop-Integration und Darstellung

- Persistenter Hell-/Dunkelmodus.
- Windows-Autostart direkt in den Tray.
- Einzelinstanz: Ein zweiter Start aktiviert die bereits laufende App.
- Eigene Fenster für **Einstellungen** und **Verlauf**.
- Direkter Link zum TI-Status im gematik Fachportal.

### Lokaler 7-Tage-Verlauf

Die öffentlichen APIs liefern keinen vollständigen mehrtägigen Zeitreihenverlauf. Daher speichert die App lokal stündliche Snapshots und zeigt sieben Kalendertage mit Tages-Zoom an. Pro Dienst bleibt für jede Stunde der schlechteste beobachtete Zustand erhalten; Stunden ohne Datenpunkt werden entsprechend markiert.

Der Verlauf entsteht erst während der Nutzung und füllt sich nur, solange die App regelmäßig läuft.

## Voraussetzungen

- Windows 10 oder Windows 11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) zum Bauen
- PowerShell 5.1 oder neuer

> [!WARNING]
> **Build-Voraussetzung:** `TILageMonitor.csproj` referenziert `TILageMonitor.ico`. Diese Binärdatei fehlt derzeit im Repository und muss vor dem Build im Projektstamm ergänzt werden.

## Schnellstart: Bauen

Repository klonen, PowerShell im Projektverzeichnis öffnen und ausführen:

```powershell
.\Build.ps1
```

Das Skript stellt die Pakete wieder her und erzeugt eine selbstenthaltende Single-File-Anwendung unter:

```text
publish\TILageMonitor.exe
```

### Manueller Publish

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o .\publish
```

Technische Basis: `net8.0-windows10.0.17763.0` und `Microsoft.Toolkit.Uwp.Notifications` 7.1.3.

## Installation

Nach einem erfolgreichen Build:

```powershell
.\Install.ps1
```

Optional mit Desktop-Verknüpfung und sofort aktiviertem Autostart:

```powershell
.\Install.ps1 -DesktopShortcut -EnableAutostart
```

Die Anwendung wird nach `%LocalAppData%\TILageMonitor\` kopiert. Das Skript legt eine Startmenü-Verknüpfung an; Desktop-Verknüpfung und Autostart sind optional. Alternativ lässt sich der Autostart später in der App aktivieren.

## Bedienung

### Tray

- **Linksklick auf das Icon:** Hauptfenster ein-/ausblenden
- **TI-Lage anzeigen:** Hauptfenster öffnen
- **Jetzt aktualisieren:** Status sofort neu abrufen
- **gematik TI-Status öffnen:** Fachportal im Browser öffnen
- **Einstellungen / Verlauf:** die jeweiligen Fenster öffnen
- **Autostart:** Start mit Windows ein-/ausschalten
- **Beenden:** Anwendung vollständig schließen

Der Autostart verwendet `--tray`, damit das Hauptfenster beim Systemstart zunächst verborgen bleibt.

### Einstellungen

Hier lassen sich Darstellung, Autostart und Benachrichtigungsfilter je Dienst konfigurieren. Änderungen werden lokal gespeichert und bleiben nach einem Neustart erhalten.

### Verlauf

Das Verlaufsfenster zeigt die lokal gesammelten stündlichen Zustände der letzten sieben Tage. Über den Tages-Zoom lässt sich ein einzelner Kalendertag genauer betrachten.

## Daten und Datenschutz

- Die App verwendet ausschließlich öffentliche API-Endpunkte; ein Login oder API-Key ist nicht erforderlich.
- Es werden keine Zugangsdaten gespeichert.
- Einstellungen, Cache und Verlauf bleiben lokal unter `%AppData%\TILageMonitor\`.
- Beim Öffnen des Fachportal-Links wird der Standardbrowser verwendet.

| Lokale Datei | Zweck |
|---|---|
| `%AppData%\TILageMonitor\settings.json` | Darstellung, Autostart und Benachrichtigungsfilter |
| `%AppData%\TILageMonitor\last-lage.json` | letzter erfolgreich geladener Status |
| `%AppData%\TILageMonitor\history.json` | stündlicher lokaler 7-Tage-Verlauf |

## Verwendete API-Endpunkte

- `https://ti-lage.prod.ccs.gematik.solutions/lageapi/v2/tilage`
- `https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/incident`
- `https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/outage`

## Architektur und Projektstruktur

Die Anwendung ist eine klassische WPF-Desktop-App auf .NET 8. Für das Tray-Icon und den Balloon-Fallback wird Windows Forms eingebunden; Toast-Benachrichtigungen laufen über `Microsoft.Toolkit.Uwp.Notifications`.

| Bereich | Zentrale Dateien |
|---|---|
| Anwendung und Lifecycle | `App.xaml(.cs)` |
| Hauptoberfläche und Tray-Steuerung | `MainWindow.xaml(.cs)`, `MainWindow.Part*.cs` |
| API und Datenmodelle | `ApiClient.cs`, `Models.cs` |
| Persistenz | `SettingsStore.cs`, `CacheStore.cs`, `HistoryStore.cs` |
| Einstellungen und Verlauf | `SettingsWindow.xaml(.cs)`, `HistoryWindow.xaml(.cs)` |
| Systemintegration | `ToastService.cs`, `AutostartService.cs`, `ThemeService.cs` |
| Build und Installation | `Build.ps1`, `Install.ps1` |

## Bekannte Einschränkungen

- Der 7-Tage-Verlauf wird ausschließlich clientseitig aufgebaut und enthält anfangs noch keine historischen Daten.
- Nicht signierte Builds können eine Windows-SmartScreen-Warnung auslösen; Hinweise dazu stehen in [`SIGNING.md`](SIGNING.md).
- Die Anwendung ist ausschließlich für Windows vorgesehen.
- `TILageMonitor.ico` muss vor dem Build manuell im Projektstamm vorhanden sein, solange die Datei nicht im Repository enthalten ist.

## Fehlerbehebung

### Build schlägt wegen `TILageMonitor.ico` fehl

Die Projektdatei erwartet `TILageMonitor.ico` im Repository-Stamm. Lege dort eine gültige Windows-ICO-Datei ab und starte `Build.ps1` erneut.

### `dotnet` wird nicht gefunden

Installiere das .NET 8 SDK, öffne PowerShell neu und prüfe anschließend:

```powershell
dotnet --info
```

### `Install.ps1` findet die EXE nicht

Führe zuerst `Build.ps1` aus. Die Installation erwartet `publish\TILageMonitor.exe`.

### Benachrichtigungen erscheinen nicht

- Prüfe unter Windows, ob Benachrichtigungen und „Nicht stören“ passend konfiguriert sind.
- Prüfe in den App-Einstellungen den Benachrichtigungsfilter des betroffenen Dienstes.
- Wenn Windows-Toasts nicht verfügbar sind, versucht die App automatisch einen Balloon-Hinweis im Tray.

## Autor

**Randy Carter** · R.C.
