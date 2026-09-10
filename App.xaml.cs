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
            // Bestehende Instanz zum Anzeigen auffordern
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
                showEvent.Set();
            }
            catch
            {
                // Event existiert noch nicht – ignorieren
            }

            Shutdown();
            return;
        }

        // Settings zuerst laden und Theme setzen, bevor MainWindow entsteht
        var settings = SettingsStore.Load();
        ThemeService.ApplyTheme(settings.DarkMode);

        // Defaults anlegen, falls die Datei noch fehlt
        if (!SettingsStore.Exists)
            SettingsStore.Save(settings);

        // Registry-Autostart mit gespeicherter Einstellung abgleichen
        AutostartService.SetEnabled(settings.AutoStart);

        var startInTray = ShouldStartInTray(e.Args);

        // Unpackaged toast: stable AUMID + Start Menu shortcut + Toolkit warm-up
        ToastRegistration.EnsureRegistered();

        _window = new MainWindow();

        // Toast-Klick → Hauptfenster (single-instance friendly via Dispatcher)
        ToastService.Initialize(() =>
        {
            try
            {
                _window?.ShowWindowPublic();
            }
            catch
            {
                // ignore
            }
        });

        if (startInTray)
        {
            // Show() damit Loaded (und damit Refresh) feuert, dann in den Tray
            _window.ShowInTaskbar = false;
            _window.Show();
            _window.Hide();
        }
        else
        {
            _window.ShowInTaskbar = true;
            _window.Show();
        }
    }

    private static bool ShouldStartInTray(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-minimized", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/tray", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _window?.Dispose();

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
