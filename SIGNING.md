# Code Signing für Releases

Ohne ein gültiges Authenticode-Zertifikat zeigt Windows für einen unbekannten Herausgeber gegebenenfalls SmartScreen an. Das Repository enthält absichtlich weder Zertifikate noch private Schlüssel.

## Einmalig in GitHub einrichten

1. Ein echtes Code-Signing-Zertifikat als PFX besorgen (z. B. von DigiCert, Sectigo oder GlobalSign).
2. Die PFX-Datei lokal in Base64 umwandeln:

   ```powershell
   [Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\Pfad\TILageMonitor.pfx")) |
     Set-Clipboard
   ```

3. Unter **Repository → Settings → Secrets and variables → Actions** diese Secrets anlegen:

   - `WINDOWS_CERTIFICATE_BASE64` – der Base64-Inhalt der PFX
   - `WINDOWS_CERTIFICATE_PASSWORD` – das PFX-Passwort

Danach signiert der Release-Workflow den Installer, prüft die Signatur mit `signtool verify /pa` und erstellt erst anschließend die SHA-256-Prüfsumme.

## Verhalten ohne Zertifikat

Der Workflow bleibt funktionsfähig und erzeugt weiterhin einen Installer mit Prüfsumme, veröffentlicht ihn aber unsigniert. Die Update-Funktion akzeptiert ausschließlich die signierte Prüfsumme der Release-Datei. Für eine echte Herausgeber-Identität und SmartScreen-Reputation muss das oben beschriebene Zertifikat hinterlegt werden.

## Update-Prüfung

Jedes Update wird per SHA-256-Prüfsumme verifiziert. Wenn der Installer Authenticode-signiert ist, prüft die App zusätzlich, ob das Zertifikat lesbar und zeitlich gültig ist. Unsigned Installer bleiben aus Kompatibilitätsgründen zulässig; die Prüfsumme bleibt dann der primäre Integritätsschutz.
