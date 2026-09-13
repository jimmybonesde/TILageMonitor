using Microsoft.Windows.AppNotifications;

namespace TILageMonitor;

public static class ToastRegistration
{
    private static Action<string?>? _onActivated;
    private static bool _registered;

    public static bool IsRegistered => _registered;

    public static void EnsureRegistered(Action<string?> onActivated)
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
        string? focus = null;
        try
        {
            if (args.Arguments is not null &&
                args.Arguments.TryGetValue("focus", out var fromMap) &&
                !string.IsNullOrWhiteSpace(fromMap))
            {
                focus = fromMap;
            }
            else if (!string.IsNullOrWhiteSpace(args.Argument))
            {
                // Raw "action=open;focus=erezept" or "action=open&focus=erezept"
                var raw = args.Argument;
                foreach (var part in raw.Split(new[] { ';', '&' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var eq = part.IndexOf('=');
                    if (eq <= 0)
                        continue;
                    var key = part[..eq];
                    var value = part[(eq + 1)..];
                    if (string.Equals(key, "focus", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(value))
                    {
                        focus = Uri.UnescapeDataString(value);
                        break;
                    }
                }
            }
        }
        catch
        {
            focus = null;
        }

        _onActivated?.Invoke(focus);
    }
}
