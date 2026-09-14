<p align="center">
  <img src="docs/assets/app-icon-128.png" alt="TI-Lage Monitor" width="144">
</p>

<h1 align="center">TI-Lage Monitor</h1>

<p align="center">
  <strong>gematik TI-Status im Windows-Tray — live, lokal, mit 14-Tage-Verlauf.</strong><br>
  <em>Schlank · ohne Login · Community-App</em>
</p>

<p align="center">
  <a href="https://github.com/jimmybonesde/TILageMonitor/releases/latest"><img src="https://img.shields.io/github/v/release/jimmybonesde/TILageMonitor?style=flat&label=Download&logo=github" alt="Latest release"></a>
  <a href="https://github.com/jimmybonesde/TILageMonitor/releases/latest"><img src="https://img.shields.io/github/downloads/jimmybonesde/TILageMonitor/total?style=flat&label=Downloads" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat&logo=windows&logoColor=white" alt="Windows 10 | 11">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?style=flat&logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/Lizenz-MIT-green?style=flat" alt="MIT">
  <a href="https://github.com/jimmybonesde/TILageMonitor/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/jimmybonesde/TILageMonitor/ci.yml?branch=main&style=flat&label=CI" alt="CI"></a>
</p>

<p align="center">
  <a href="https://github.com/jimmybonesde/TILageMonitor/releases/latest"><strong>⬇️ Download</strong></a>
  ·
  <a href="#-funktionen">Features</a>
  ·
  <a href="#-14-tage-verlauf">Verlauf</a>
  ·
  <a href="#-schnellstart">Start</a>
  ·
  <a href="#-für-entwickler">Entwickler</a>
</p>

