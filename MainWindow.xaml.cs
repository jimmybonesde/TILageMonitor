using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace TILageMonitor;

public partial class MainWindow : Window
{
    private readonly ApiClient _api = new();

    private readonly Forms.NotifyIcon _tray;
    private readonly Drawing.Icon _trayIconOk;
    private readonly Drawing.Icon _trayIconStoerung;
    private readonly Drawing.Icon _trayIconBeeintraechtigung;

    private readonly ObservableCollection<AppRow> _apps = new();
    private readonly ObservableCollection<MessageRow> _messages = new();
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    private readonly HashSet<string> _knownActiveIncidents = new();
    private readonly Dictionary<string, string> _knownServiceStatus =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _firstLoad = true;
    private bool _hadTiProblem;
    private int _consecutiveApiFailures;
    private bool _apiDownBalloonShown;
    private bool _loading;
    private bool _trayIconsDisposed;
    private bool _disposed;
    private AppSettings _settings = new();

    private EventWaitHandle? _showWindowEvent;
    private CancellationTokenSource? _showWindowCts;
    private Forms.ToolStripMenuItem? _autostartMenuItem;
    private Forms.ToolStripMenuItem? _notificationsMenuItem;
    private SettingsWindow? _settingsWindow;
    private HistoryWindow? _historyWindow;
    private HistoryFile _lastHistory = new();
    private OutageResponse? _lastOutages;


