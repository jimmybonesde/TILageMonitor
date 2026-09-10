$ErrorActionPreference = 'Stop'

Write-Host "=== TI-Lage Monitor Build ===" -ForegroundColor Cyan

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "dotnet wurde nicht gefunden. Installiere das .NET 8 SDK." -ForegroundColor Red
    exit 1
}

dotnet restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o .\publish
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build fehlgeschlagen." -ForegroundColor Red
    exit $LASTEXITCODE
}

# Icon neben der EXE (Tray + Install.ps1)
$IcoSrc = Join-Path $PWD 'TILageMonitor.ico'
$IcoDst = Join-Path $PWD 'publish\TILageMonitor.ico'
if (Test-Path -LiteralPath $IcoSrc) {
    Copy-Item -LiteralPath $IcoSrc -Destination $IcoDst -Force
}

Write-Host ""
Write-Host "Fertig:" -ForegroundColor Green
Write-Host (Join-Path $PWD "publish\TILageMonitor.exe")