> [!IMPORTANT]
> **Kein offizielles gematik-Produkt.** TI-Lage Monitor ist eine unabhängige Community-App von Randy Carter (R.C.). Maßgeblich bleibt das [gematik Fachportal TI-Status](https://fachportal.gematik.de/ti-status#TI-Anschluss).

## 📸 Auf einen Blick

| Hellmodus | Dunkelmodus |
|:---:|:---:|
| ![Hauptfenster Hellmodus](docs/assets/screenshot-light.png) | ![Hauptfenster Dunkelmodus](docs/assets/screenshot-dark.png) |

TI-Lage Monitor zeigt den aktuellen Status von **7 TI-Diensten** als Tray-Ampel und im Fluent-Hauptfenster — inkl. Meldungen, Toasts und einem ruhigen **14-Tage-Verlauf**. Alles läuft **lokal** auf Windows, ohne Login und ohne Cloud. **X** / Minimieren → Tray (Monitoring weiter); nur **Beenden** beendet die App.

## 💡 Warum TI-Lage Monitor?

- **Lokal & privat** — öffentliche gematik-APIs, kein Account, keine Sync-Cloud
- **Immer im Blick** — Tray-Ampel und Toasts, ohne einen Portal-Tab offen zu lassen
- **14-Tage-Verlauf** — Übersicht, Stunden und Ereignisse auf einer Datenbasis
- **Aktuell bleiben** — GitHub-Releases mit SHA-256-Prüfung und optionaler Auto-Installation

## ✨ Funktionen

| | Funktion | Nutzen |
| :--: | --- | --- |
| 🛡️ | **Tray-Ampel** | Grün / Amber / Rot / Keine Daten im Infobereich |
| 🔎 | **7 TI-Dienste** | eRezept, ePA, KIM, WANDA, OGD, VSDM, TI-Anschluss |
| 🔔 | **Windows-Toasts** | Klickbare Benachrichtigungen mit Kontextsprung |
| 📈 | **14-Tage-Verlauf** | Übersicht · Stunden · Ereignisse — KPI, Fokus-Chips, Timeline |
| ⬇️ | **GitHub-Updates** | Releases prüfen, SHA-256 verifizieren, optional still installieren |
| 🎨 | **Fluent UI** | Hell-/Dunkelmodus, Autostart, Start direkt in den Tray |
| ⚙️ | **Notify-Filter** | Pro Dienst und global aus dem Tray steuerbar |

**Statusfarben**

| Status | Bedeutung |
| --- | --- |
| 🟢 **Grün** | Überwachte Dienste verfügbar |
| 🟠 **Amber** | Einschränkung oder Teilausfall |
| 🔴 **Rot** | Vollausfall oder kritische Störung |
| ⚪ **Keine Daten** | API nicht erreichbar oder noch kein Abruf |

Lage (tilage **v2**), Incidents (**v1**) und Outages (**v1**) von `ti-lage.prod.ccs.gematik.solutions` — Abrufe im gematik-Rhythmus mit Offset (`:01`, `:06`, `:11`, …). Nach **drei aufeinanderfolgenden** API-Fehlern: dauerhaft **API down**.

## 🚀 Schnellstart

1. [Neueste Version herunterladen](https://github.com/jimmybonesde/TILageMonitor/releases/latest) (`TILageMonitor-*-Setup.exe`).
2. Setup starten und Installation abschließen.
3. Optional: Autostart, Theme und Sprache unter **Einstellungen**.

> [!TIP]
> Der Installer ist offline und bringt .NET 10 sowie Windows App SDK mit. Unter **Einstellungen → Updates** lässt sich die optionale stille Auto-Installation aktivieren; der Download wird per **SHA-256** geprüft (nur GitHub-URLs).

> [!WARNING]
> Die EXE ist derzeit **unsigniert**. Windows SmartScreen kann warnen. Details und Signatur-Plan: [SIGNING.md](SIGNING.md).

## 🖱️ Bedienung

| Aktion | Wirkung |
| --- | --- |
| Linksklick Tray-Icon | Hauptfenster ein-/ausblenden |
| Schließen (X) / Minimieren | Nur in den Tray (Monitoring weiter) |
| Rechtsklick → Benachrichtigungen | Toasts global an/aus |
| **Aktualisieren** | Status sofort neu laden |
| **Verlauf** | 14-Tage-Historie (maximiert) |
| **Einstellungen** | Theme, Sprache, Autostart, Updates, Filter |
| **gematik** | Fachportal im Browser |
| **Beenden** | App beenden |

## 📈 14-Tage-Verlauf

Drei Segmente auf derselben Datenbasis: **Übersicht | Stunden | Ereignisse**. v2.x behandelt den Verlauf als First-Class-Feature — ruhige KPI, Fokus-Layout und klare Timelines.

| Baustein | Inhalt |
| --- | --- |
| **KPI** | **Heute** · **14 Tage** (auffällige Tage) · **Verfügbarkeit %** |
| **Fokus-Chips** | Alle Dienste oder ein Dienst — Zeilen bzw. große Tages-Kacheln |
| **Timeline** | Karten mit Status-Balken; Klick zoomt in Stunden / markiert Ereignisse |
| **Quellen** | gematik Incidents/Outages (auch bei ausgeschaltetem PC) + lokale Snapshots (`history.json`) für Online-Stunden |
| **Leer/OK** | „Alles ruhig“ statt leerer Listen; Hell/Dunkel aktualisiert live |

> [!NOTE]
> Graue Kacheln/Stunden = weder lokaler Snapshot noch API-Meldung (zukünftige Stunden bleiben grau). Bei erreichbarer Incident-API gelten begonnene Stunden ohne Störung als verfügbar (grün).

| Tipps | Wirkung |
| --- | --- |
| Filter-Chip | Zeilen und Ereignisse eingrenzen |
| Tages-Kachel | Stunden-Zoom |
| Ereignis / auffällige Stunde | Zoom bzw. Segment **Ereignisse** |
| Esc / Zurück | Zurück zur Tages-Übersicht |

## 🔐 Daten & Datenschutz

- Nur öffentliche gematik-APIs — **kein Login, kein API-Key**
- Keine Cloud-Synchronisation
- Lokal unter `%AppData%\TILageMonitor\`
- Updates nur von allowlisted GitHub-Hosts (`github.com`, `*.githubusercontent.com`)

| Datei | Inhalt |
| --- | --- |
| `settings.json` | Theme, Sprache, Autostart, Notify-Filter, Updates |
| `last-lage.json` | Offline-Cache des letzten Stands |
| `history.json` | Stündliche lokale Ergänzung zum Verlauf |

<details>
<summary><strong>API-Endpunkte</strong></summary>

- `https://ti-lage.prod.ccs.gematik.solutions/lageapi/v2/tilage`
- `https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/incident`
- `https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/outage`

</details>

## ⚠️ Bekannte Grenzen

- Nur Windows **x64** (10 / 11)
- Unsignierte EXE → mögliche SmartScreen-Warnung ([SIGNING.md](SIGNING.md))
- API deckt Störungen bis 14 Tage ab; vollständige OK-Verfügbarkeit braucht lokale Snapshots
- Ergänzt das Fachportal — ersetzt es nicht

## 🗂️ Für Entwickler

**Voraussetzungen:** Windows 10/11 x64 · [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) · PowerShell 5.1+

```powershell
git clone https://github.com/jimmybonesde/TILageMonitor.git
cd TILageMonitor
.\Build.ps1
.\Install.ps1
```

| Bereich | Dateien |
| --- | --- |
| UI & Tray | `MainWindow.*`, `App.xaml(.cs)` |
| API & Modelle | `ApiClient.cs`, `Models.cs`, `TiStatusClassifier.cs` |
| Persistenz | `*Store.cs`, `AtomicJsonStore.cs` |
| Verlauf | `HistoryWindow.*`, `HistoryTimelineBuilder.cs` |
| Fenster / System | `SettingsWindow.*`, `AboutWindow.*`, `ToastService.cs`, `ThemeService.cs`, `AutostartService.cs`, `UpdateService.cs`, `LocalizationService.cs` |
| Build / Release | `Build.ps1`, `Install.ps1`, `installer/`, `.github/workflows/` |

**Stack:** WPF + WinForms-Tray · `net10.0-windows` · Windows App SDK Toasts · Inno Setup · MIT

## 👤 Autor

**Randy Carter** (R.C.) · © 2026 · Community-App, nicht gematik

<p align="center">
  <sub>Für Praxen &amp; IT, die den TI-Status im Blick behalten — ohne einen Portal-Tab offen zu lassen.</sub>
</p>

---

## 🌐 Sprache / Language

**DE:** Unter **Einstellungen → Sprache** wählen Sie **Automatisch (Windows)** · **Deutsch** · **Englisch**. Die Auswahl greift nach einem **Neustart** der App.

**EN:** Under **Settings → Language** choose **Automatic (Windows)** · **German** · **English**. The selection takes effect after **restarting** the app.
