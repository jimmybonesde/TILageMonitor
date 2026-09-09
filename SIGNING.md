# Code Signing (optional)

Die mit `Build.ps1` erzeugte `publish\TILageMonitor.exe` ist **nicht** signiert.
Ohne gültiges Authenticode-Zertifikat kann Windows SmartScreen beim ersten Start warnen.

## Beispiel mit signtool (eigenes Zertifikat erforderlich)

```powershell
# Beispiel – Pfade und Zertifikat vom Herausgeber ersetzen.
# KEINE erfundenen Zertifikate oder Secrets verwenden.

signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com `
  /f "C:\path\to\your-code-signing.pfx" /p "<PFX-Passwort>" `
  ".\publish\TILageMonitor.exe"
```

Alternativ Signierung über ein im Zertifikatsspeicher vorhandenes Zertifikat:

```powershell
signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com `
  /n "Ihr Herausgebername" `
  ".\publish\TILageMonitor.exe"
```

## Hinweise

- SmartScreen und „Unbekannter Herausgeber“ verschwinden nur mit einem **echten** Code-Signing-Zertifikat (z. B. von DigiCert, Sectigo, GlobalSign) des Publishers.
- Dieses Repository enthält **keine** Zertifikate und keine privaten Schlüssel.
- Nach dem Signieren optional `signtool verify /pa .\publish\TILageMonitor.exe` prüfen.
