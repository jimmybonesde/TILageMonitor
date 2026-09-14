namespace TILageMonitor;

public static class UpdateInstallerLauncher
{
    public static string BuildScript(
        int processId,
        string installerPath,
        string applicationPath,
        string logPath,
        string? backupDirectory = null)
    {
        if (processId <= 0)
            throw new ArgumentOutOfRangeException(nameof(processId));

        var installer = RequireSafePath(installerPath, nameof(installerPath));
        var application = RequireSafePath(applicationPath, nameof(applicationPath));
        var log = RequireSafePath(logPath, nameof(logPath));
        var appDir = RequireSafePath(
            Path.GetDirectoryName(applicationPath) ?? throw new ArgumentException("Ungültiger Anwendungspfad.", nameof(applicationPath)),
            nameof(applicationPath));
        var backup = string.IsNullOrWhiteSpace(backupDirectory) ? null : RequireSafePath(backupDirectory, nameof(backupDirectory));

        var backupBlock = backup is null ? "" : $@"if not exist ""{backup}"" mkdir ""{backup}""
robocopy ""{appDir}"" ""{backup}"" /MIR /NFL /NDL /NJH /NJS /NP >nul
set ""backupExitCode=%ERRORLEVEL%""
if %backupExitCode% GEQ 8 (
  start """" ""{application}"" --update-failed
  del ""%~f0""
  exit /b 1
)
";
        var rollbackBlock = backup is null ? "" : $@"robocopy ""{backup}"" ""{appDir}"" /MIR /NFL /NDL /NJH /NJS /NP >nul
rmdir /s /q ""{backup}"" 2>nul
";
        var cleanupBlock = backup is null ? "" : $@"rmdir /s /q ""{backup}"" 2>nul
";

        return $@"@echo off
:waitforapp
tasklist /FI ""PID eq {processId}"" /NH | findstr /C:"" {processId} "" >nul
if not errorlevel 1 (
  timeout /t 1 /nobreak >nul
  goto waitforapp
)
{backupBlock}start """" /wait ""{installer}"" /SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /LOG=""{log}""
set ""updateExitCode=%ERRORLEVEL%""
if not ""%updateExitCode%""==""0"" (
  {rollbackBlock}start """" ""{application}"" --update-failed
) else (
  {cleanupBlock}
)
del ""%~f0""
exit /b %updateExitCode%
";
    }

    private static string RequireSafePath(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0)
            throw new ArgumentException("Der Pfad enthält ungültige Zeichen.", parameterName);
        return value;
    }
}
