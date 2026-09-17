using System.Threading;
using System.Windows;

namespace TILageMonitor;

public partial class App : System.Windows.Application
{
    /// <summary>Preferred cross-session mutex; falls back to Local\ when Global\ is denied.</summary>
    public const string PreferredMutexName = @"Global\TILageMonitor_SingleInstance";
    public const string FallbackMutexName = @"Local\TILageMonitor_SingleInstance";
    public const string PreferredShowWindowEventName = @"Global\TILageMonitor_ShowWindow";
    public const string FallbackShowWindowEventName = @"Local\TILageMonitor_ShowWindow";

    /// <summary>
    /// Shared Global\ or Local\ prefix chosen once for both the single-instance mutex
    /// and the show-window event (avoids split-brain across namespaces).
    /// </summary>
    public static string SyncObjectNamespace { get; private set; } = @"Global\";

    /// <summary>Actual show-window event name in use (same namespace as the mutex).</summary>
    public static string ShowWindowEventName { get; private set; } = PreferredShowWindowEventName;

    /// <summary>Actual single-instance mutex name in use (same namespace as the event).</summary>
    public static string MutexName { get; private set; } = PreferredMutexName;

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showWindowEvent;
    private MainWindow? _window;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One namespace decision for mutex + event (prefer Global\; matches Inno AppMutex).
        // Local\ fallback: installer only declares Global\; CloseApplications still covers Local instances.
        _singleInstanceMutex = CreateCoupledSyncObjects(out var createdNew, out var showEvent);
        _showWindowEvent = showEvent;
        _ownsMutex = createdNew;

        if (!createdNew)
        {
            TrySignalShowWindow();
            Shutdown();
            return;
        }

