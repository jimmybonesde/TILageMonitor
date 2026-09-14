namespace TILageMonitor;

/// <summary>
/// Produces the tiny, detached Windows command helper for a silent Inno Setup update.
/// Keeping it deterministic makes the unattended update path testable.
/// </summary>
public static class UpdateInstallerLauncher
{
    public static string BuildScript(
        int processId,
        string installerPath,
        string applicationPath,
        string logPath)
    {
        if (processId <= 0)
            throw new ArgumentOutOfRangeException(nameof(processId));

        return $"""
            @echo off
            :waitforapp
            tasklist /FI "PID eq {processId}" /NH | findstr /C:" {processId} " >nul
            if not errorlevel 1 (
              timeout /t 1 /nobreak >nul
              goto waitforapp
            )
            start "" /wait "{RequireSafePath(installerPath, nameof(installerPath))}" /SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /LOG="{RequireSafePath(logPath, nameof(logPath))}"
            set "updateExitCode=%ERRORLEVEL%"
            if not "%updateExitCode%"=="0" (
              start "" "{RequireSafePath(applicationPath, nameof(applicationPath))}" --update-failed
            )
            del "%~f0"
            exit /b %updateExitCode%
            """;
    }

    private static string RequireSafePath(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.IndexOfAny(['"', '\r', '\n']) >= 0)
        {
            throw new ArgumentException("Der Pfad enthält ungültige Zeichen.", parameterName);
        }

        return value;
    }
}
