using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TILageMonitor;

public sealed record AuthenticodeCheckResult(bool IsValid, bool IsSigned, string Message);

/// <summary>Validates an Authenticode signature through Windows trust verification.</summary>
public static class AuthenticodeVerifier
{
    private static readonly Guid WinTrustActionGenericVerifyV2 =
        new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

    public static AuthenticodeCheckResult Check(string path)
    {
        try
        {
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            var trustResult = VerifyWindowsTrust(path);
            if (trustResult != 0)
            {
                return new AuthenticodeCheckResult(
                    false,
                    true,
                    "Die Authenticode-Signatur des Installers ist ungültig oder nicht vertrauenswürdig.");
            }

            return new AuthenticodeCheckResult(
                true,
                true,
                $"Authenticode-Signatur von {certificate.Subject} wurde geprüft.");
        }
        catch (CryptographicException)
        {
            // Releases remain compatible until SignPath signing is enabled.
            // SHA-256 is mandatory for every downloaded installer.
            return new AuthenticodeCheckResult(
                true,
                false,
                "Installer ist unsigniert; SHA-256-Prüfsumme wurde verwendet.");
        }
        catch (Exception ex)
        {
            return new AuthenticodeCheckResult(
                false,
                true,
                $"Authenticode-Prüfung fehlgeschlagen: {ex.Message}");
        }
    }

    private static int VerifyWindowsTrust(string path)
    {
        var fileInfo = new WinTrustFileInfo(path);
        var fileInfoPointer = IntPtr.Zero;
        var dataPointer = IntPtr.Zero;

        try
        {
            fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
            Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);

            var data = new WinTrustData(fileInfoPointer);
            dataPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustData>());
            Marshal.StructureToPtr(data, dataPointer, false);

            return WinVerifyTrust(IntPtr.Zero, WinTrustActionGenericVerifyV2, dataPointer);
        }
        finally
        {
            if (dataPointer != IntPtr.Zero)
            {
                Marshal.DestroyStructure<WinTrustData>(dataPointer);
                Marshal.FreeHGlobal(dataPointer);
            }

            if (fileInfoPointer != IntPtr.Zero)
            {
                Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPointer);
                Marshal.FreeHGlobal(fileInfoPointer);
            }
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int WinVerifyTrust(
        IntPtr hwnd,
        [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID,
        IntPtr pWVTData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint StructSize;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;

        public WinTrustFileInfo(string filePath)
        {
            StructSize = (uint)Marshal.SizeOf<WinTrustFileInfo>();
            FilePath = filePath;
            FileHandle = IntPtr.Zero;
            KnownSubject = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;

        public WinTrustData(IntPtr fileInfo)
        {
            StructSize = (uint)Marshal.SizeOf<WinTrustData>();
            PolicyCallbackData = IntPtr.Zero;
            SipClientData = IntPtr.Zero;
            UiChoice = 2; // WTD_UI_NONE
            RevocationChecks = 0;
            UnionChoice = 1; // WTD_CHOICE_FILE
            FileInfo = fileInfo;
            StateAction = 0; // WTD_STATEACTION_IGNORE
            StateData = IntPtr.Zero;
            UrlReference = IntPtr.Zero;
            ProviderFlags = 0x00000040; // WTD_REVOCATION_CHECK_CHAIN
            UiContext = 0;
            SignatureSettings = IntPtr.Zero;
        }
    }
}
