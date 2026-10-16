using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TILageMonitor.Konnektor;

/// <summary>Encrypts/decrypts secrets at rest. Mockable for tests.</summary>
public interface ISecretProtector
{
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] protectedData);
}

/// <summary>Windows DPAPI, CurrentUser scope (only this Windows user can decrypt).</summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TILageMonitor.Konnektor.v1");

    public byte[] Protect(byte[] plaintext) =>
        ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] protectedData) =>
        ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.CurrentUser);
}

/// <summary>
/// Stores the client-system PKCS#12 and its password only in protected form
/// (<c>konnektor-client.p12.dpapi</c>, <c>konnektor-client.pwd.dpapi</c>) — never plaintext.
/// </summary>
public sealed class KonnektorSecretStore
{
    public const string P12FileName = "konnektor-client.p12.dpapi";
    public const string PasswordFileName = "konnektor-client.pwd.dpapi";

    private readonly ISecretProtector _protector;
    private readonly string _directory;

    public KonnektorSecretStore(ISecretProtector protector, string directory)
    {
        _protector = protector;
        _directory = directory;
    }

    public static KonnektorSecretStore CreateDefault() =>
        new(new DpapiSecretProtector(), SettingsStore.SettingsDirectoryPath);

    private string P12Path => Path.Combine(_directory, P12FileName);
    private string PasswordPath => Path.Combine(_directory, PasswordFileName);

    public bool HasClientCertificate => File.Exists(P12Path);

    public void SaveClientCertificate(byte[] p12Bytes, string password)
    {
        Directory.CreateDirectory(_directory);
        WriteAtomic(P12Path, _protector.Protect(p12Bytes));
        var pwdBytes = Encoding.UTF8.GetBytes(password ?? "");
        try
        {
            WriteAtomic(PasswordPath, _protector.Protect(pwdBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pwdBytes);
        }
    }

    public (byte[] P12, string Password)? LoadClientCertificate()
    {
        if (!File.Exists(P12Path))
            return null;
        var p12 = _protector.Unprotect(File.ReadAllBytes(P12Path));
        var password = File.Exists(PasswordPath)
            ? Encoding.UTF8.GetString(_protector.Unprotect(File.ReadAllBytes(PasswordPath)))
            : "";
        return (p12, password);
    }

    public void Delete()
    {
        foreach (var path in new[] { P12Path, PasswordPath })
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
        }
    }

    private static void WriteAtomic(string path, byte[] data)
    {
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, data);
        File.Move(tmp, path, overwrite: true);
    }
}
