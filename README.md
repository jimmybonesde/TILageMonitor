<p align="center">
  <img src="docs/assets/app-icon-128.png" alt="TI-Lage Monitor" width="160">
</p>

<h1 align="center">TI-Lage Monitor</h1>

<p align="center">
  <strong>Der TI-Status der gematik direkt im Windows-Tray.</strong><br>
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
  <a href="https://fachportal.gematik.de/ti-status#TI-Anschluss">gematik Fachportal</a>
</p>

> [!IMPORTANT]
> **Kein offizielles gematik-Produkt.** TI-Lage Monitor ist eine Community-App von Randy Carter (R.C.). Maßgeblich bleibt das [gematik Fachportal TI-Status](https://fachportal.gematik.de/ti-status#TI-Anschluss).

## 📸 Auf einen Blick

| Hellmodus | Dunkelmodus |
|:---:|:---:|
| ![Hauptfenster im Hellmodus](docs/assets/screenshot-light.png) | ![Hauptfenster im Dunkelmodus](docs/assets/screenshot-dark.png) |

Die Hero-Karte fasst die Lage zusammen, Dienste erscheinen als Status-Pills, aktuelle Meldungen darunter. Das Tray-Icon zeigt den Zustand auch dann, wenn das Fenster geschlossen ist.

## 🚀 In 60 Sekunden startklar

1. [Neueste Version herunterladen](https://github.com/jimmybonesde/TILageMonitor/releases/latest).
2. **Empfohlen:** `TILageMonitor-x.y.z-Setup.exe` starten.
3. Installation abschließen — Startmenü-Eintrag und Desktop-Verknüpfung werden auf Wunsch erstellt.

> 💡 Der Setup-Installer ist vollständig offline und enthält .NET 10 sowie die Windows App SDK. Für einen portablen Start gibt es zusätzlich die komprimierte `TILageMonitor.exe` und das ZIP-Paket.

> [!WARNING]
> Die EXE ist derzeit **unsigniert**. Windows SmartScreen kann deshalb warnen. Details stehen in [SIGNING.md](SIGNING.md).

## ✨ Funktionen

| | Funktion | Was sie bringt |
| :--: | --- | --- |
| 🛡️ | **TI-Lage im Tray** | Schild mit grünem, amberfarbenem oder rotem Status-Badge |
| 🔎 | **Dienste im Blick** | eRezept, ePA, KIM, WANDA, OGD, VSDM und TI-Anschluss |
| 🔔 | **Windows-Benachrichtigungen** | Klickbare Toasts bei Störung, Teilausfall, API-Ausfall und Entwarnung |
| 🕒 | **7-Tage-Verlauf** | Lokale Heatmap mit Tages-Zoom und Abdeckung pro Stunde |
| 🎨 | **Desktop-tauglich** | Fluent UI, Hell-/Dunkelmodus, Autostart und Einzelinstanz |
| ⚙️ | **Steuerbar** | Refresh, Fachportal-Link und Notify-Filter pro Dienst direkt aus der App |

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
| **Aktualisieren** | Status sofort neu laden |
| **Verlauf** | Lokale 7-Tage-Heatmap öffnen |
| **Einstellungen** | Theme, Autostart und Benachrichtigungsfilter ändern |
| **gematik** | Fachportal im Browser öffnen |
| **Beenden** | Anwendung schließen |

## 📈 Lokaler 7-Tage-Verlauf

Die öffentlichen gematik-APIs stellen keine lange Zeitreihe bereit. Deshalb legt die App stündlich einen lokalen Snapshot ab und bildet daraus eine **7 × 24-Heatmap**.

- Tages-Zoom; mit `Esc` zurück zur Wochenansicht
- Wochentags-Köpfe, Stundenachse und Abdeckungsanzeige `N / 168 Stunden`
- Legende: OK · Einschränkung · Störung · Wartung · keine Daten

> Der Verlauf füllt sich nur, während die App läuft.

## 🧰 Installation für den Alltag

### Installer verwenden

Öffne PowerShell im heruntergeladenen Projektordner und führe aus:

```powershell
.\Install.ps1
.\Install.ps1 -DesktopShortcut -EnableAutostart
```

Die Installation legt die App unter `%LocalAppData%\TILageMonitor\` ab und erstellt einen Startmenü-Eintrag.

### Aus dem Quellcode bauen

Voraussetzungen: Windows 10/11 x64, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) und PowerShell 5.1+.

```powershell
git clone https://github.com/jimmybonesde/TILageMonitor.git
cd TILageMonitor
.\Build.ps1
.\Install.ps1
```

## 🔐 Daten & Datenschutz

- Nur öffentliche gematik-APIs — **kein Login, kein API-Key**
- Keine Cloud-Synchronisation
- Alle Dateien liegen lokal unter `%AppData%\TILageMonitor\`

| Datei | Inhalt |
| --- | --- |
| `settings.json` | Theme, Autostart, Notify-Filter |
| `last-lage.json` | Offline-Cache des letzten Stands |
| `history.json` | Stündlicher 7-Tage-Verlauf |

Verwendete Endpunkte:

- `https://ti-lage.prod.ccs.gematik.solutions/lageapi/v2/tilage`
- `https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/incident`
- `https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/outage`

## ⚠️ Bekannte Grenzen

- Nur für Windows x64
- Eine unsignierte EXE kann SmartScreen-Warnungen auslösen
- Der Verlauf entsteht erst, wenn die App läuft
- Die App ergänzt das gematik Fachportal, ersetzt es aber nicht

## 🗂️ Für Entwickler

| Bereich | Zentrale Dateien |
| --- | --- |
| UI & Tray | `MainWindow.*`, `App.xaml(.cs)` |
| API & Modelle | `ApiClient.cs`, `Models.cs` |
| Persistenz | `SettingsStore.cs`, `CacheStore.cs`, `HistoryStore.cs` |
| Fenster | `SettingsWindow.*`, `HistoryWindow.*` |
| Systemdienste | `ToastService.cs`, `ToastRegistration.cs`, `ThemeService.cs`, `AutostartService.cs` |
| Build & Release | `Build.ps1`, `Install.ps1`, `.github/workflows/release.yml` |

Technik: WPF + WinForms-Tray · `net10.0-windows10.0.17763.0` · Windows App SDK App Notifications

## 👤 Autor

**Randy Carter** · R.C. · © 2026

<p align="center">
  <sub>Für Praxen & IT, die den TI-Status im Blick behalten wollen — ohne einen Portal-Tab offen zu lassen.</sub>
</p>
