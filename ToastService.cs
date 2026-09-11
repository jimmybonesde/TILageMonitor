using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Forms = System.Windows.Forms;

namespace TILageMonitor;

public enum ToastUrgency
{
    Info,
    Warning,
    Error
}

public static class ToastService
{
    private static Forms.NotifyIcon? _fallbackTray;
    private static Action? _onActivated;
    private static bool _initialized;
    private static Func<bool>? _notificationsEnabled;

    public static void SetNotificationsEnabledProvider(Func<bool> provider) =>
        _notificationsEnabled = provider;

    public static bool AreNotificationsEnabled =>
        _notificationsEnabled?.Invoke() ?? true;

    public static void Initialize(Action onActivated, Forms.NotifyIcon? fallbackTray = null)
    {
        _onActivated = onActivated;
        if (fallbackTray is not null)
            _fallbackTray = fallbackTray;

        if (_initialized)
            return;

        _initialized = true;
        ToastRegistration.EnsureRegistered(InvokeActivationOnDispatcher);
    }

    public static void SetFallbackTray(Forms.NotifyIcon tray) =>
        _fallbackTray = tray;

    public static void Show(string title, string body, ToastUrgency urgency = ToastUrgency.Info)
    {
        if (!AreNotificationsEnabled)
            return;

        var notificationShown = ToastRegistration.IsRegistered &&
            TryShowAppNotification(title, body);

        if (!notificationShown)
            ShowBalloonFallback(title, body, urgency);
    }

    private static void InvokeActivationOnDispatcher()
    {
        try
        {
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher?.CheckAccess() == true)
                _onActivated?.Invoke();
            else
                app?.Dispatcher?.Invoke(() => _onActivated?.Invoke());
        }
        catch
        {
            // Activation is best-effort.
        }
    }

    private static bool TryShowAppNotification(string title, string body)
    {
        try
        {
            var notification = new AppNotificationBuilder()
                .AddText(title)
                .AddText(body)
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ShowBalloonFallback(string title, string body, ToastUrgency urgency)
    {
        try
        {
            if (_fallbackTray is null)
                return;

            var icon = urgency switch
            {
                ToastUrgency.Error => Forms.ToolTipIcon.Error,
                ToastUrgency.Warning => Forms.ToolTipIcon.Warning,
                _ => Forms.ToolTipIcon.Info
            };

            if (title.Length > 63)
                title = title[..63];
            if (body.Length > 255)
                body = body[..255];

            _fallbackTray.ShowBalloonTip(
                urgency == ToastUrgency.Error ? 6000 : urgency == ToastUrgency.Warning ? 5000 : 4000,
                title,
                body,
                icon);
        }
        catch
        {
            // Notification fallback is best-effort.
        }
    }
}
