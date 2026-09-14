using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TILageMonitor;

public sealed record AuthenticodeCheckResult(bool IsValid, bool IsSigned, string Message);

public static class AuthenticodeVerifier
{
    public static AuthenticodeCheckResult Check(string path)
    {
        try
        {
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            var now = DateTime.UtcNow;
            if (now < certificate.NotBefore.ToUniversalTime() ||
                now > certificate.NotAfter.ToUniversalTime())
            {
                return new AuthenticodeCheckResult(false, true, "Das Signaturzertifikat des Installers ist abgelaufen oder noch nicht gültig.");
            }

            return new AuthenticodeCheckResult(true, true, "Authenticode-Zertifikat erkannt.");
        }
        catch (CryptographicException)
        {
            // Existing releases may be unsigned; SHA-256 remains the mandatory check.
            return new AuthenticodeCheckResult(true, false, "Installer ist unsigniert; SHA-256-Prüfsumme wurde verwendet.");
        }
        catch (Exception ex)
        {
            return new AuthenticodeCheckResult(false, true, $"Authenticode-Prüfung fehlgeschlagen: {ex.Message}");
        }
    }
}
