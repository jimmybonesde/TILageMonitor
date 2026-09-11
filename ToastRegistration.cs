using Microsoft.Windows.AppNotifications;

namespace TILageMonitor;

public static class ToastRegistration
{
    private static Action? _onActivated;
    private static bool _registered;

    public static bool IsRegistered => _registered;

    public static void EnsureRegistered(Action onActivated)
    {
        _onActivated = onActivated;
        if (_registered)
            return;

        try
        {
            var manager = AppNotificationManager.Default;
            manager.NotificationInvoked += OnNotificationInvoked;
            manager.Register();
            _registered = true;
        }
        catch
        {
            _registered = false;
        }
    }

    public static void Unregister()
    {
        if (!_registered)
            return;

        try
        {
            var manager = AppNotificationManager.Default;
            manager.NotificationInvoked -= OnNotificationInvoked;
            manager.Unregister();
        }
        catch
        {
            // Notification cleanup is best-effort.
        }
        finally
        {
            _registered = false;
        }
    }

    private static void OnNotificationInvoked(
        AppNotificationManager sender,
        AppNotificationActivatedEventArgs args)
    {
        _onActivated?.Invoke();
    }
}
