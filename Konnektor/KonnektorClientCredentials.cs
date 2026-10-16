using System.IO;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.TeleTrust;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;

namespace TILageMonitor.Konnektor;

public enum KonnektorKeyType
{
    Rsa,
    EcSecp256r1,
    EcSecp384r1,
    EcBrainpoolP256r1,
    EcBrainpoolP384r1,
    EcBrainpoolP512r1
}

/// <summary>Problems with the client-system P12 (wrong password, no key, unsupported key type).</summary>
public sealed class KonnektorCredentialException : Exception
{
    public KonnektorCredentialException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Client-system credentials loaded with BouncyCastle from PKCS#12 — kept in memory only,
/// never imported into a Windows key store. Supports RSA and ECDSA (NIST + brainpool).
/// </summary>
public sealed class KonnektorClientCredentials
{
    public AsymmetricKeyParameter PrivateKey { get; }
    /// <summary>DER certificates, leaf first.</summary>
    public IReadOnlyList<byte[]> ChainDer { get; }
    public KonnektorKeyType KeyType { get; }
    public DateTime NotAfter { get; }
    public string Subject { get; }

    private KonnektorClientCredentials(AsymmetricKeyParameter key, IReadOnlyList<byte[]> chain, KonnektorKeyType type, DateTime notAfter, string subject)
    {
        PrivateKey = key;
        ChainDer = chain;
        KeyType = type;
        NotAfter = notAfter;
        Subject = subject;
    }

    public static KonnektorClientCredentials Create(AsymmetricKeyParameter key, IReadOnlyList<byte[]> chainDer)
    {
        if (chainDer.Count == 0)
            throw new KonnektorCredentialException(KonnektorText.L("Das P12 enthält kein Zertifikat.", "The P12 contains no certificate."));
        var leaf = new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(chainDer[0]);
        return new KonnektorClientCredentials(key, chainDer, DetectKeyType(key), leaf.NotAfter.ToLocalTime(), leaf.SubjectDN.ToString());
    }

    public static KonnektorClientCredentials Load(byte[] p12, string password)
    {
        Pkcs12Store store;
        try
        {
            store = new Pkcs12StoreBuilder().Build();
            using var ms = new MemoryStream(p12, writable: false);
            store.Load(ms, (password ?? "").ToCharArray());
        }
        catch (Exception ex) when (ex is IOException or InvalidCipherTextException or ArgumentException or Org.BouncyCastle.Security.GeneralSecurityException)
        {
            throw new KonnektorCredentialException(
                KonnektorText.L("P12 konnte nicht geöffnet werden – Passwort falsch oder Datei beschädigt.",
                    "P12 could not be opened – wrong password or corrupted file."), ex);
        }

        var alias = store.Aliases.FirstOrDefault(store.IsKeyEntry)
                    ?? throw new KonnektorCredentialException(KonnektorText.L(
                        "Das P12 enthält keinen privaten Schlüssel.", "The P12 contains no private key."));

        var key = store.GetKey(alias).Key;
        var chain = store.GetCertificateChain(alias)?.Select(e => e.Certificate.GetEncoded()).ToList()
                    ?? new List<byte[]>();
        if (chain.Count == 0 && store.GetCertificate(alias) is { } single)
            chain.Add(single.Certificate.GetEncoded());

        return Create(key, chain);
    }

    public static KonnektorKeyType DetectKeyType(AsymmetricKeyParameter key)
    {
        switch (key)
        {
            case RsaKeyParameters { IsPrivate: true }:
                return KonnektorKeyType.Rsa;
            case ECPrivateKeyParameters ec:
            {
                var oid = ec.PublicKeyParamSet ?? FindCurveOid(ec.Parameters);
                if (oid is not null)
                {
                    if (oid.Equals(SecObjectIdentifiers.SecP256r1)) return KonnektorKeyType.EcSecp256r1;
                    if (oid.Equals(SecObjectIdentifiers.SecP384r1)) return KonnektorKeyType.EcSecp384r1;
                    if (oid.Equals(TeleTrusTObjectIdentifiers.BrainpoolP256R1)) return KonnektorKeyType.EcBrainpoolP256r1;
                    if (oid.Equals(TeleTrusTObjectIdentifiers.BrainpoolP384R1)) return KonnektorKeyType.EcBrainpoolP384r1;
                    if (oid.Equals(TeleTrusTObjectIdentifiers.BrainpoolP512R1)) return KonnektorKeyType.EcBrainpoolP512r1;
                }
                throw new KonnektorCredentialException(KonnektorText.L(
                    "Nicht unterstützte EC-Kurve im Clientzertifikat (unterstützt: brainpoolP256r1/P384r1/P512r1, secp256r1, secp384r1).",
                    "Unsupported EC curve in client certificate (supported: brainpoolP256r1/P384r1/P512r1, secp256r1, secp384r1)."));
            }
            default:
                throw new KonnektorCredentialException(KonnektorText.L(
                    $"Nicht unterstützter Schlüsseltyp im Clientzertifikat ({key.GetType().Name}); unterstützt sind RSA und ECDSA.",
                    $"Unsupported key type in client certificate ({key.GetType().Name}); RSA and ECDSA are supported."));
        }
    }

    private static DerObjectIdentifier? FindCurveOid(ECDomainParameters parameters)
    {
        foreach (var oid in new[]
                 {
                     SecObjectIdentifiers.SecP256r1, SecObjectIdentifiers.SecP384r1,
                     TeleTrusTObjectIdentifiers.BrainpoolP256R1, TeleTrusTObjectIdentifiers.BrainpoolP384R1,
                     TeleTrusTObjectIdentifiers.BrainpoolP512R1
                 })
        {
            var named = Org.BouncyCastle.Asn1.X9.ECNamedCurveTable.GetByOid(oid);
            if (named is not null && named.Curve.Equals(parameters.Curve) && named.G.Equals(parameters.G))
                return oid;
        }
        return null;
    }
}
