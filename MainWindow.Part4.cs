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

    private enum TrayBadgeGlyph
    {
        Check,
        Exclaim
    }

    /// <summary>
    /// 32×32 tray icon: blue shield base + status badge (✓ / !) bottom-right.
    /// </summary>
    private static Drawing.Icon CreateStatusIcon(Drawing.Color badge, TrayBadgeGlyph glyph)
    {
        const int size = 32;
        using var bmp = new Drawing.Bitmap(size, size);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.Clear(Drawing.Color.Transparent);

            if (!TryDrawBaseTrayIcon(g, size))
                DrawFallbackShield(g, size);

            // Badge ~1/3 of icon, bottom-right (mockup)
            const int badgeSize = 13;
            int bx = size - badgeSize - 1;
            int by = size - badgeSize - 1;

            using (var brush = new Drawing.SolidBrush(badge))
                g.FillEllipse(brush, bx, by, badgeSize, badgeSize);
            using (var pen = new Drawing.Pen(Drawing.Color.FromArgb(255, 255, 255), 1.5f))
                g.DrawEllipse(pen, bx + 0.5f, by + 0.5f, badgeSize - 1f, badgeSize - 1f);
            using (var pen = new Drawing.Pen(Drawing.Color.FromArgb(50, 50, 50), 1f))
                g.DrawEllipse(pen, bx, by, badgeSize, badgeSize);

            DrawBadgeGlyph(g, bx, by, badgeSize, glyph);
        }

        var handle = bmp.GetHicon();
        try
        {
            using var temp = Drawing.Icon.FromHandle(handle);
            return (Drawing.Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static void DrawBadgeGlyph(
        Drawing.Graphics g, int bx, int by, int badgeSize, TrayBadgeGlyph glyph)
    {
        using var font = new Drawing.Font(
            "Segoe UI",
            glyph == TrayBadgeGlyph.Check ? 8.5f : 9.5f,
            Drawing.FontStyle.Bold,
            Drawing.GraphicsUnit.Pixel);
        var text = glyph == TrayBadgeGlyph.Check ? "✓" : "!";
        var sz = g.MeasureString(text, font);
        float tx = bx + (badgeSize - sz.Width) / 2f + 0.5f;
        float ty = by + (badgeSize - sz.Height) / 2f - 0.5f;
        using var brush = new Drawing.SolidBrush(Drawing.Color.White);
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.DrawString(text, font, brush, tx, ty);
    }

    /// <summary>Simple blue shield when TILageMonitor.ico is unavailable.</summary>
    private static void DrawFallbackShield(Drawing.Graphics g, int size)
    {
        // Dark blue shield silhouette matching mockup base
        var blue = Drawing.Color.FromArgb(30, 64, 175);
        using var path = new Drawing.Drawing2D.GraphicsPath();
        float w = size - 4;
        float h = size - 2;
        float x = 2;
        float y = 1;
        // Shield: top rounded rect tapering to a point
        path.AddLine(x + w * 0.15f, y + 2, x + w * 0.85f, y + 2);
        path.AddLine(x + w * 0.85f, y + 2, x + w, y + h * 0.35f);
        path.AddLine(x + w, y + h * 0.35f, x + w * 0.5f, y + h);
        path.AddLine(x + w * 0.5f, y + h, x, y + h * 0.35f);
        path.CloseFigure();
        using (var brush = new Drawing.SolidBrush(blue))
            g.FillPath(brush, path);
        using (var pen = new Drawing.Pen(Drawing.Color.FromArgb(20, 40, 120), 1f))
            g.DrawPath(pen, path);

        // Simple white star-of-life cross hint
        using var white = new Drawing.Pen(Drawing.Color.FromArgb(230, 242, 255), 2f);
        float cx = size / 2f;
        float cy = size / 2f - 1;
        g.DrawLine(white, cx, cy - 6, cx, cy + 6);
        g.DrawLine(white, cx - 6, cy, cx + 6, cy);
    }

    /// <summary>
    /// Draws the app icon (TILageMonitor.ico) into a size×size graphics context.
    /// Tries BaseDirectory next to the exe, then a pack URI resource.
    /// </summary>
    private static bool TryDrawBaseTrayIcon(Drawing.Graphics g, int size)
    {
        try
        {
            var icoPath = Path.Combine(AppContext.BaseDirectory, "TILageMonitor.ico");
            if (File.Exists(icoPath))
            {
                using var baseIcon = new Drawing.Icon(icoPath, 64, 64);
                using var src = baseIcon.ToBitmap();
                g.DrawImage(src, 0, 0, size, size);
                return true;
            }
        }
        catch
        {
            // fall through to pack URI / fallback
        }

        try
        {
            var streamInfo = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/TILageMonitor.ico", UriKind.Absolute));
            if (streamInfo?.Stream != null)
            {
                using var baseIcon = new Drawing.Icon(streamInfo.Stream, 64, 64);
                using var src = baseIcon.ToBitmap();
                g.DrawImage(src, 0, 0, size, size);
                return true;
            }
        }
        catch
        {
            // missing resource — caller draws shield fallback
        }

        return false;
    }

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
    string Body,
    string TimestampText);