        // Autostart writes "--tray". Startup is already tray-first (Show then Hide);
        // parse known args so future flags can land without breaking the Run key.
        foreach (var arg in e.Args)
        {
            if (string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase))
            {
                // no-op today — reserved for start-visible modes later
            }
        }

        var settings = SettingsStore.Load();
        LocalizationService.Configure(settings.Language);
        ThemeService.ApplyTheme(settings.DarkMode);

        var failedUpdateVersion = GetArgumentValue(e.Args, "--failed-update-version");
        if (e.Args.Any(arg => string.Equals(arg, "--update-failed", StringComparison.OrdinalIgnoreCase)) &&
            Version.TryParse(failedUpdateVersion, out _))
        {
            settings.LastAutoInstallFailureVersion = failedUpdateVersion;
            settings.AutoInstallRetryAfterUtc = AutoUpdateRetryPolicy.GetNextRetryUtc(DateTime.UtcNow);
            SettingsStore.Save(settings);
        }
        else if (e.Args.Any(arg => string.Equals(arg, "--updated", StringComparison.OrdinalIgnoreCase)))
        {
            settings.LastAutoInstallFailureVersion = null;
            settings.AutoInstallRetryAfterUtc = null;
            SettingsStore.Save(settings);
        }

        if (!SettingsStore.Exists)
            SettingsStore.Save(settings);

        AutostartService.SetEnabled(settings.AutoStart);

        _window = new MainWindow();

        ToastService.Initialize(focusKey =>
        {
            try
            {
                _window?.HandleToastActivation(focusKey);
            }
            catch
            {
                // Window activation is best-effort.
            }
        });

        _window.ShowInTaskbar = false;
        _window.Show();
        _window.Hide();

        if (e.Args.Any(arg => string.Equals(arg, "--update-failed", StringComparison.OrdinalIgnoreCase)))
        {
            Dispatcher.BeginInvoke(_window.ShowUpdateFailureAfterRestart);
        }
    }

    /// <summary>
    /// Prefer Global\ (cross-session; aligns with Inno AppMutex). On denial, use Local\
    /// for both mutex and show-window event so they never diverge.
    /// </summary>
    public static string ChooseSyncObjectNamespace()
    {
        try
        {
            // Probe Global\ accessibility without claiming single-instance ownership.
            using var probe = new Mutex(initiallyOwned: false, PreferredMutexName);
            return @"Global\";
        }
        catch (UnauthorizedAccessException)
        {
            // Restricted environments may deny Global\ — Local\ still prevents same-session dupes.
            // Installer AppMutex is Global-only; CloseApplications covers Local\ instances.
            return @"Local\";
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return @"Local\";
        }
    }

    /// <summary>
    /// Create mutex + show-window event under the same chosen namespace.
    /// </summary>
    private static Mutex CreateCoupledSyncObjects(out bool createdNew, out EventWaitHandle showEvent)
    {
        // Prefer Global\; on UnauthorizedAccessException / cannot-open → Local\ for BOTH.
        try
        {
            return CreateSyncObjectsInNamespace(@"Global\", out createdNew, out showEvent);
        }
        catch (UnauthorizedAccessException)
        {
            return CreateSyncObjectsInNamespace(@"Local\", out createdNew, out showEvent);
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return CreateSyncObjectsInNamespace(@"Local\", out createdNew, out showEvent);
        }
    }

    private static Mutex CreateSyncObjectsInNamespace(
        string ns,
        out bool createdNew,
        out EventWaitHandle showEvent)
    {
        SyncObjectNamespace = ns;
        MutexName = ns + "TILageMonitor_SingleInstance";
        ShowWindowEventName = ns + "TILageMonitor_ShowWindow";

        // Mutex first (namespace probe); event uses the same chosen namespace.
        // Create/open the show-window event alongside mutex acquisition so a late
        // second instance can signal even while MainWindow is still constructing.
        var mutex = new Mutex(initiallyOwned: true, MutexName, out createdNew);
        try
        {
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
            return mutex;
        }
        catch
        {
            try { mutex.Dispose(); } catch { /* ignore */ }
            throw;
        }
    }

    /// <summary>Signal primary instance on the same namespace this process chose (startup race retry).</summary>
    private static void TrySignalShowWindow()
    {
        // Prefer the coupled name; also try the alternate namespace once for a brief
        // upgrade race against an older primary that decided independently.
        var names = new[]
        {
            ShowWindowEventName,
            ShowWindowEventName == PreferredShowWindowEventName
                ? FallbackShowWindowEventName
                : PreferredShowWindowEventName
        };

        foreach (var name in names)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using var showEvent = EventWaitHandle.OpenExisting(name);
                    showEvent.Set();
                    return;
                }
                catch
                {
                    Thread.Sleep(40);
                }
            }
        }
    }

    private static string? GetArgumentValue(IEnumerable<string> args, string name)
    {
        var values = args.ToArray();
        for (var i = 0; i + 1 < values.Length; i++)
        {
            if (string.Equals(values[i], name, StringComparison.OrdinalIgnoreCase))
                return values[i + 1];
        }

        return null;
    }

    /// <summary>
    /// Releases and disposes the single-instance mutex (and App-owned show-window event handle)
    /// so a successor process can create the mutex during an in-place restart.
    /// Must run before Process.Start; OnExit must not re-acquire afterward.
    /// </summary>
    public void ReleaseSingleInstanceForRestart()
    {
        try { _showWindowEvent?.Dispose(); } catch { /* ignore */ }
        _showWindowEvent = null;

        if (_singleInstanceMutex is not null)
        {
            if (_ownsMutex)
            {
                try { _singleInstanceMutex.ReleaseMutex(); } catch { /* ignore */ }
            }

            try { _singleInstanceMutex.Dispose(); } catch { /* ignore */ }
            _singleInstanceMutex = null;
        }

        _ownsMutex = false;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _window?.Dispose();
        ToastRegistration.Unregister();

        try { _showWindowEvent?.Dispose(); } catch { /* ignore */ }
        _showWindowEvent = null;

        if (_ownsMutex && _singleInstanceMutex is not null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch
            {
                // ignore
            }
        }

        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;

        base.OnExit(e);
    }
}
