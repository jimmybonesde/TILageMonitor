using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace TILageMonitor.Konnektor;

/// <summary>TLS failure with a readable DE/EN message (includes the TLS alert description).</summary>
public sealed class KonnektorTlsException : Exception
{
    public short? Alert { get; }
    public bool PinFailure { get; }

    public KonnektorTlsException(string message, short? alert = null, bool pinFailure = false, Exception? inner = null)
        : base(message, inner)
    {
        Alert = alert;
        PinFailure = pinFailure;
    }
}

/// <summary>
/// BouncyCastle TLS client for the Konnektor: TLS 1.2 (optionally 1.3) with the gematik
/// TR-03116-3 cipher suites, brainpool + NIST groups and signature schemes. Server trust is
/// decided only by the pinned SHA-256 fingerprint; client auth uses RSA or ECDSA keys from P12.
/// </summary>
public sealed class KonnektorTlsClient : DefaultTlsClient
{
    public static readonly int[] Tls12CipherSuites =
    [
        CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
        CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384,
        CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
        CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
        CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256,
        CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA384,
        CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256,
        CipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384
    ];

    public static readonly int[] Tls13CipherSuites =
    [
        CipherSuite.TLS_AES_128_GCM_SHA256,
        CipherSuite.TLS_AES_256_GCM_SHA384
    ];

    public static readonly int[] Groups =
    [
        NamedGroup.brainpoolP256r1,       // 26 (TLS 1.2)
        NamedGroup.brainpoolP384r1,       // 27
        NamedGroup.brainpoolP512r1,       // 28
        NamedGroup.secp256r1,
        NamedGroup.secp384r1,
        NamedGroup.brainpoolP256r1tls13,  // 31 (TLS 1.3)
        NamedGroup.brainpoolP384r1tls13,
        NamedGroup.brainpoolP512r1tls13
    ];

    public static readonly int[] SignatureSchemes =
    [
        SignatureScheme.ecdsa_brainpoolP256r1tls13_sha256,
        SignatureScheme.ecdsa_brainpoolP384r1tls13_sha384,
        SignatureScheme.ecdsa_brainpoolP512r1tls13_sha512,
        SignatureScheme.ecdsa_secp256r1_sha256,   // = (sha256, ecdsa) in TLS 1.2
        SignatureScheme.ecdsa_secp384r1_sha384,   // = (sha384, ecdsa) in TLS 1.2
        SignatureScheme.rsa_pss_rsae_sha256,
        SignatureScheme.rsa_pss_rsae_sha384,
        SignatureScheme.rsa_pkcs1_sha256,
        SignatureScheme.rsa_pkcs1_sha384
    ];

    private readonly string? _pinnedFingerprint;
    private readonly KonnektorClientCredentials? _credentials;
    private readonly bool _allowTls13;
    private readonly string? _sniHost;

    public KonnektorServerCertificate? ServerCertificate { get; private set; }
    public PinEvaluation Pin { get; private set; } = PinEvaluation.NotPinned;

    public KonnektorTlsClient(string? pinnedFingerprint, KonnektorClientCredentials? credentials, bool allowTls13 = true, string? sniHost = null)
        : base(new BcTlsCrypto(new SecureRandom()))
    {
        _pinnedFingerprint = pinnedFingerprint;
        _credentials = credentials;
        _allowTls13 = allowTls13;
        _sniHost = sniHost;
    }

    protected override ProtocolVersion[] GetSupportedVersions() =>
        _allowTls13
            ? ProtocolVersion.TLSv13.DownTo(ProtocolVersion.TLSv12)
            : ProtocolVersion.TLSv12.Only();

    protected override int[] GetSupportedCipherSuites() =>
        TlsUtilities.GetSupportedCipherSuites(Crypto,
            _allowTls13 ? Tls13CipherSuites.Concat(Tls12CipherSuites).ToArray() : Tls12CipherSuites);

