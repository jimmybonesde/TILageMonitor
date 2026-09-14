# Code signing policy

TI-Lage Monitor wird als Open-Source-Projekt aus diesem öffentlichen Repository veröffentlicht:

- Repository: https://github.com/jimmybonesde/TILageMonitor
- Releases: https://github.com/jimmybonesde/TILageMonitor/releases
- Lizenz: MIT

## Signatur

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

Signiert werden ausschließlich Windows-Installer, die aus dem Quellcode dieses Repositorys und dem versionierten Release-Workflow gebaut wurden. Jede veröffentlichte Version wird vor der Signierung durch den Release-Prozess geprüft und anschließend manuell zur Signierung freigegeben.

## Rollen

- Committer und Maintainer: Randy Carter (jimmybonesde)
- Reviewer: Änderungen von Dritten werden über GitHub-Pull-Requests geprüft.
- Approver: Randy Carter (jimmybonesde) gibt veröffentlichte Installer zur Signierung frei.

Für den GitHub-Account und SignPath ist aktivierte Mehrfaktor-Authentifizierung vorgesehen.

## Datenschutz

TI-Lage Monitor überträgt keine personenbezogenen Daten an andere Systeme, außer wenn dies ausdrücklich durch den Benutzer angefordert wird. Die Anwendung ruft ausschließlich die für die TI-Statusanzeige erforderlichen öffentlichen API-Daten ab. Lokale Einstellungen und Verlaufsdaten verbleiben auf dem Gerät.

## Deinstallation

Der Windows-Installer enthält eine reguläre Deinstallation über die Windows-Einstellungen beziehungsweise die Liste der installierten Apps.
