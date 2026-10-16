using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TILageMonitor.Konnektor;

/// <summary>
/// Read-only Konnektor client: connector.sds + EventService/CardService queries.
/// TLS server trust is decided solely by the pinned SHA-256 fingerprint.
/// </summary>
public sealed class KonnektorClient : IDisposable
{
    public const int CertificateWarningDays = 90;

    private readonly KonnektorSettings _settings;
    private readonly X509Certificate2? _clientCertificate;
    private readonly HttpClient _http;
    private X509Certificate2? _lastServerCertificate;
    private PinEvaluation _lastPin = PinEvaluation.NotPinned;

    public KonnektorClient(KonnektorSettings settings, X509Certificate2? clientCertificate, TimeSpan? timeout = null)
    {
        _settings = settings;
        _clientCertificate = clientCertificate;

        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(8),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                RemoteCertificateValidationCallback = ValidateServerCertificate,
                ClientCertificates = clientCertificate is null ? null : new X509CertificateCollection { clientCertificate },
                LocalCertificateSelectionCallback = clientCertificate is null
                    ? null
                    : (_, _, _, _, _) => clientCertificate
            }
        };

        _http = new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"TILageMonitor/{UpdateService.CurrentVersion}");
    }

    /// <summary>Loads the client system certificate (with private key) from PKCS#12 bytes.</summary>
    public static X509Certificate2 LoadClientCertificate(byte[] p12, string password)
    {
        var collection = X509CertificateLoader.LoadPkcs12Collection(
            p12, password, X509KeyStorageFlags.UserKeySet);
        var withKey = collection.FirstOrDefault(c => c.HasPrivateKey)
                      ?? throw new InvalidOperationException("Das P12 enthält kein Zertifikat mit privatem Schlüssel.");
        foreach (var other in collection.Where(c => !ReferenceEquals(c, withKey)))
            other.Dispose();
        return withKey;
    }

    private bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (certificate is null)
            return false;
        var raw = certificate.GetRawCertData();
        _lastServerCertificate = X509CertificateLoader.LoadCertificate(raw);
        _lastPin = CertificatePinning.Evaluate(CertificatePinning.Fingerprint(raw), _settings.PinnedFingerprint);
        return _lastPin == PinEvaluation.Match;
    }

    /// <summary>Runs SDS + all status calls; never throws for network/SOAP problems.</summary>
    public async Task<KonnektorSnapshot> CollectAsync(CancellationToken ct)
    {
        var snapshot = new KonnektorSnapshot { CheckedAt = DateTime.Now };
        var baseUri = KonnektorSettings.BuildBaseUri(_settings.Host, _settings.Port);

        try
        {
            var sdsXml = await GetStringAsync(new Uri(baseUri, "/connector.sds"), ct);
            snapshot.Sds = SdsParser.Parse(sdsXml);
            snapshot.Reachable = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ApplyTlsState(snapshot);
            snapshot.Error = snapshot.TrustRequired
                ? L("Zertifikat des Konnektors ist noch nicht vertrauenswürdig – bitte in den Einstellungen „Verbindung testen“ und bestätigen.",
                    "The connector certificate is not trusted yet – use “Test connection” in Settings and confirm.")
                : snapshot.PinMismatch
                    ? L("WARNUNG: Das TLS-Zertifikat des Konnektors hat sich geändert (Fingerprint passt nicht). Verbindung blockiert – bitte prüfen und neu vertrauen.",
                        "WARNING: The connector TLS certificate changed (fingerprint mismatch). Connection blocked – please verify and re-trust.")
                    : Describe(ex);
            return snapshot;
        }

        ApplyTlsState(snapshot);
        var ctx = _settings.Context;

        var evt = SdsParser.SelectVersion(snapshot.Sds.FindService("EventService"), 7);
        var card = SdsParser.SelectVersion(snapshot.Sds.FindService("CardService"), 8);

        if (evt is null)
        {
            snapshot.ResourceError = L("EventService nicht im SDS gefunden.", "EventService not found in SDS.");
        }
        else
        {
            var ns = evt.TargetNamespace ?? KonnektorSoap.DefaultEventServiceNs;
            var url = ResolveEndpoint(baseUri, evt.PreferredEndpoint!);

            try
            {
                snapshot.Resource = KonnektorSoap.ParseGetResourceInformation(
                    await PostSoapAsync(url, ns, "GetResourceInformation", KonnektorSoap.GetResourceInformationRequest(ns, ctx), ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                snapshot.ResourceError = Describe(ex);
            }

            try
            {
                snapshot.Cards.AddRange(KonnektorSoap.ParseGetCards(
                    await PostSoapAsync(url, ns, "GetCards", KonnektorSoap.GetCardsRequest(ns, ctx), ct)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                snapshot.CardsError = Describe(ex);
            }

            try
            {
                snapshot.Terminals.AddRange(KonnektorSoap.ParseGetCardTerminals(
                    await PostSoapAsync(url, ns, "GetCardTerminals", KonnektorSoap.GetCardTerminalsRequest(ns, ctx), ct)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                snapshot.TerminalsError = Describe(ex);
            }
        }

        if (card is not null)
        {
            var ns = card.TargetNamespace ?? KonnektorSoap.DefaultCardServiceNs;
            var url = ResolveEndpoint(baseUri, card.PreferredEndpoint!);
            foreach (var c in snapshot.Cards)
            {
                var pinType = KonnektorSoap.PinTypeFor(c.CardType);
                if (pinType is null || string.IsNullOrWhiteSpace(c.CardHandle))
                    continue;
                c.PinType = pinType;
                try
                {
                    var (status, tries) = KonnektorSoap.ParseGetPinStatus(
                        await PostSoapAsync(url, ns, "GetPinStatus",
                            KonnektorSoap.GetPinStatusRequest(ns, ctx, c.CardHandle!, pinType), ct));
                    c.PinStatus = status;
                    c.PinLeftTries = tries;
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    c.PinError = Describe(ex);
                }
            }
        }

        KonnektorAnalysis.AddWarnings(snapshot, _clientCertificate?.NotAfter, DateTime.Now);
        return snapshot;
    }

    /// <summary>
    /// Connects once to read the server certificate for the trust dialog (no data is sent
    /// beyond the TLS handshake and GET /connector.sds when already trusted).
    /// </summary>
    public async Task<(KonnektorServerCertificate? Certificate, PinEvaluation Pin, string? Error)> ProbeCertificateAsync(CancellationToken ct)
    {
        var baseUri = KonnektorSettings.BuildBaseUri(_settings.Host, _settings.Port);
        string? error = null;
        try
        {
            using var response = await _http.GetAsync(new Uri(baseUri, "/connector.sds"), HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            error = Describe(ex);
        }

        var cert = _lastServerCertificate is null ? null : CertificatePinning.Describe(_lastServerCertificate);
        return (cert, _lastPin, cert is null ? error : null);
    }

    private void ApplyTlsState(KonnektorSnapshot snapshot)
    {
        if (_lastServerCertificate is not null)
            snapshot.ServerCertificate = CertificatePinning.Describe(_lastServerCertificate);
        snapshot.TrustRequired = _lastServerCertificate is not null && _lastPin == PinEvaluation.NotPinned;
        snapshot.PinMismatch = _lastServerCertificate is not null && _lastPin == PinEvaluation.Mismatch;
    }

    /// <summary>SDS endpoints may be absolute or relative; absolute ones are used as published.</summary>
    public static Uri ResolveEndpoint(Uri baseUri, string location) =>
        Uri.TryCreate(location, UriKind.Absolute, out var abs) && (abs.Scheme == Uri.UriSchemeHttps || abs.Scheme == Uri.UriSchemeHttp)
            ? abs
            : new Uri(baseUri, location);

    private async Task<string> GetStringAsync(Uri uri, CancellationToken ct)
    {
        using var response = await _http.GetAsync(uri, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    private async Task<string> PostSoapAsync(Uri url, string ns, string operation, string envelope, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
        };
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{KonnektorSoap.SoapAction(ns, operation)}\"");
        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        // SOAP faults usually come with HTTP 500 — parse them for a readable message.
        if (!response.IsSuccessStatusCode && !body.Contains("Fault", StringComparison.Ordinal))
            response.EnsureSuccessStatusCode();
        return body;
    }

    private static string Describe(Exception ex) => ex switch
    {
        KonnektorSoapFaultException f => L("SOAP-Fehler: ", "SOAP fault: ") + f.Message,
        TaskCanceledException => L("Zeitüberschreitung bei der Verbindung zum Konnektor.", "Connection to the connector timed out."),
        HttpRequestException h when h.InnerException is AuthenticationException a => L("TLS-Fehler: ", "TLS error: ") + a.Message,
        HttpRequestException h => L("Konnektor nicht erreichbar: ", "Connector unreachable: ") + (h.InnerException?.Message ?? h.Message),
        System.Xml.XmlException x => L("Ungültige XML-Antwort: ", "Invalid XML response: ") + x.Message,
        _ => ex.Message
    };

    internal static string L(string de, string en) => LocalizationService.IsGerman ? de : en;

    public void Dispose()
    {
        _http.Dispose();
        _lastServerCertificate?.Dispose();
    }
}

/// <summary>Pure analysis helpers (testable): expiry and PIN warnings.</summary>
public static class KonnektorAnalysis
{
    public static void AddWarnings(KonnektorSnapshot snapshot, DateTime? clientCertNotAfter, DateTime now)
    {
        foreach (var card in snapshot.Cards)
        {
            var label = $"{card.CardType} {card.CardHolderName ?? card.Iccsn}".Trim();
            if (IsExpiringSoon(card.CertificateExpirationDate, now, out var days))
                snapshot.Warnings.Add(days < 0
                    ? KonnektorClient.L($"Kartenzertifikat abgelaufen: {label}", $"Card certificate expired: {label}")
                    : KonnektorClient.L($"Kartenzertifikat läuft in {days} Tagen ab: {label}", $"Card certificate expires in {days} days: {label}"));

            if (card.PinStatus is not null && IsPinProblem(card.PinStatus, card.CardType))
                snapshot.Warnings.Add(KonnektorClient.L($"PIN-Status {card.PinStatus}: {label}", $"PIN status {card.PinStatus}: {label}"));
        }

        if (IsExpiringSoon(clientCertNotAfter, now, out var clientDays))
            snapshot.Warnings.Add(KonnektorClient.L($"Clientsystem-Zertifikat läuft in {clientDays} Tagen ab.", $"Client system certificate expires in {clientDays} days."));

        if (IsExpiringSoon(snapshot.ServerCertificate?.NotAfter, now, out var serverDays))
            snapshot.Warnings.Add(KonnektorClient.L($"TLS-Zertifikat des Konnektors läuft in {serverDays} Tagen ab.", $"Connector TLS certificate expires in {serverDays} days."));
    }

    public static bool IsExpiringSoon(DateTime? notAfter, DateTime now, out int days)
    {
        days = 0;
        if (notAfter is null)
            return false;
        days = (int)Math.Floor((notAfter.Value - now).TotalDays);
        return days < KonnektorClient.CertificateWarningDays;
    }

    /// <summary>
    /// TRANSPORT_PIN, EMPTY_PIN, BLOCKED, DISABLED always need action. For the SMC-B,
    /// VERIFIABLE means "not unlocked" — practice software cannot use it until PIN.SMC is entered.
    /// For the HBA, VERIFIABLE is the normal resting state.
    /// </summary>
    public static bool IsPinProblem(string pinStatus, string? cardType = null)
    {
        var status = pinStatus.Trim().ToUpperInvariant();
        if (status is "TRANSPORT_PIN" or "EMPTY_PIN" or "BLOCKED" or "DISABLED")
            return true;
        return status == "VERIFIABLE" && string.Equals(cardType?.Trim(), "SMC-B", StringComparison.OrdinalIgnoreCase);
    }
}
