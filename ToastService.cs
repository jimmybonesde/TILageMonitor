using System.Xml.Linq;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;
using Forms = System.Windows.Forms;

namespace TILageMonitor;

/// <summary>Urgency / style for Windows toast notifications.</summary>
public enum ToastUrgency
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Thin wrapper around ToastContentBuilder with NotifyIcon balloon fallback.
/// Requires TFM net8.0-windows10.0.17763.0+ for ToastNotificationManagerCompat.
/// </summary>
public static class ToastService
{
    private static Forms.NotifyIcon? _fallbackTray;
    private static Action? _onActivated;
    private static bool _activationRegistered;

    /// <summary>
    /// Registers toast click activation (show main window) and optional balloon fallback.
    /// Safe to call once from App.OnStartup / MainWindow.
    /// </summary>
    public static void Initialize(Action onActivated, Forms.NotifyIcon? fallbackTray = null)
    {
        _onActivated = onActivated;
        if (fallbackTray is not null)
            _fallbackTray = fallbackTray;

        if (_activationRegistered)
            return;

        _activationRegistered = true;

        try
        {
            ToastNotificationManagerCompat.OnActivated += _ =>
            {
                try
                {
                    var app = System.Windows.Application.Current;
                    if (app?.Dispatcher is null)
                    {
                        _onActivated?.Invoke();
                        return;
                    }

                    if (app.Dispatcher.CheckAccess())
                        _onActivated?.Invoke();
                    else
                        app.Dispatcher.Invoke(() => _onActivated?.Invoke());
                }
                catch
                {
                    // Activation is best-effort
                }
            };
        }
        catch
        {
            // Toast COM registration may fail on unsupported hosts — ignore
        }
    }

    /// <summary>Updates the balloon fallback tray (e.g. after MainWindow creates NotifyIcon).</summary>
    public static void SetFallbackTray(Forms.NotifyIcon tray) =>
        _fallbackTray = tray;

    public static void Show(string title, string body, ToastUrgency urgency = ToastUrgency.Info)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddArgument("action", "show")
                .AddText(title)
                .AddText(body);

            switch (urgency)
            {
                case ToastUrgency.Error:
                    builder.AddAudio(new Uri("ms-winsoundevent:Notification.Looping.Alarm4"), loop: false);
                    break;
                case ToastUrgency.Warning:
                    builder.AddAudio(new Uri("ms-winsoundevent:Notification.SMS"), loop: false);
                    break;
                default:
                    builder.AddAudio(new Uri("ms-winsoundevent:Notification.Default"), loop: false);
                    break;
            }

            // Prefer Toolkit Show() extension (desktop); fall back to notifier API.
            try
            {
                builder.Show();
            }
            catch
            {
                var xml = builder.GetToastContent().GetXml();
                var toast = new ToastNotification(xml);
                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
            }
        }
        catch
        {
            ShowBalloonFallback(title, body, urgency);
        }
    }

    private static void ShowBalloonFallback(string title, string body, ToastUrgency urgency)
    {
        try
        {
            var tray = _fallbackTray;
            if (tray is null)
                return;

            var icon = urgency switch
            {
                ToastUrgency.Error => Forms.ToolTipIcon.Error,
                ToastUrgency.Warning => Forms.ToolTipIcon.Warning,
                _ => Forms.ToolTipIcon.Info
            };

            var timeout = urgency switch
            {
                ToastUrgency.Error => 6000,
                ToastUrgency.Warning => 5000,
                _ => 4000
            };

            tray.ShowBalloonTip(timeout, title, body, icon);
        }
        catch
        {
            // both toast and balloon failed — silent
        }
    }
}
