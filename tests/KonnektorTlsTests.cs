using System.Net;
using System.Net.Sockets;
using System.Text;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.TeleTrust;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Org.BouncyCastle.X509;
using TILageMonitor.Konnektor;
using Xunit;

namespace TILageMonitor.Tests;

/// <summary>Self-signed test material generated with BouncyCastle.</summary>
internal static class TestPki
{
    private static readonly SecureRandom Random = new();

    public static AsymmetricCipherKeyPair EcKey(Org.BouncyCastle.Asn1.DerObjectIdentifier curve)
    {
        var gen = new ECKeyPairGenerator("EC");
        gen.Init(new ECKeyGenerationParameters(curve, Random));
        return gen.GenerateKeyPair();
    }

    public static AsymmetricCipherKeyPair RsaKey()
    {
        var gen = new RsaKeyPairGenerator();
        gen.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(65537), Random, 2048, 80));
        return gen.GenerateKeyPair();
    }

    public static byte[] SelfSigned(AsymmetricCipherKeyPair key, string cn)
    {
        var gen = new X509V3CertificateGenerator();
        var name = new X509Name($"CN={cn},O=TILageMonitor Test");
        gen.SetSerialNumber(BigInteger.ProbablePrime(64, Random));
        gen.SetIssuerDN(name);
        gen.SetSubjectDN(name);
        gen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
        gen.SetNotAfter(DateTime.UtcNow.AddDays(365));
        gen.SetPublicKey(key.Public);
        var alg = key.Private is RsaKeyParameters ? "SHA256WITHRSA" : "SHA256WITHECDSA";
        return gen.Generate(new Asn1SignatureFactory(alg, key.Private, Random)).GetEncoded();
    }

    public static byte[] ToP12(AsymmetricCipherKeyPair key, byte[] certDer, string password)
    {
        var store = new Pkcs12StoreBuilder().Build();
        var cert = new X509CertificateParser().ReadCertificate(certDer);
        store.SetKeyEntry("client", new AsymmetricKeyEntry(key.Private), [new X509CertificateEntry(cert)]);
        using var ms = new MemoryStream();
        store.Save(ms, password.ToCharArray(), Random);
        return ms.ToArray();
    }
}

