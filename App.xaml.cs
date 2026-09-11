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

        var settings = SettingsStore.Load();
        ThemeService.ApplyTheme(settings.DarkMode);

        if (!SettingsStore.Exists)
            SettingsStore.Save(settings);

        AutostartService.SetEnabled(settings.AutoStart);

        _window = new MainWindow();

        ToastService.Initialize(() =>
        {
            try
            {
                _window?.ShowWindowPublic();
            }
            catch
            {
                // Window activation is best-effort.
            }
        });

        _window.ShowInTaskbar = false;
        _window.Show();
        _window.Hide();
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
