# TI-Lage Monitor

Windows-Tray-App zur Überwachung der öffentlichen **gematik TI-Lage** (eRezept, ePA, KIM, WANDA, OGD, VSDM, TI-Anschluss).

**Voraussetzungen:** Windows 10/11, .NET 8 SDK

```powershell
dotnet restore
.\Build.ps1
```

Die EXE liegt unter `publish\TILageMonitor.exe`. Optional: `.\Install.ps1`

---

## Funktionen

- **TI-Lage V2**: eRezept, ePA, KIM, WANDA, OGD, VSDM, TI-Anschluss
- **Tray-Icon** App-Icon mit Ampel-Badge (OK / Beeinträchtigung / Störung)
- **Windows-Toasts** (Microsoft.Toolkit.Uwp.Notifications) bei Statusänderungen, Incidents, API-Down und „Alles wieder OK“ — Klick öffnet das Hauptfenster; bei Fehlern stiller Fallback auf NotifyIcon-Ballons
- **Einstellungen-Fenster** (Darstellung / Autostart / Benachrichtigungsfilter pro Dienst)
- **Fluent-UI-Politur** (größere Radien, Kartenränder, Accent-Blau, Dark/Light-Paletten)
- **Pro-Dienst-Filter** unter Einstellungen → Benachrichtigungen (Tray-Farbe bleibt global)
- **Einzelinstanz**: zweite Starts aktivieren das bestehende Fenster
- **Offline-Cache**: bei API-Ausfall Anzeige des letzten Standes (`last-lage.json`)
- **TI-Status · 7 Tage**: clientseitiger Verlauf (baut sich über die Laufzeit auf)
- Dunkelmodus, Autostart, Fachportal-Link
- Automatische Aktualisierung im gematik-Rhythmus (~xx:01, xx:06, …)
- Kein Login / API-Key notwendig

## 7-Tage-Verlauf (clientseitig)

Die öffentlichen gematik-APIs liefern **kein** mehrtägiges Zeitreihen-Historie (Outages = aktuelle/kurze Slots, Lage V2 = Punktstand).  
Deshalb speichert die App bei jedem erfolgreichen Refresh einen **stündlichen Snapshot** unter:

`%AppData%\TILageMonitor\history.json`

Pro Dienst wird der schlechteste Status der aktuellen lokalen Stunde gehalten (`none` / `partial` / `full` / `maintenance`).  
Angezeigt werden 7 Kalendertage × 24 Stunden; fehlende Stunden erscheinen grau („kein Datenpunkt“).  
Der Verlauf füllt sich, **solange die App regelmäßig läuft**.

## Einstellungen & Cache

| Datei | Pfad |
|--------|------|
| Einstellungen | `%AppData%\TILageMonitor\settings.json` |
| Offline-Cache | `%AppData%\TILageMonitor\last-lage.json` |
| 7-Tage-Verlauf | `%AppData%\TILageMonitor\history.json` |

`settings.json` enthält u. a. `darkMode`, `autoStart` und `notifyServices` (Dictionary je Dienst).  
Die In-App-Oberfläche **Einstellungen** (Header-Button oder Tray-Menü) speichert Änderungen sofort.

## Voraussetzungen

Windows 10/11 und .NET 8 SDK zum Bauen.

## Bauen

```powershell
.\Build.ps1
```

oder manuell:

```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

Die EXE liegt danach unter `publish\TILageMonitor.exe`.

## Installation

Nach dem Publish:

```powershell
.\Install.ps1
.\Install.ps1 -DesktopShortcut -EnableAutostart
```

Kopiert die EXE nach `%LocalAppData%\TILageMonitor\`, legt eine Startmenü-Verknüpfung an und optional Desktop/Autostart.

## Code Signing

Siehe [SIGNING.md](SIGNING.md). Ohne echtes Authenticode-Zertifikat kann SmartScreen warnen.

## API

Öffentliche gematik-Endpunkte:

- https://ti-lage.prod.ccs.gematik.solutions/lageapi/v2/tilage
- https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/incident
- https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/outage

Die gematik aktualisiert das Lagebild im 5-Minuten-Rhythmus; die App fragt mit leichtem Offset ab (Slots :01, :06, :11, …).

## Tray-Menü

- TI-Lage anzeigen / Jetzt aktualisieren / gematik TI-Status öffnen
- Über TI-Lage Monitor… / Einstellungen… / Autostart: an|aus
- Beenden

Autostart startet mit `--tray` (Fenster zunächst im Tray).
