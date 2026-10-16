using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TILageMonitor.Konnektor;

public enum PinEvaluation
{
    /// <summary>No fingerprint stored yet — user has to confirm trust first.</summary>
    NotPinned,
    Match,
    /// <summary>A different certificate than the trusted one — possible MITM or connector re-key.</summary>
    Mismatch
}

/// <summary>
/// Trust-on-first-use pinning of the connector TLS certificate (SHA-256 over DER).
/// Connector certificates come from the TI PKI that Windows does not trust, so the pin
/// replaces chain validation; there is never a blanket "accept any certificate".
/// </summary>
public static class CertificatePinning
{
    public static string Fingerprint(byte[] derCertificate)
    {
        ArgumentNullException.ThrowIfNull(derCertificate);
        var hash = SHA256.HashData(derCertificate);
        return string.Join(":", hash.Select(b => b.ToString("X2")));
    }

    public static string Fingerprint(X509Certificate2 certificate) => Fingerprint(certificate.RawData);

    /// <summary>Normalises "aa:bb cc-DD" style input to "AABBCCDD".</summary>
    public static string Normalize(string? fingerprint) =>
        new string((fingerprint ?? "").Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());

    public static PinEvaluation Evaluate(string? actualFingerprint, string? pinnedFingerprint)
    {
        var pinned = Normalize(pinnedFingerprint);
        if (pinned.Length == 0)
            return PinEvaluation.NotPinned;
        var actual = Normalize(actualFingerprint);
        // SHA-256 = 64 hex chars; anything else can never be a valid match.
        if (actual.Length != 64 || pinned.Length != 64)
            return PinEvaluation.Mismatch;
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(actual),
            System.Text.Encoding.ASCII.GetBytes(pinned))
            ? PinEvaluation.Match
            : PinEvaluation.Mismatch;
    }

    public static KonnektorServerCertificate Describe(X509Certificate2 certificate) =>
        new(Fingerprint(certificate), certificate.Subject, certificate.Issuer,
            certificate.NotBefore, certificate.NotAfter);
}
