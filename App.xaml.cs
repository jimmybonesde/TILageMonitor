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

    /// <summary>Actual show-window event name in use (Global or Local fallback).</summary>
    public static string ShowWindowEventName { get; private set; } = PreferredShowWindowEventName;

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showWindowEvent;
    private MainWindow? _window;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Create/open the show-window event alongside mutex acquisition so a late
        // second instance can signal even while MainWindow is still constructing.
        _showWindowEvent = CreateShowWindowEvent(out var showEventName);
        ShowWindowEventName = showEventName;

        _singleInstanceMutex = CreateSingleInstanceMutex(out var createdNew);
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
    /// Prefer Global\ mutex (cross-session); on UnauthorizedAccessException fall back to Local\.
    /// </summary>
    private static Mutex CreateSingleInstanceMutex(out bool createdNew)
    {
        try
        {
            return new Mutex(initiallyOwned: true, PreferredMutexName, out createdNew);
        }
        catch (UnauthorizedAccessException)
        {
            // Restricted environments may deny Global\ — Local\ still prevents same-session dupes.
            return new Mutex(initiallyOwned: true, FallbackMutexName, out createdNew);
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return new Mutex(initiallyOwned: true, FallbackMutexName, out createdNew);
        }
    }

    private static EventWaitHandle CreateShowWindowEvent(out string eventName)
    {
        try
        {
            eventName = PreferredShowWindowEventName;
            return new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        }
        catch (UnauthorizedAccessException)
        {
            eventName = FallbackShowWindowEventName;
            return new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            eventName = FallbackShowWindowEventName;
            return new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        }
    }

    /// <summary>Signal primary instance with a short OpenExisting retry (startup race).</summary>
    private static void TrySignalShowWindow()
    {
        var names = new[] { PreferredShowWindowEventName, FallbackShowWindowEventName };
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
