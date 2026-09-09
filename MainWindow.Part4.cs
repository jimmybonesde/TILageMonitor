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

public partial class MainWindow
{

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    private void DisposeTrayIcons()
    {
        if (_trayIconsDisposed)
            return;

        _trayIconsDisposed = true;
        _trayIconOk.Dispose();
        _trayIconStoerung.Dispose();
        _trayIconBeeintraechtigung.Dispose();
    }

    private void CloseApp()
    {
        DisposeRuntime();
        System.Windows.Application.Current.Shutdown();
    }
}

public record ServiceStatusChange(
    string ServiceKey,
    string ServiceName,
    string PreviousStatus,
    string CurrentStatus,
    string Details);

public record AppRow(
    string Icon,
    string Name,
    string Detail,
    System.Windows.Media.Brush StatusBrush);

public record MessageRow(
    string Header,
    string Body);
