using System.Text;

namespace TILageMonitor.Konnektor;

/// <summary>
/// Read-only Konnektor client: connector.sds + EventService/CardService queries.
/// Uses a BouncyCastle TLS transport (brainpool/ECDSA/RSA client auth); TLS server trust
/// is decided solely by the pinned SHA-256 fingerprint.
/// </summary>
public sealed class KonnektorClient : IDisposable
{
    public const int CertificateWarningDays = 90;

    private readonly KonnektorSettings _settings;
    private readonly KonnektorClientCredentials? _credentials;
    private readonly IKonnektorHttpTransport _transport;

    public KonnektorClient(KonnektorSettings settings, KonnektorClientCredentials? credentials, TimeSpan? timeout = null, IKonnektorHttpTransport? transport = null)
    {
        _settings = settings;
        _credentials = credentials;
        _transport = transport ?? new BcTlsHttpTransport(() => _settings.PinnedFingerprint, credentials, timeout ?? TimeSpan.FromSeconds(20));
    }

    /// <summary>Loads the client system credentials (RSA/ECDSA incl. brainpool) from PKCS#12 bytes.</summary>
    public static KonnektorClientCredentials LoadClientCertificate(byte[] p12, string password) =>
        KonnektorClientCredentials.Load(p12, password);

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

        KonnektorAnalysis.AddWarnings(snapshot, _credentials?.NotAfter, DateTime.Now);
        return snapshot;
    }

    /// <summary>
    /// Connects once to read the server certificate for the trust dialog. When not yet trusted
    /// the handshake is aborted right after the server certificate — no request is sent.
    /// </summary>
    public async Task<(KonnektorServerCertificate? Certificate, PinEvaluation Pin, string? Error)> ProbeCertificateAsync(CancellationToken ct)
    {
        var baseUri = KonnektorSettings.BuildBaseUri(_settings.Host, _settings.Port);
        string? error = null;
        try
        {
            await _transport.SendAsync(new KonnektorHttpRequest("GET", new Uri(baseUri, "/connector.sds")), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            error = Describe(ex);
        }

        var cert = _transport.LastServerCertificate;
        return (cert, _transport.LastPin, cert is null ? error : null);
    }

    private void ApplyTlsState(KonnektorSnapshot snapshot)
    {
        var cert = _transport.LastServerCertificate;
        if (cert is not null)
            snapshot.ServerCertificate = cert;
        snapshot.TrustRequired = cert is not null && _transport.LastPin == PinEvaluation.NotPinned;
        snapshot.PinMismatch = cert is not null && _transport.LastPin == PinEvaluation.Mismatch;
    }

    /// <summary>SDS endpoints may be absolute or relative; absolute ones are used as published.</summary>
    public static Uri ResolveEndpoint(Uri baseUri, string location) =>
        Uri.TryCreate(location, UriKind.Absolute, out var abs) && (abs.Scheme == Uri.UriSchemeHttps || abs.Scheme == Uri.UriSchemeHttp)
            ? abs
            : new Uri(baseUri, location);

    private async Task<string> GetStringAsync(Uri uri, CancellationToken ct)
    {
        var response = await _transport.SendAsync(new KonnektorHttpRequest("GET", uri), ct);
        if (!response.IsSuccess)
            throw new KonnektorHttpException(response.StatusCode, response.Reason);
        return response.BodyText;
    }

    private async Task<string> PostSoapAsync(Uri url, string ns, string operation, string envelope, CancellationToken ct)
    {
        var headers = new Dictionary<string, string> { ["SOAPAction"] = $"\"{KonnektorSoap.SoapAction(ns, operation)}\"" };
        var response = await _transport.SendAsync(
            new KonnektorHttpRequest("POST", url, headers, Encoding.UTF8.GetBytes(envelope), "text/xml; charset=utf-8"), ct);
        var body = response.BodyText;
        // SOAP faults usually come with HTTP 500 — parse them for a readable message.
        if (!response.IsSuccess && !body.Contains("Fault", StringComparison.Ordinal))
            throw new KonnektorHttpException(response.StatusCode, response.Reason);
        return body;
    }

    private static string Describe(Exception ex) => ex switch
    {
        KonnektorSoapFaultException f => L("SOAP-Fehler: ", "SOAP fault: ") + f.Message,
        KonnektorTlsException t => t.Message,
        KonnektorCredentialException c => c.Message,
        KonnektorHttpException h => h.Message,
        TimeoutException or TaskCanceledException => L("Zeitüberschreitung bei der Verbindung zum Konnektor.", "Connection to the connector timed out."),
        System.Net.Sockets.SocketException s => L("Konnektor nicht erreichbar: ", "Connector unreachable: ") + s.Message,
        System.IO.IOException io => L("Verbindungsfehler: ", "Connection error: ") + io.Message,
        System.Xml.XmlException x => L("Ungültige XML-Antwort: ", "Invalid XML response: ") + x.Message,
        _ => ex.Message
    };

    internal static string L(string de, string en) => KonnektorText.L(de, en);

    public void Dispose()
    {
        (_transport as IDisposable)?.Dispose();
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