    private static readonly Dictionary<string, string> ServiceNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["erezept"] = "eRezept",
            ["epa"] = "ePA",
            ["kim"] = "KIM",
            ["wanda"] = "WANDA",
            ["ogd"] = "OGD",
            ["vsdm"] = "VSDM",
            ["tianschluss"] = "TI-Anschluss"
        };

    public MainWindow()
    {
        InitializeComponent();
        VersionFooterText.Text = $"© 2026 R.C. · TI-Lage Monitor · Version {GetDisplayVersion()}";

        AppsList.ItemsSource = _apps;
        MessagesList.ItemsSource = _messages;

        _settings = SettingsStore.Load();
        ToastService.SetNotificationsEnabledProvider(() => _settings.NotificationsEnabled);

        // =========================================================
        // SYSTEM TRAY
        // =========================================================

        // Mockup: green ✓ / amber ! / red ! on blue shield (32×32 for HiDPI)
        _trayIconOk = CreateStatusIcon(
            Drawing.Color.FromArgb(34, 197, 94), TrayBadgeGlyph.Check);       // green OK
        _trayIconBeeintraechtigung = CreateStatusIcon(
            Drawing.Color.FromArgb(245, 158, 11), TrayBadgeGlyph.Exclaim);  // amber
        _trayIconStoerung = CreateStatusIcon(
            Drawing.Color.FromArgb(220, 38, 38), TrayBadgeGlyph.Exclaim);   // red

        _tray = new Forms.NotifyIcon
        {
            Visible = true,
            Text = "TI-Status: wird geladen…",
            Icon = _trayIconOk
        };

        ToastService.SetFallbackTray(_tray);

        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
                ToggleWindow();
        };

        _tray.DoubleClick += (_, _) => ToggleWindow();

        // Fallback: Klick auf Balloon (wenn Toast-API fehlschlägt) öffnet das Fenster
        _tray.BalloonTipClicked += (_, _) =>
        {
            Dispatcher.Invoke(ShowWindow);
        };

        var menu = new Forms.ContextMenuStrip();

        menu.Items.Add("TI-Lage anzeigen", null, (_, _) => ShowWindow());
        menu.Items.Add("Jetzt aktualisieren", null, (_, _) => _ = RefreshAsync(false));
        menu.Items.Add(
            "gematik TI-Status öffnen",
            null,
            (_, _) => OpenUrl("https://fachportal.gematik.de/ti-status#TI-Anschluss"));

        menu.Items.Add(new Forms.ToolStripSeparator());

        menu.Items.Add("Über TI-Lage Monitor…", null, (_, _) => ShowAbout());
        menu.Items.Add("TI-Status · 7 Tage…", null, (_, _) => Dispatcher.Invoke(OpenHistoryWindow));
        menu.Items.Add("Einstellungen…", null, (_, _) => Dispatcher.Invoke(OpenSettingsWindow));

        _autostartMenuItem = new Forms.ToolStripMenuItem("Autostart: aus")
        {
            CheckOnClick = false
        };
        _autostartMenuItem.Click += (_, _) => ToggleAutostartFromMenu();
        SyncAutostartMenuItem();
        menu.Items.Add(_autostartMenuItem);

        _notificationsMenuItem = new Forms.ToolStripMenuItem("Benachrichtigungen: an")
        {
            CheckOnClick = false
        };
        _notificationsMenuItem.Click += (_, _) => ToggleNotificationsFromMenu();
        SyncNotificationsMenuItem();
        menu.Items.Add(_notificationsMenuItem);

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => CloseApp());

        _tray.ContextMenuStrip = menu;

        // =========================================================
        // FENSTER
        // =========================================================

        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
                HideToTray();
        };

        Closing += (_, e) =>
        {
            if (e.Cancel)
                return;

            DisposeRuntime();
        };

        // =========================================================
        // SINGLE-INSTANCE: ShowWindow-Event
        // =========================================================

        StartShowWindowListener();

        // =========================================================
        // AUTOMATISCHE AKTUALISIERUNG
        // gematik empfiehlt Abfrage im ~5-Minuten-Rhythmus mit leichtem Offset
        // (Slots :01, :06, :11, … — Minute % 5 == 1).
        // =========================================================

        _timer = new System.Windows.Threading.DispatcherTimer();

        _timer.Tick += async (_, _) =>
        {
            await RefreshAsync(false);
            ScheduleNextRefresh();
            _ = CheckForUpdatesAsync(userInitiated: false);
        };

        Loaded += async (_, _) =>
        {
            // Verlauf aus lokalem Cache anzeigen (auch vor erstem API-Call)
            RenderHistory(HistoryStore.Load(), null);

            await RefreshAsync(false);
            ScheduleNextRefresh();
        };
    }

    /// <summary>
    /// Delay until the next gematik-style refresh slot (local minute % 5 == 1, second 0).
    /// Slots: 01, 06, 11, 16, 21, 26, 31, 36, 41, 46, 51, 56.
    /// </summary>
    private static TimeSpan GetDelayUntilNextGematikSlot(DateTime nowLocal)
    {
        // Truncate to the current minute, then advance to the next slot minute.
        var next = new DateTime(
            nowLocal.Year, nowLocal.Month, nowLocal.Day,
            nowLocal.Hour, nowLocal.Minute, 0, nowLocal.Kind);

        int mod = next.Minute % 5;
        if (mod == 1)
        {
            // Already on a slot minute — if past second 0, take the following slot (+5 min).
            if (nowLocal > next)
                next = next.AddMinutes(5);
        }
        else
        {
            // Minutes until next minute where Minute % 5 == 1.
            int minutesToAdd = (1 - mod + 5) % 5;
            next = next.AddMinutes(minutesToAdd);
        }

        var delay = next - nowLocal;
        // Minimum 5 seconds avoids tight loops if we land exactly on a boundary.
        if (delay < TimeSpan.FromSeconds(5))
            delay = TimeSpan.FromSeconds(5);
        return delay;
    }

    private void ScheduleNextRefresh()
    {
        _timer.Stop();
        _timer.Interval = GematikRefreshSchedule.GetDelayUntilNextSlot(DateTime.Now);
        _timer.Start();
    }

    // =============================================================
    // SHOW-WINDOW EVENT (zweite Instanz)
    // =============================================================

    private void StartShowWindowListener()
    {
        try
        {
            _showWindowEvent = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                App.ShowWindowEventName);

            _showWindowCts = new CancellationTokenSource();
            var token = _showWindowCts.Token;

            _ = Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (_showWindowEvent.WaitOne(500))
                        {
                            Dispatcher.Invoke(ShowWindow);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch
                    {
                        // kurz warten und weiter
                        Thread.Sleep(200);
                    }
                }
            }, token);
        }
        catch
        {
            // optional
        }
    }

    private void StopShowWindowListener()
    {
        try
        {
            _showWindowCts?.Cancel();
            _showWindowCts?.Dispose();
            _showWindowCts = null;
            _showWindowEvent?.Dispose();
            _showWindowEvent = null;
        }
        catch
        {
            // ignore
        }
    }

    // =============================================================
    // EINSTELLUNGEN (SettingsWindow)
    // =============================================================

    public AppSettings GetSettingsSnapshot() => _settings;

    public void ApplySettings(AppSettings settings, bool themeChanged = false)
    {
        _settings = settings;
        SyncAutostartMenuItem();
        SyncNotificationsMenuItem();
        if (themeChanged)
            NotifyHistoryUpdated(_lastHistory, _lastOutages);
        _settingsWindow?.SyncFrom(_settings);
    }

    public void ShowWindowPublic() => ShowWindow();

    private void OpenSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            if (!_settingsWindow.IsVisible)
                _settingsWindow.Show();
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(this);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }
}