    protected override IList<int> GetSupportedGroups(IList<int> namedGroupRoles) =>
        Groups.Where(g => Crypto.HasNamedGroup(g)).ToList();

    protected override IList<SignatureAndHashAlgorithm> GetSupportedSignatureAlgorithms() =>
        SignatureSchemes.Select(SignatureScheme.GetSignatureAndHashAlgorithm).ToList();

    protected override IList<ServerName>? GetSniServerNames()
    {
        if (string.IsNullOrWhiteSpace(_sniHost) || System.Net.IPAddress.TryParse(_sniHost, out _))
            return null;
        return new List<ServerName> { new(NameType.host_name, System.Text.Encoding.ASCII.GetBytes(_sniHost)) };
    }

    public override TlsAuthentication GetAuthentication() => new Authentication(this);

    private sealed class Authentication : TlsAuthentication
    {
        private readonly KonnektorTlsClient _owner;

        public Authentication(KonnektorTlsClient owner) => _owner = owner;

        public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
        {
            var chain = serverCertificate.Certificate;
            if (chain is null || chain.IsEmpty)
                throw new TlsFatalAlert(AlertDescription.bad_certificate);

            var der = chain.GetCertificateAt(0).GetEncoded();
            var x509 = new Org.BouncyCastle.X509.X509CertificateParser().ReadCertificate(der);
            _owner.ServerCertificate = new KonnektorServerCertificate(
                CertificatePinning.Fingerprint(der),
                x509.SubjectDN.ToString(),
                x509.IssuerDN.ToString(),
                x509.NotBefore.ToLocalTime(),
                x509.NotAfter.ToLocalTime());
            _owner.Pin = CertificatePinning.Evaluate(_owner.ServerCertificate.Sha256Fingerprint, _owner._pinnedFingerprint);

            // Strict: only a matching pin proceeds. NotPinned stops before any application data is sent.
            if (_owner.Pin != PinEvaluation.Match)
                throw new TlsFatalAlert(AlertDescription.bad_certificate);
        }

        public TlsCredentials? GetClientCredentials(CertificateRequest certificateRequest)
        {
            var creds = _owner._credentials;
            if (creds is null)
                return null;

            var context = _owner.m_context;
            var tls13 = TlsUtilities.IsTlsV13(context.ServerVersion);
            var offered = certificateRequest.SupportedSignatureAlgorithms;
            var sigAlg = SelectSignatureAlgorithm(creds.KeyType, tls13, offered)
                         ?? throw new TlsFatalAlert(AlertDescription.handshake_failure);

            var crypto = (BcTlsCrypto)_owner.Crypto;
            var tlsCerts = creds.ChainDer.Select(d => (TlsCertificate)new BcTlsCertificate(crypto, d)).ToArray();
            var certificate = tls13
                ? new Certificate(certificateRequest.GetCertificateRequestContext(),
                    tlsCerts.Select(c => new CertificateEntry(c, null)).ToArray())
                : new Certificate(tlsCerts);

            return new BcDefaultTlsCredentialedSigner(new TlsCryptoParameters(context), crypto, creds.PrivateKey, certificate, sigAlg);
        }
    }

