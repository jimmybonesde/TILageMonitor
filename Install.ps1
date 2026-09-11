#Requires -Version 5.1
<#
.SYNOPSIS
  Installiert TI-Lage Monitor nach %LocalAppData%\TILageMonitor\

.DESCRIPTION
  Idempotent: kopiert die veröffentlichte EXE (+ Icon), legt Startmenü-Verknüpfung an,
  optional Desktop-Verknüpfung und optional Autostart (Run-Key).
  Die Startmenü-Verknüpfung erhält den AppUserModelId RandyCarter.TILageMonitor
  (PKEY_AppUserModel_ID) — zusätzlich setzt ToastRegistration denselben AUMID beim ersten Start.

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
$ReleaseExe = Join-Path $ScriptDir 'TILageMonitor.exe'
$PublishIco = Join-Path $ScriptDir 'TILageMonitor.ico'
$AppUserModelId = 'RandyCarter.TILageMonitor'

if (-not (Test-Path -LiteralPath $PublishExe)) {
    $PublishExe = $ReleaseExe
}

if (-not (Test-Path -LiteralPath $PublishExe)) {
    Write-Host "Nicht gefunden: $PublishExe" -ForegroundColor Red
    Write-Host "Bitte entweder Build.ps1 ausführen oder das Release-ZIP vollständig entpacken." -ForegroundColor Yellow
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

function Set-ShortcutAumid {
    param([string]$ShortcutPath, [string]$Aumid)
    # Compact PropertyStore stamp (PKEY_AppUserModel_ID)
    $code = @'
using System; using System.Runtime.InteropServices; using System.Runtime.InteropServices.ComTypes; using System.Text;
public static class AumidHelper {
  public static void Set(string path, string aumid) {
    var link = (IShellLinkW)new CShellLink();
    try {
      ((IPersistFile)link).Load(path, 0);
      var store = (IPropertyStore)link;
      var pv = new PropVariant(aumid);
      try { var k = Key; store.SetValue(ref k, pv); store.Commit(); }
      finally { pv.Clear(); }
      ((IPersistFile)link).Save(path, true);
    } finally { Marshal.FinalReleaseComObject(link); }
  }
  static PropertyKey Key = new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
  [ComImport, Guid("00021401-0000-0000-C000-000000000046")] class CShellLink {}
  [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
  interface IShellLinkW {
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder f, int c, IntPtr p, uint flags);
    void GetIDList(out IntPtr ppidl); void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder n, int c); void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string n);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder d, int c); void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string d);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder a, int c); void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string a);
    void GetHotkey(out short h); void SetHotkey(short h); void GetShowCmd(out int s); void SetShowCmd(int s);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder i, int c, out int idx); void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string i, int idx);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string p, uint r); void Resolve(IntPtr h, uint f); void SetPath([MarshalAs(UnmanagedType.LPWStr)] string p);
  }
  [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
  interface IPropertyStore {
    void GetCount(out uint c); void GetAt(uint i, out PropertyKey k); void GetValue(ref PropertyKey k, PropVariant v); void SetValue(ref PropertyKey k, PropVariant v); void Commit();
  }
  [StructLayout(LayoutKind.Sequential, Pack = 4)] struct PropertyKey { public Guid fmtid; public uint pid; public PropertyKey(Guid f, uint p){fmtid=f;pid=p;} }
  [StructLayout(LayoutKind.Sequential)] sealed class PropVariant {
    ushort vt; ushort r1,r2,r3; IntPtr ptr;
    public PropVariant(string v){ vt=31; ptr=Marshal.StringToCoTaskMemUni(v); }
    public void Clear(){ PropVariantClear(this); vt=0; ptr=IntPtr.Zero; }
    [DllImport("ole32.dll")] static extern int PropVariantClear([In, Out] PropVariant p);
  }
}
'@
    if (-not ('AumidHelper' -as [type])) { Add-Type -TypeDefinition $code }
    [AumidHelper]::Set($ShortcutPath, $Aumid)
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
    try {
        Set-ShortcutAumid -ShortcutPath $ShortcutPath -Aumid $script:AppUserModelId
        Write-Host "  AUMID: $script:AppUserModelId" -ForegroundColor DarkGray
    } catch {
        Write-Host "  AUMID-Hinweis: wird beim ersten App-Start gesetzt." -ForegroundColor Yellow
    }
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