/// <summary>Loopback BouncyCastle TLS server requiring client auth, answering one HTTP request.</summary>
internal sealed class LoopbackTlsServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly AsymmetricCipherKeyPair _key;
    private readonly byte[] _certDer;
    private readonly bool _tls13;
    private readonly Func<string, byte[]> _respond;
    public Task Completion { get; }
    public byte[]? ClientCertificateDer { get; private set; }
    public string? ReceivedRequest { get; private set; }
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public LoopbackTlsServer(AsymmetricCipherKeyPair key, byte[] certDer, bool tls13, Func<string, byte[]> respond)
    {
        _key = key;
        _certDer = certDer;
        _tls13 = tls13;
        _respond = respond;
        _listener.Start();
        Completion = Task.Run(Serve);
    }

    private void Serve()
    {
        using var tcp = _listener.AcceptTcpClient();
        tcp.ReceiveTimeout = 10000;
        var protocol = new TlsServerProtocol(tcp.GetStream());
        var server = new Server(this);
        try
        {
            protocol.Accept(server);
            var stream = protocol.Stream;
            var sb = new StringBuilder();
            var buf = new byte[4096];
            while (!sb.ToString().Contains("\r\n\r\n"))
            {
                var n = stream.Read(buf, 0, buf.Length);
                if (n <= 0) break;
                sb.Append(Encoding.ASCII.GetString(buf, 0, n));
            }
            ReceivedRequest = sb.ToString();
            var response = _respond(ReceivedRequest);
            stream.Write(response, 0, response.Length);
            stream.Flush();
        }
        catch (TlsFatalAlertReceived)
        {
            // client aborted (e.g. pin mismatch) — expected in negative tests
        }
        finally
        {
            try { protocol.Close(); } catch { /* ignore */ }
        }
    }

    public void Dispose() => _listener.Stop();

    private sealed class Server : DefaultTlsServer
    {
        private readonly LoopbackTlsServer _o;
        public Server(LoopbackTlsServer owner) : base(new BcTlsCrypto(new SecureRandom())) => _o = owner;

        protected override ProtocolVersion[] GetSupportedVersions() =>
            _o._tls13 ? ProtocolVersion.TLSv13.Only() : ProtocolVersion.TLSv12.Only();

        // Offer only the suites matching the server key (credentials are supplied directly).
        protected override int[] GetSupportedCipherSuites() =>
            TlsUtilities.GetSupportedCipherSuites(Crypto, KonnektorTlsClient.Tls13CipherSuites
                .Concat(KonnektorTlsClient.Tls12CipherSuites.Where(cs =>
                    TlsUtilities.GetKeyExchangeAlgorithm(cs) ==
                    (_o._key.Private is RsaKeyParameters ? KeyExchangeAlgorithm.ECDHE_RSA : KeyExchangeAlgorithm.ECDHE_ECDSA)))
                .ToArray());

        public override int[] GetSupportedGroups() =>
            [NamedGroup.brainpoolP256r1, NamedGroup.brainpoolP256r1tls13, NamedGroup.secp256r1];

        public override TlsCredentials GetCredentials()
        {
            var keyType = KonnektorClientCredentials.DetectKeyType(_o._key.Private);
            var tls13 = TlsUtilities.IsTlsV13(m_context.ServerVersion);
            var sigAlg = KonnektorTlsClient.SelectSignatureAlgorithm(keyType, tls13, m_context.SecurityParameters.ClientSigAlgs)!;
            var crypto = (BcTlsCrypto)Crypto;
            var tlsCert = new BcTlsCertificate(crypto, _o._certDer);
            var certificate = tls13
                ? new Certificate(TlsUtilities.EmptyBytes, [new CertificateEntry(tlsCert, null)])
                : new Certificate([tlsCert]);
            return new BcDefaultTlsCredentialedSigner(new TlsCryptoParameters(m_context), crypto, _o._key.Private, certificate, sigAlg);
        }

        public override CertificateRequest GetCertificateRequest()
        {
            var algs = KonnektorTlsClient.SignatureSchemes.Select(SignatureScheme.GetSignatureAndHashAlgorithm).ToList();
            return TlsUtilities.IsTlsV13(m_context.ServerVersion)
                ? new CertificateRequest(TlsUtilities.EmptyBytes, algs, null, null)
                : new CertificateRequest([ClientCertificateType.ecdsa_sign, ClientCertificateType.rsa_sign], algs, null);
        }

        public override void NotifyClientCertificate(Certificate clientCertificate)
        {
            if (clientCertificate is null || clientCertificate.IsEmpty)
                throw new TlsFatalAlert(AlertDescription.certificate_required);
            _o.ClientCertificateDer = clientCertificate.GetCertificateAt(0).GetEncoded();
        }
    }
}

public sealed class KonnektorTlsTests
{
    private static byte[] ChunkedSds(string _) => Encoding.ASCII.GetBytes(
        "HTTP/1.1 200 OK\r\nContent-Type: text/xml; charset=utf-8\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n" +
        "5\r\n<a>He\r\n" + "7;ext=1\r\nllo</a>\r\n" + "0\r\n\r\n");