    /// <summary>Picks the client signature algorithm for the key type from what the server offered.</summary>
    public static SignatureAndHashAlgorithm? SelectSignatureAlgorithm(
        KonnektorKeyType keyType, bool tls13, IList<SignatureAndHashAlgorithm>? offered)
    {
        int[] preferred = (keyType, tls13) switch
        {
            (KonnektorKeyType.Rsa, true) => [SignatureScheme.rsa_pss_rsae_sha256, SignatureScheme.rsa_pss_rsae_sha384],
            (KonnektorKeyType.Rsa, false) => [SignatureScheme.rsa_pkcs1_sha256, SignatureScheme.rsa_pkcs1_sha384, SignatureScheme.rsa_pss_rsae_sha256, SignatureScheme.rsa_pss_rsae_sha384],
            (KonnektorKeyType.EcBrainpoolP256r1, true) => [SignatureScheme.ecdsa_brainpoolP256r1tls13_sha256],
            (KonnektorKeyType.EcBrainpoolP384r1, true) => [SignatureScheme.ecdsa_brainpoolP384r1tls13_sha384],
            (KonnektorKeyType.EcBrainpoolP512r1, true) => [SignatureScheme.ecdsa_brainpoolP512r1tls13_sha512],
            (KonnektorKeyType.EcSecp256r1, true) => [SignatureScheme.ecdsa_secp256r1_sha256],
            (KonnektorKeyType.EcSecp384r1, true) => [SignatureScheme.ecdsa_secp384r1_sha384],
            // TLS 1.2: ECDSA hash is independent of the curve; prefer the curve-matching size.
            (KonnektorKeyType.EcSecp384r1 or KonnektorKeyType.EcBrainpoolP384r1 or KonnektorKeyType.EcBrainpoolP512r1, false)
                => [SignatureScheme.ecdsa_secp384r1_sha384, SignatureScheme.ecdsa_secp256r1_sha256],
            _ => [SignatureScheme.ecdsa_secp256r1_sha256, SignatureScheme.ecdsa_secp384r1_sha384]
        };

        foreach (var scheme in preferred)
        {
            var alg = SignatureScheme.GetSignatureAndHashAlgorithm(scheme);
            if (offered is null || offered.Count == 0 || offered.Contains(alg))
                return alg;
        }

        return null;
    }

    /// <summary>Human readable message for BC TLS exceptions.</summary>
    public static KonnektorTlsException Translate(Exception ex, KonnektorTlsClient client)
    {
        if (client.Pin != PinEvaluation.Match && client.ServerCertificate is not null)
        {
            return new KonnektorTlsException(client.Pin == PinEvaluation.NotPinned
                    ? KonnektorText.L("Konnektor-Zertifikat ist noch nicht vertraut.", "Connector certificate is not trusted yet.")
                    : KonnektorText.L("Konnektor-Zertifikat passt nicht zum vertrauten Fingerprint.", "Connector certificate does not match the trusted fingerprint."),
                AlertDescription.bad_certificate, pinFailure: true, inner: ex);
        }

        return ex switch
        {
            TlsFatalAlertReceived received => new KonnektorTlsException(
                KonnektorText.L($"TLS-Handshake vom Konnektor abgelehnt: {AlertDescription.GetText(received.AlertDescription)}",
                    $"TLS handshake rejected by connector: {AlertDescription.GetText(received.AlertDescription)}")
                + Hint(received.AlertDescription), received.AlertDescription, inner: ex),
            TlsFatalAlert local => new KonnektorTlsException(
                KonnektorText.L($"TLS-Handshake fehlgeschlagen: {AlertDescription.GetText(local.AlertDescription)}",
                    $"TLS handshake failed: {AlertDescription.GetText(local.AlertDescription)}")
                + (string.IsNullOrWhiteSpace(local.Message) ? "" : $" ({local.Message})"), local.AlertDescription, inner: ex),
            _ => new KonnektorTlsException(KonnektorText.L("TLS-Fehler: ", "TLS error: ") + ex.Message, inner: ex)
        };
    }

    private static string Hint(short alert) => alert switch
    {
        AlertDescription.bad_certificate or AlertDescription.certificate_unknown or AlertDescription.unknown_ca or AlertDescription.certificate_required
            => KonnektorText.L(" – Clientzertifikat fehlt oder ist im Konnektor nicht hinterlegt.", " – client certificate missing or not registered in the connector."),
        AlertDescription.handshake_failure or AlertDescription.insufficient_security
            => KonnektorText.L(" – keine gemeinsame Cipher-Suite/Kurve.", " – no common cipher suite/curve."),
        _ => ""
    };
}
