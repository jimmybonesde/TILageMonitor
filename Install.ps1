#Requires -Version 5.1
<#
.SYNOPSIS
  Installiert TI-Lage Monitor nach %LocalAppData%\TILageMonitor\

.DESCRIPTION
  Idempotent: kopiert die veröffentlichte EXE (+ Icon), legt Startmenü-Verknüpfung an,
  optional Desktop-Verknüpfung und optional Autostart (Run-Key).

.PARAMETER DesktopShortcut
  Erstellt zusätzlich eine Desktop-Verknüpfung.

.PARAMETER EnableAutostart
  Schreibt den Autostart-Run-Key (Start mit --tray). Alternativ in der App unter „Autostart“.

.EXAMPLE
  .\Install.ps1
  .\Install.ps1 -DesktopShortcut -EnableAutostart
#>
[CmdletBinding()]
param(
    [switch]$DesktopShortcut,
    [switch]$EnableAutostart
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PublishExe = Join-Path $ScriptDir 'publish\TILageMonitor.exe'
$PublishIco = Join-Path $ScriptDir 'TILageMonitor.ico'

if (-not (Test-Path -LiteralPath $PublishExe)) {
    Write-Host "Nicht gefunden: $PublishExe" -ForegroundColor Red
    Write-Host "Bitte zuerst Build.ps1 ausführen (dotnet publish → .\publish)." -ForegroundColor Yellow
    exit 1
}

$InstallDir = Join-Path $env:LOCALAPPDATA 'TILageMonitor'
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

$TargetExe = Join-Path $InstallDir 'TILageMonitor.exe'
$TargetIco = Join-Path $InstallDir 'TILageMonitor.ico'

Write-Host "Installiere nach: $InstallDir" -ForegroundColor Cyan
Copy-Item -LiteralPath $PublishExe -Destination $TargetExe -Force

if (Test-Path -LiteralPath $PublishIco) {
    Copy-Item -LiteralPath $PublishIco -Destination $TargetIco -Force
}

function New-Shortcut {
    param(
        [Parameter(Mandatory)][string]$ShortcutPath,
        [Parameter(Mandatory)][string]$TargetPath,
        [string]$WorkingDirectory,
        [string]$IconLocation,
        [string]$Description = 'TI-Lage Monitor'
    )
    $shell = New-Object -ComObject WScript.Shell
    $sc = $shell.CreateShortcut($ShortcutPath)
    $sc.TargetPath = $TargetPath
    if ($WorkingDirectory) { $sc.WorkingDirectory = $WorkingDirectory }
    if ($IconLocation -and (Test-Path -LiteralPath $IconLocation)) {
        $sc.IconLocation = "$IconLocation,0"
    }
    $sc.Description = $Description
    $sc.Save()
}

# Startmenü
$StartMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
New-Item -ItemType Directory -Force -Path $StartMenuDir | Out-Null
$StartMenuLnk = Join-Path $StartMenuDir 'TI-Lage Monitor.lnk'
New-Shortcut -ShortcutPath $StartMenuLnk -TargetPath $TargetExe `
    -WorkingDirectory $InstallDir -IconLocation $TargetIco
Write-Host "Startmenü: $StartMenuLnk" -ForegroundColor Green

if ($DesktopShortcut) {
    $DesktopLnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'TI-Lage Monitor.lnk'
    New-Shortcut -ShortcutPath $DesktopLnk -TargetPath $TargetExe `
        -WorkingDirectory $InstallDir -IconLocation $TargetIco
    Write-Host "Desktop: $DesktopLnk" -ForegroundColor Green
}

if ($EnableAutostart) {
    $RunKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    Set-ItemProperty -Path $RunKey -Name 'TILageMonitor' -Value "`"$TargetExe`" --tray" -Type String
    Write-Host "Autostart (Run-Key) aktiviert." -ForegroundColor Green
}
else {
    Write-Host "Autostart: in der App unter „Autostart“ einschalten oder -EnableAutostart nutzen." -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Fertig. Starten mit:" -ForegroundColor Green
Write-Host "  $TargetExe"
Write-Host "Einstellungen: %AppData%\TILageMonitor\settings.json"