    private static async Task<(KonnektorHttpResponse? Response, Exception? Error, BcTlsHttpTransport Transport, LoopbackTlsServer Server)> RunAsync(
        AsymmetricCipherKeyPair serverKey, AsymmetricCipherKeyPair clientKey, bool tls13, Func<byte[], string?> pin,
        Func<string, byte[]>? respond = null, string method = "GET", byte[]? body = null)
    {
        var serverCert = TestPki.SelfSigned(serverKey, "konnektor.test");
        var clientCert = TestPki.SelfSigned(clientKey, "clientsystem");
        var p12 = TestPki.ToP12(clientKey, clientCert, "pa$$");
        var creds = KonnektorClientCredentials.Load(p12, "pa$$");

        var server = new LoopbackTlsServer(serverKey, serverCert, tls13, respond ?? ChunkedSds);
        var transport = new BcTlsHttpTransport(() => pin(serverCert), creds, TimeSpan.FromSeconds(20));
        try
        {
            var headers = new Dictionary<string, string> { ["SOAPAction"] = "\"urn:x#Op\"" };
            var response = await transport.SendAsync(
                new KonnektorHttpRequest(method, new Uri($"https://127.0.0.1:{server.Port}/connector.sds"), headers, body, "text/xml"),
                CancellationToken.None);
            await server.Completion.WaitAsync(TimeSpan.FromSeconds(20));
            return (response, null, transport, server);
        }
        catch (Exception ex)
        {
            try { await server.Completion.WaitAsync(TimeSpan.FromSeconds(20)); } catch { /* ignore */ }
            return (null, ex, transport, server);
        }
        finally
        {
            server.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Brainpool_handshake_with_client_auth_pin_match_and_chunked_get(bool tls13)
    {
        var serverKey = TestPki.EcKey(TeleTrusTObjectIdentifiers.BrainpoolP256R1);
        var clientKey = TestPki.EcKey(TeleTrusTObjectIdentifiers.BrainpoolP256R1);

        var (response, error, transport, server) = await RunAsync(serverKey, clientKey, tls13, CertificatePinning.Fingerprint);

        Assert.Null(error);
        Assert.Equal(200, response!.StatusCode);
        Assert.Equal("<a>Hello</a>", response.BodyText);
        Assert.Equal(PinEvaluation.Match, transport.LastPin);
        Assert.NotNull(server.ClientCertificateDer);
        Assert.Contains("CN=konnektor.test", transport.LastServerCertificate!.Subject);
        Assert.StartsWith("GET /connector.sds HTTP/1.1", server.ReceivedRequest);
        Assert.Contains("Connection: close", server.ReceivedRequest);
        Assert.Contains($"Host: 127.0.0.1:", server.ReceivedRequest);
    }

    [Fact]
    public async Task Brainpool_P384_client_key_is_supported()
    {
        var (response, error, _, server) = await RunAsync(
            TestPki.EcKey(TeleTrusTObjectIdentifiers.BrainpoolP256R1),
            TestPki.EcKey(TeleTrusTObjectIdentifiers.BrainpoolP384R1),
            tls13: false, CertificatePinning.Fingerprint);
        Assert.Null(error);
        Assert.Equal(200, response!.StatusCode);
        Assert.NotNull(server.ClientCertificateDer);
    }

    [Fact]
    public async Task Rsa_server_and_client_with_content_length_post()
    {
        var (response, error, transport, server) = await RunAsync(
            TestPki.RsaKey(), TestPki.RsaKey(), tls13: false, CertificatePinning.Fingerprint,
            respond: _ => Encoding.UTF8.GetBytes("HTTP/1.1 500 Internal Server Error\r\nContent-Length: 5\r\n\r\nFault"),
            method: "POST", body: Encoding.UTF8.GetBytes("<x/>"));

        Assert.Null(error);
        Assert.Equal(500, response!.StatusCode);
        Assert.Equal("Fault", response.BodyText);
        Assert.Equal(PinEvaluation.Match, transport.LastPin);
        Assert.Contains("SOAPAction: \"urn:x#Op\"", server.ReceivedRequest);
        Assert.Contains("Content-Length: 4", server.ReceivedRequest);
        Assert.NotNull(server.ClientCertificateDer);
    }

    [Fact]
    public async Task Pin_mismatch_aborts_handshake()
    {
        var other = CertificatePinning.Fingerprint(Encoding.ASCII.GetBytes("not the server"));
        var (response, error, transport, server) = await RunAsync(
            TestPki.EcKey(TeleTrusTObjectIdentifiers.BrainpoolP256R1),
            TestPki.EcKey(TeleTrusTObjectIdentifiers.BrainpoolP256R1),
            tls13: false, _ => other);

        Assert.Null(response);
        var tls = Assert.IsType<KonnektorTlsException>(error);
        Assert.True(tls.PinFailure);
        Assert.Equal(PinEvaluation.Mismatch, transport.LastPin);
        Assert.NotNull(transport.LastServerCertificate);
        Assert.Null(server.ReceivedRequest); // no application data was sent
    }

    [Fact]
    public async Task Unpinned_server_reports_certificate_for_trust_dialog()
    {
        var (_, error, transport, server) = await RunAsync(
            TestPki.EcKey(SecObjectIdentifiers.SecP256r1),
            TestPki.EcKey(SecObjectIdentifiers.SecP256r1),
            tls13: false, _ => null);

        Assert.True(Assert.IsType<KonnektorTlsException>(error).PinFailure);
        Assert.Equal(PinEvaluation.NotPinned, transport.LastPin);
        Assert.Matches("^([0-9A-F]{2}:){31}[0-9A-F]{2}$", transport.LastServerCertificate!.Sha256Fingerprint);
        Assert.Null(server.ReceivedRequest);
    }

    [Fact]
    public void Wrong_p12_password_gives_clear_error()
    {
        var key = TestPki.EcKey(TeleTrusTObjectIdentifiers.BrainpoolP256R1);
        var p12 = TestPki.ToP12(key, TestPki.SelfSigned(key, "c"), "right");
        var ex = Assert.Throws<KonnektorCredentialException>(() => KonnektorClientCredentials.Load(p12, "wrong"));
        Assert.Contains("P12", ex.Message);
    }

    [Theory]
    [InlineData("bp256", KonnektorKeyType.EcBrainpoolP256r1)]
    [InlineData("bp384", KonnektorKeyType.EcBrainpoolP384r1)]
    [InlineData("p256", KonnektorKeyType.EcSecp256r1)]
    [InlineData("p384", KonnektorKeyType.EcSecp384r1)]
    [InlineData("rsa", KonnektorKeyType.Rsa)]
    public void Key_types_are_detected_from_p12(string kind, KonnektorKeyType expected)
    {
        var key = kind switch
        {
            "bp256" => TestPki.EcKey(TeleTrusTObjectIdentifiers.BrainpoolP256R1),
            "bp384" => TestPki.EcKey(TeleTrusTObjectIdentifiers.BrainpoolP384R1),
            "p256" => TestPki.EcKey(SecObjectIdentifiers.SecP256r1),
            "p384" => TestPki.EcKey(SecObjectIdentifiers.SecP384r1),
            _ => TestPki.RsaKey()
        };
        var creds = KonnektorClientCredentials.Load(TestPki.ToP12(key, TestPki.SelfSigned(key, "c"), "x"), "x");
        Assert.Equal(expected, creds.KeyType);
        Assert.True(creds.NotAfter > DateTime.Now);
    }

    [Fact]
    public void Unsupported_curve_is_rejected()
    {
        var key = TestPki.EcKey(SecObjectIdentifiers.SecP521r1);
        var ex = Assert.Throws<KonnektorCredentialException>(() =>
            KonnektorClientCredentials.Load(TestPki.ToP12(key, TestPki.SelfSigned(key, "c"), "x"), "x"));
        Assert.Contains("brainpool", ex.Message);
    }

    [Fact]
    public void Signature_algorithm_selection()
    {
        var all = KonnektorTlsClient.SignatureSchemes.Select(SignatureScheme.GetSignatureAndHashAlgorithm).ToList();
        Assert.Equal(SignatureScheme.ecdsa_brainpoolP256r1tls13_sha256,
            SignatureScheme.From(KonnektorTlsClient.SelectSignatureAlgorithm(KonnektorKeyType.EcBrainpoolP256r1, true, all)!));
        Assert.Equal(SignatureScheme.ecdsa_secp256r1_sha256,
            SignatureScheme.From(KonnektorTlsClient.SelectSignatureAlgorithm(KonnektorKeyType.EcBrainpoolP256r1, false, all)!));
        Assert.Equal(SignatureScheme.rsa_pkcs1_sha256,
            SignatureScheme.From(KonnektorTlsClient.SelectSignatureAlgorithm(KonnektorKeyType.Rsa, false, all)!));
        Assert.Null(KonnektorTlsClient.SelectSignatureAlgorithm(KonnektorKeyType.EcBrainpoolP256r1, true,
            [SignatureScheme.GetSignatureAndHashAlgorithm(SignatureScheme.rsa_pss_rsae_sha256)]));
    }

    [Fact]
    public void Http_parser_handles_content_length_and_read_to_close()
    {
        var r1 = BcTlsHttpTransport.ReadResponse(new MemoryStream(Encoding.ASCII.GetBytes(
            "HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nabcEXTRA")));
        Assert.Equal("abc", r1.BodyText);
        var r2 = BcTlsHttpTransport.ReadResponse(new MemoryStream(Encoding.ASCII.GetBytes(
            "HTTP/1.0 200 OK\r\nContent-Type: text/xml\r\n\r\n<sds/>")));
        Assert.Equal("<sds/>", r2.BodyText);
    }

    [Fact]
    public async Task Silent_server_times_out_and_cancellation_is_honoured()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var uri = new Uri($"https://127.0.0.1:{port}/connector.sds");

            var transport = new BcTlsHttpTransport(() => null, null, TimeSpan.FromSeconds(1));
            await Assert.ThrowsAsync<TimeoutException>(() => transport.SendAsync(new KonnektorHttpRequest("GET", uri), CancellationToken.None));

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
            var slow = new BcTlsHttpTransport(() => null, null, TimeSpan.FromSeconds(30));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slow.SendAsync(new KonnektorHttpRequest("GET", uri), cts.Token));
        }
        finally
        {
            listener.Stop();
        }
    }
}
