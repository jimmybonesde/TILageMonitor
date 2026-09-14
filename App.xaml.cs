using System.Threading;
using System.Windows;

namespace TILageMonitor;

public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\TILageMonitor_SingleInstance";
    public const string ShowWindowEventName = @"Local\TILageMonitor_ShowWindow";

    private Mutex? _singleInstanceMutex;
    private MainWindow? _window;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        _ownsMutex = createdNew;

        if (!createdNew)
        {
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
                showEvent.Set();
            }
            catch
            {
                // The primary instance may still be initializing.
            }

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
