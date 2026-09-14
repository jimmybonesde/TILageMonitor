<p align="center">
  <img src="docs/assets/app-icon-128.png" alt="TI-Lage Monitor" width="160">
</p>

<h1 align="center">TI-Lage Monitor</h1>

<p align="center">
  <strong>Der TI-Status der gematik direkt im Windows-Tray – inklusive klarer 14-Tage-Historie.</strong><br>
  Schlank · lokal · ohne Login
</p>

<p align="center">
  <a href="https://github.com/jimmybonesde/TILageMonitor/releases/latest"><img src="https://img.shields.io/github/v/release/jimmybonesde/TILageMonitor?style=flat&label=Download&logo=github" alt="Aktueller Download"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat&logo=windows&logoColor=white" alt="Windows 10 und 11">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?style=flat&logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/Lizenz-Community-gray?style=flat" alt="Community-App">
</p>

<p align="center">
  <a href="https://github.com/jimmybonesde/TILageMonitor/releases/latest"><strong>⬇️ Neueste Version herunterladen</strong></a>
  ·
  <a href="#-in-60-sekunden-startklar">Schnellstart</a>
  ·
  <a href="#-funktionen">Funktionen</a>
  ·
  <a href="#-14-tage-verlauf">14-Tage-Verlauf</a>
  ·
  <a href="https://fachportal.gematik.de/ti-status#TI-Anschluss">gematik Fachportal</a>
</p>

