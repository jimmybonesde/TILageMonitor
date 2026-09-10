using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;

namespace TILageMonitor;

/// <summary>
/// Unpackaged Win32 toast registration: stable AUMID, Start Menu shortcut with
/// PKEY_AppUserModel_ID, and Toolkit COM activator warm-up.
/// </summary>
public static class ToastRegistration
{
    public const string AppUserModelId = "RandyCarter.TILageMonitor";
    public const string ShortcutName = "TI-Lage Monitor.lnk";
    public const string DisplayName = "TI-Lage Monitor";

    /// <summary>True when Toolkit notifier could be created after registration.</summary>
    public static bool IsRegistered { get; private set; }

    /// <summary>
    /// Call once early in App.OnStartup (before ToastService.Initialize / Show).
    /// Best-effort: never throws.
    /// </summary>
    public static void EnsureRegistered()
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }
        catch
        {
            // ignore
        }

        try
        {
            EnsureStartMenuShortcut();
        }
        catch
        {
            // ignore
        }

        try
        {
            // Some Toolkit builds expose Register(); 7.1.3 relies on static Initialize.
            TryCallToolkitRegister();
            _ = ToastNotificationManagerCompat.CreateToastNotifier();
            IsRegistered = true;
        }
        catch
        {
            IsRegistered = false;
        }
    }

    private static void TryCallToolkitRegister()
    {
        try
        {
            var mi = typeof(ToastNotificationManagerCompat).GetMethod(
                "Register",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            mi?.Invoke(null, null);
        }
        catch
        {
            // absent on 7.1.3 — CreateToastNotifier still warms up COM / registry
        }
    }

    public static void EnsureStartMenuShortcut()
    {
        var programs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs");
        Directory.CreateDirectory(programs);
        var shortcutPath = Path.Combine(programs, ShortcutName);

        var exePath = GetExecutablePath();
        var iconPath = FindIconPath(exePath);

        CreateShortcutWithAumid(shortcutPath, exePath, iconPath, AppUserModelId);

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                @"Software\Classes\AppUserModelId\" + AppUserModelId);
            key?.SetValue("DisplayName", DisplayName);
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                key?.SetValue("IconUri", iconPath);
        }
        catch
        {
            // ignore
        }
    }

    private static string GetExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            return Environment.ProcessPath!;

        try
        {
            var fromModule = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(fromModule))
                return fromModule!;
        }
        catch
        {
            // ignore
        }

        return Path.Combine(AppContext.BaseDirectory, "TILageMonitor.exe");
    }

    private static string? FindIconPath(string exePath)
    {
        var dir = Path.GetDirectoryName(exePath) ?? "";
        var besideExe = Path.Combine(dir, "TILageMonitor.ico");
        if (File.Exists(besideExe))
            return besideExe;

        var baseIco = Path.Combine(AppContext.BaseDirectory, "TILageMonitor.ico");
        if (File.Exists(baseIco))
            return baseIco;

        return File.Exists(exePath) ? exePath : null;
    }

    private static void CreateShortcutWithAumid(
        string shortcutPath,
        string targetPath,
        string? iconPath,
        string aumid)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            link.SetPath(targetPath);
            link.SetWorkingDirectory(Path.GetDirectoryName(targetPath) ?? "");
            link.SetDescription(DisplayName);
            if (!string.IsNullOrEmpty(iconPath))
                link.SetIconLocation(iconPath, 0);

            var store = (IPropertyStore)link;
            var pv = new PropVariant(aumid);
            try
            {
                var key = PkeyAppUserModelId;
                store.SetValue(ref key, pv);
                store.Commit();
            }
            finally
            {
                pv.Clear();
            }

            ((IPersistFile)link).Save(shortcutPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appID);

    private static PropertyKey PkeyAppUserModelId =
        new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile,
            int cchMaxPath,
            IntPtr pfd,
            uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName,
            int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir,
            int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs,
            int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath,
            int cchIconPath,
            out int piIcon);
        void SetIconLocation(
            [MarshalAs(UnmanagedType.LPWStr)] string pszIconPath,
            int iIcon);
        void SetRelativePath(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPathRel,
            uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PropertyKey pkey);
        void GetValue(ref PropertyKey key, PropVariant pv);
        void SetValue(ref PropertyKey key, PropVariant pv);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;

        public PropertyKey(Guid formatId, uint propertyId)
        {
            fmtid = formatId;
            pid = propertyId;
        }
    }

    /// <summary>Minimal PROPVARIANT wrapper for VT_LPWSTR values.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private sealed class PropVariant : IDisposable
    {
        private ushort vt;
        private ushort wReserved1;
        private ushort wReserved2;
        private ushort wReserved3;
        private IntPtr pointerValue;

        public PropVariant(string value)
        {
            vt = (ushort)VarEnum.VT_LPWSTR;
            pointerValue = Marshal.StringToCoTaskMemUni(value);
        }

        public void Clear()
        {
            PropVariantClear(this);
            vt = 0;
            pointerValue = IntPtr.Zero;
        }

        public void Dispose() => Clear();

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear([In, Out] PropVariant pvar);
    }
}
