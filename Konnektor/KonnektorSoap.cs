using System.Globalization;
using System.Security;
using System.Xml.Linq;

namespace TILageMonitor.Konnektor;

public sealed record KonnektorContext(string MandantId, string ClientSystemId, string WorkplaceId, string? UserId = null);

public sealed class KonnektorSoapFaultException : Exception
{
    public KonnektorSoapFaultException(string message) : base(message) { }
}

/// <summary>
/// Hand-written SOAP 1.1 envelopes and namespace-agnostic response parsers for the
/// gematik Konnektor EventService (v7.x) and CardService (v8.x). Operation namespaces
/// come from the connector.sds TargetNamespace so v7.2 / v7.3 etc. work unchanged.
/// </summary>
public static class KonnektorSoap
{
    public const string ConnectorContextNs = "http://ws.gematik.de/conn/ConnectorContext/v2.0";
    public const string ConnectorCommonNs = "http://ws.gematik.de/conn/ConnectorCommon/v5.0";
    public const string CardServiceCommonNs = "http://ws.gematik.de/conn/CardServiceCommon/v2.0";
    public const string DefaultEventServiceNs = "http://ws.gematik.de/conn/EventService/v7.2";
    public const string DefaultCardServiceNs = "http://ws.gematik.de/conn/CardService/v8.1";

    // ------------------------------------------------------------------ requests

    public static string ContextXml(KonnektorContext ctx)
    {
        var user = string.IsNullOrWhiteSpace(ctx.UserId)
            ? ""
            : $"<CONN:UserId>{Esc(ctx.UserId!)}</CONN:UserId>";
        return "<CCTX:Context>" +
               $"<CONN:MandantId>{Esc(ctx.MandantId)}</CONN:MandantId>" +
               $"<CONN:ClientSystemId>{Esc(ctx.ClientSystemId)}</CONN:ClientSystemId>" +
               $"<CONN:WorkplaceId>{Esc(ctx.WorkplaceId)}</CONN:WorkplaceId>" +
               user +
               "</CCTX:Context>";
    }

    private static string Envelope(string operationNs, string body) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"" +
        $" xmlns:OP=\"{Esc(operationNs)}\"" +
        $" xmlns:CCTX=\"{ConnectorContextNs}\"" +
        $" xmlns:CONN=\"{ConnectorCommonNs}\"" +
        $" xmlns:CARDCMN=\"{CardServiceCommonNs}\">" +
        "<soapenv:Header/>" +
        $"<soapenv:Body>{body}</soapenv:Body>" +
        "</soapenv:Envelope>";

    public static string GetCardsRequest(string eventNs, KonnektorContext ctx) =>
        Envelope(eventNs, $"<OP:GetCards mandant-wide=\"false\">{ContextXml(ctx)}</OP:GetCards>");

    public static string GetCardTerminalsRequest(string eventNs, KonnektorContext ctx) =>
        Envelope(eventNs, $"<OP:GetCardTerminals mandant-wide=\"false\">{ContextXml(ctx)}</OP:GetCardTerminals>");

    public static string GetResourceInformationRequest(string eventNs, KonnektorContext ctx) =>
        Envelope(eventNs, $"<OP:GetResourceInformation>{ContextXml(ctx)}</OP:GetResourceInformation>");

    public static string GetPinStatusRequest(string cardNs, KonnektorContext ctx, string cardHandle, string pinType) =>
        Envelope(cardNs,
            "<OP:GetPinStatus>" + ContextXml(ctx) +
            $"<CONN:CardHandle>{Esc(cardHandle)}</CONN:CardHandle>" +
            $"<CARDCMN:PinTyp>{Esc(pinType)}</CARDCMN:PinTyp>" +
            "</OP:GetPinStatus>");

    /// <summary>SOAPAction header value: "&lt;namespace&gt;#&lt;Operation&gt;".</summary>
    public static string SoapAction(string ns, string operation) => $"{ns}#{operation}";

    /// <summary>PIN to query per card type (HBA → PIN.CH, SMC-B → PIN.SMC); null = no PIN query.</summary>
    public static string? PinTypeFor(string cardType) => cardType.Trim().ToUpperInvariant() switch
    {
        "HBA" => "PIN.CH",
        "SMC-B" => "PIN.SMC",
        _ => null
    };

    // ------------------------------------------------------------------ responses

    /// <summary>Parses XML, throws <see cref="KonnektorSoapFaultException"/> on a SOAP fault or Status Error.</summary>
    public static XElement ParseBody(string xml)
    {
        var doc = XDocument.Parse(xml);
        var body = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "Body")
                   ?? throw new FormatException("SOAP-Body fehlt.");

        var fault = body.Elements().FirstOrDefault(e => e.Name.LocalName == "Fault");
        if (fault is not null)
            throw new KonnektorSoapFaultException(FaultText(fault));