> [!IMPORTANT]
> **Kein offizielles gematik-Produkt.** TI-Lage Monitor ist eine Community-App von Randy Carter (R.C.). Maßgeblich bleibt das [gematik Fachportal TI-Status](https://fachportal.gematik.de/ti-status#TI-Anschluss).

## 📸 Auf einen Blick

| Hellmodus | Dunkelmodus |
|:---:|:---:|
| ![Hauptfenster im Hellmodus](docs/assets/screenshot-light.png) | ![Hauptfenster im Dunkelmodus](docs/assets/screenshot-dark.png) |

Die Hero-Karte fasst die Lage zusammen, Dienste erscheinen als Status-Pills, aktuelle Meldungen darunter. Schließen (X) und Minimieren legen das Fenster nur in den Tray — das Monitoring läuft weiter. Nur **Beenden** beendet die App.

## 🚀 In 60 Sekunden startklar

1. [Neueste Version herunterladen](https://github.com/jimmybonesde/TILageMonitor/releases/latest).
2. **Empfohlen:** `TILageMonitor-x.y.z-Setup.exe` starten.
3. Installation abschließen — Startmenü-Eintrag und Desktop-Verknüpfung werden auf Wunsch erstellt.

> 💡 Der Setup-Installer ist vollständig offline und enthält .NET 10 sowie die Windows App SDK. Zusätzliche ZIP- oder Portable-Pakete werden nicht benötigt.

> [!WARNING]
> Die EXE ist derzeit **unsigniert**. Windows SmartScreen kann deshalb warnen. Details stehen in [SIGNING.md](SIGNING.md).

## ✨ Funktionen

| | Funktion | Was sie bringt |
| :--: | --- | --- |
| 🛡️ | **TI-Lage im Tray** | Schild mit grünem, amberfarbenem oder rotem Status-Badge |
| 🔎 | **Dienste im Blick** | eRezept, ePA, KIM, WANDA, OGD, VSDM und TI-Anschluss |
| 🔔 | **Windows-Benachrichtigungen** | Klickbare Toasts; Klick öffnet das Fenster und springt zum Kontext |
| 🕒 | **14-Tage-Verlauf** | Tages-Kacheln, Stunden-Zoom, Ereignisse-Timeline und Dienst-Filter |
| ⬇️ | **Selbst-Update** | Prüft GitHub Releases, lädt das Setup herunter und startet es |
| 🎨 | **Desktop-tauglich** | Fluent UI, Hell-/Dunkelmodus, Autostart, Start direkt in den Tray |
| ⚙️ | **Steuerbar** | Notify-Filter pro Dienst, globale Benachrichtigungen aus dem Tray |

### Statuslogik

| Status | Bedeutung |
| --- | --- |
| 🟢 **Grün** | Alle überwachten Dienste sind verfügbar |
| 🟠 **Amber** | Einschränkung oder Teilausfall |
| 🔴 **Rot** | Vollausfall oder kritische Störung |
| ⚪ **Keine Daten** | Öffentliche API nicht erreichbar oder noch kein Abruf erfolgt |

Die App ruft Lage, Incidents und Outages parallel ab und folgt dem gematik-Rhythmus mit Offset (`:01`, `:06`, `:11`, …). Nach drei aufeinanderfolgenden API-Fehlern zeigt sie einen dauerhaften „API down“-Status.

## 🖱️ Bedienung

| Aktion | Wirkung |
| --- | --- |
| Linksklick auf das Tray-Icon | Hauptfenster ein- oder ausblenden |
| Schließen (X) / Minimieren | Fenster in den Tray legen (Monitoring läuft weiter) |
| Rechtsklick → Benachrichtigungen | Alle Toasts global an oder aus |
| **Aktualisieren** | Status sofort neu laden |
| **Verlauf** | 14-Tage-Historie öffnen (maximiert) |
| **Einstellungen** | Theme, Autostart, Updates und Notify-Filter |
| **gematik** | Fachportal im Browser öffnen |
| **Beenden** | Anwendung beenden |

## 📈 14-Tage-Verlauf

Der 14-Tage-Verlauf ist in **2.0** fest integriert (gereift über 1.0.15–1.0.17): ruhige KPI-Übersicht, Fokus-Layout und klare Timelines statt einer überladenen Stunden-Heatmap für alle Dienste gleichzeitig.

### So ist die Ansicht aufgebaut

1. **KPI-Header**  
   Drei Karten oben: **Heute** (schlechtester Status), **14 Tage** (auffällige Tage mit Einschränkung, Störung oder Wartung) und **Verfügbarkeit %** (bevorzugt lokale Snapshots; sonst API-gefüllte OK-Stunden ohne zukünftige Stunden).

2. **Segmentierte Ansicht**  
   Umschalter **Übersicht | Stunden | Ereignisse** — dieselbe Datenbasis, drei Lesarten.

3. **Fokus-Layout (Standard)**  
   Standardfilter ist **Alle Dienste** (kompakte Zeilen). Ein Dienst-Chip fokussiert große **Tages-Kacheln**. Die **Ereignisse-Timeline** liegt im Segment **Ereignisse**.

4. **Timeline-Karten**  
   Einträge mit farbigem linken Status-Balken (z. B. `eRezept · Teilausfall · Di 10:00–12:00`). Klick springt in den **Stunden-Zoom** des betreffenden Tages. In der Stundenansicht öffnet ein Klick auf eine auffällige Stunde das passende Ereignis.

5. **Stabil / Alles ruhig**  
   Wenn für den Fokus keine Auffälligkeiten vorliegen, erscheint ein beruhigender Leer-/OK-Zustand statt einer leeren Liste.

6. **Mikro-Details**  
   Ausgewählter Tag mit Akzent-Rahmen, Tooltips an Kacheln und Ereignissen, weiche Ein-/Ausblendungen der Panels beim Segmentwechsel. Hell- und Dunkelmodus aktualisieren Farben, Schatten und Chips live.

### Datenquellen

Der Verlauf kombiniert zwei Quellen:

| Quelle | Was sie liefert |
| --- | --- |
| **gematik-API** (Incidents / Outages) | Gemeldete Einschränkungen und Statusschritte der letzten **14 Tage** — auch wenn der PC aus war |
| **Lokale Stunden-Snapshots** (`history.json`) | Ergänzen die Verfügbarkeit (grüne Online-Stunden), solange die App läuft |

> Graue Kacheln / Stunden bedeuten: Für diesen Zeitraum liegt weder ein lokaler Snapshot noch eine gemeldete API-Einschränkung vor — **zukünftige Stunden bleiben grau**. Wenn die Incident-API erreichbar ist, werden bereits begonnene Stunden ohne gemeldete Störung als verfügbar (grün) dargestellt. Das Verlaufsfenster bleibt in der Taskleiste nutzbar, auch wenn das Hauptfenster im Tray liegt.

### Kurzbedienung im Verlauf

| Aktion | Wirkung |
| --- | --- |
| Filter-Chip (eRezept, ePA, … / Alle) | Zeilen und Ereignisse eingrenzen |
| Tages-Kachel anklicken | Stunden-Zoom für diesen Tag |
| Ereignis anklicken | Zoom auf den Starttag des Ereignisses |
| Auffällige Stunde anklicken | Wechselt zu **Ereignisse** und markiert das passende Ereignis |
| Esc / Zurück | Zurück zur Tages-Übersicht |

## 🔐 Daten & Datenschutz

- Nur öffentliche gematik-APIs — **kein Login, kein API-Key**
- Keine Cloud-Synchronisation
- Alle Dateien liegen lokal unter `%AppData%\TILageMonitor\`

| Datei | Inhalt |
| --- | --- |
| `settings.json` | Theme, Autostart, Notify-Filter |
| `last-lage.json` | Offline-Cache des letzten Stands |
| `history.json` | Stündliche lokale Ergänzung zum 14-Tage-Verlauf |

Verwendete Endpunkte:

- `https://ti-lage.prod.ccs.gematik.solutions/lageapi/v2/tilage`
- `https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/incident`
- `https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/outage`

## ⚠️ Bekannte Grenzen

- Nur für Windows x64
- Eine unsignierte EXE kann SmartScreen-Warnungen auslösen
- Ausfälle stammen bis zu 14 Tage aus der API; vollständige OK-Verfügbarkeit wird zusätzlich lokal erfasst
- Die App ergänzt das gematik Fachportal, ersetzt es aber nicht

## 🗂️ Für Entwickler

### Aus dem Quellcode bauen

Voraussetzungen: Windows 10/11 x64, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) und PowerShell 5.1+.

```powershell
git clone https://github.com/jimmybonesde/TILageMonitor.git
cd TILageMonitor
.\Build.ps1
.\Install.ps1
```

| Bereich | Zentrale Dateien |
| --- | --- |
| UI & Tray | `MainWindow.*`, `App.xaml(.cs)` |
| API & Modelle | `ApiClient.cs`, `Models.cs` |
| Persistenz | `SettingsStore.cs`, `CacheStore.cs`, `HistoryStore.cs` |
| Verlauf | `HistoryWindow.*`, `HistoryTimelineBuilder.cs` |
| Fenster | `SettingsWindow.*`, `AboutWindow.*` |
| Systemdienste | `ToastService.cs`, `ToastRegistration.cs`, `ThemeService.cs`, `AutostartService.cs`, `UpdateService.cs` |
| Build & Release | `Build.ps1`, `Install.ps1`, `installer/TILageMonitor.iss`, `.github/workflows/release.yml` |

Technik: WPF + WinForms-Tray · `net10.0-windows10.0.17763.0` · Windows App SDK App Notifications · Inno Setup Installer

## 👤 Autor

**Randy Carter** · R.C. · © 2026

<p align="center">
  <sub>Für Praxen & IT, die den TI-Status im Blick behalten wollen — ohne einen Portal-Tab offen zu lassen.</sub>
</p>