        var response = body.Elements().FirstOrDefault() ?? throw new FormatException("SOAP-Antwort ist leer.");
        var status = response.Elements().FirstOrDefault(e => e.Name.LocalName == "Status");
        var result = status is null ? null : SdsParser.Text(status, "Result");
        if (string.Equals(result, "Error", StringComparison.OrdinalIgnoreCase))
            throw new KonnektorSoapFaultException(FaultText(status!));
        return response;
    }

    public static string FaultText(XElement fault)
    {
        var parts = new List<string>();
        void Add(string? s)
        {
            if (!string.IsNullOrWhiteSpace(s) && !parts.Contains(s.Trim()))
                parts.Add(s.Trim());
        }

        Add(Child(fault, "faultstring") ?? SdsParser.Text(fault, "Text"));
        foreach (var error in fault.Descendants().Where(e => e.Name.LocalName == "Trace"))
        {
            var code = SdsParser.Text(error, "Code");
            var text = SdsParser.Text(error, "ErrorText");
            Add(code is null ? text : $"{code}: {text}");
            Add(SdsParser.Text(error, "Detail"));
        }

        return parts.Count == 0 ? "Unbekannter SOAP-Fehler." : string.Join(" – ", parts);
    }

    public static List<KonnektorCard> ParseGetCards(string xml)
    {
        var response = ParseBody(xml);
        return response.Descendants()
            .Where(e => e.Name.LocalName == "Card")
            .Select(card => new KonnektorCard
            {
                CardHandle = Child(card, "CardHandle"),
                CardType = Child(card, "CardType") ?? "",
                Iccsn = Child(card, "Iccsn"),
                CtId = Child(card, "CtId"),
                SlotId = Child(card, "SlotId"),
                CardHolderName = Child(card, "CardHolderName"),
                Kvnr = Child(card, "Kvnr"),
                InsertTime = Date(Child(card, "InsertTime")),
                CertificateExpirationDate = Date(Child(card, "CertificateExpirationDate"))
            })
            .ToList();
    }

    public static List<KonnektorTerminal> ParseGetCardTerminals(string xml)
    {
        var response = ParseBody(xml);
        return response.Descendants()
            .Where(e => e.Name.LocalName == "CardTerminal")
            .Select(ct => new KonnektorTerminal
            {
                CtId = Child(ct, "CtId") ?? "",
                Name = Child(ct, "Name"),
                IpAddress = SdsParser.Text(ct, "IPV4Address") ?? SdsParser.Text(ct, "IPV6Address") ?? SdsParser.Text(ct, "IPAddress"),
                MacAddress = Child(ct, "MacAddress"),
                Slots = int.TryParse(Child(ct, "Slots"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : null,
                Connected = SdsParser.Bool(Child(ct, "Connected")),
                IsPhysical = SdsParser.Bool(Child(ct, "IS_PHYSICAL"))
            })
            .ToList();
    }

    public static KonnektorResourceInfo ParseGetResourceInformation(string xml)
    {
        var response = ParseBody(xml);
        var info = new KonnektorResourceInfo();
        var connector = response.Descendants().FirstOrDefault(e => e.Name.LocalName == "Connector");
        var scope = connector ?? response;

        var ti = scope.Descendants().FirstOrDefault(e => e.Name.LocalName == "VPNTIStatus");
        if (ti is not null)
        {
            info.VpnTiOnline = Online(SdsParser.Text(ti, "ConnectionStatus"));
            info.VpnTiTimestamp = SdsParser.Text(ti, "Timestamp");
        }

        var sis = scope.Descendants().FirstOrDefault(e => e.Name.LocalName == "VPNSISStatus");
        if (sis is not null)
        {
            info.VpnSisOnline = Online(SdsParser.Text(sis, "ConnectionStatus"));
            info.VpnSisTimestamp = SdsParser.Text(sis, "Timestamp");
        }

        foreach (var es in scope.Descendants().Where(e => e.Name.LocalName == "ErrorState"))
        {
            info.Errors.Add(new KonnektorErrorState
            {
                Condition = Child(es, "ErrorCondition") ?? "",
                Severity = Child(es, "Severity"),
                Type = Child(es, "Type"),
                Active = SdsParser.Bool(Child(es, "Value")) ?? true,
                ValidFrom = Child(es, "ValidFrom")
            });
        }

        return info;
    }

    public static (string PinStatus, int? LeftTries) ParseGetPinStatus(string xml)
    {
        var response = ParseBody(xml);
        var status = Child(response, "PinStatus") ?? SdsParser.Text(response, "PinStatus") ?? "UNKNOWN";
        var tries = SdsParser.Text(response, "LeftTries");
        return (status, int.TryParse(tries, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) ? t : null);
    }

    private static bool? Online(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "online" => true,
        "offline" => false,
        _ => null
    };

    private static string? Child(XElement scope, string localName)
    {
        var value = scope.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTime? Date(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            return value.Length <= 10 ? dto.Date : dto.LocalDateTime;
        return null;
    }

    private static string Esc(string value) => SecurityElement.Escape(value) ?? "";
}
