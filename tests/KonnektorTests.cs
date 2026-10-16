using System.Text;
using TILageMonitor.Konnektor;
using Xunit;

namespace TILageMonitor.Tests;

public sealed class KonnektorTests
{
    private const string SampleSds = """
<?xml version="1.0" encoding="UTF-8"?>
<ConnectorServices xmlns="http://ws.gematik.de/conn/ServiceDirectory/v3.1"
    xmlns:PI="http://ws.gematik.de/int/version/ProductInformation/v1.1"
    xmlns:SI="http://ws.gematik.de/int/version/ServiceInformation/v1.0">
  <PI:ProductInformation>
    <PI:InformationDate>2026-09-01T10:00:00Z</PI:InformationDate>
    <PI:ProductTypeInformation>
      <PI:ProductType>Konnektor</PI:ProductType>
      <PI:ProductTypeVersion>5.6.0</PI:ProductTypeVersion>
    </PI:ProductTypeInformation>
    <PI:ProductIdentification>
      <PI:ProductVendorID>SECU</PI:ProductVendorID>
      <PI:ProductCode>SNK</PI:ProductCode>
      <PI:ProductVersion>
        <PI:Local>
          <PI:HWVersion>2.0.0</PI:HWVersion>
          <PI:FWVersion>5.1.2</PI:FWVersion>
        </PI:Local>
      </PI:ProductVersion>
    </PI:ProductIdentification>
    <PI:ProductMiscellaneous>
      <PI:ProductVendorName>secunet Security Networks AG</PI:ProductVendorName>
      <PI:ProductName>secunet konnektor 2.1.0</PI:ProductName>
    </PI:ProductMiscellaneous>
  </PI:ProductInformation>
  <TLSMandatory>true</TLSMandatory>
  <ClientAutMandatory>true</ClientAutMandatory>
  <SI:ServiceInformation>
    <SI:Service Name="EventService">
      <SI:Abstract>Event</SI:Abstract>
      <SI:Versions>
        <SI:Version Version="7.2.0" TargetNamespace="http://ws.gematik.de/conn/EventService/v7.2">
          <SI:Abstract>v7.2</SI:Abstract>
          <SI:EndpointTLS Location="https://10.0.0.5:443/ws/EventService"/>
          <SI:WSDL Location="https://10.0.0.5/wsdl/EventService.wsdl"/>
        </SI:Version>
        <SI:Version Version="6.1.0" TargetNamespace="http://ws.gematik.de/conn/EventService/v6.1">
          <SI:EndpointTLS Location="https://10.0.0.5:443/ws/EventService61"/>
        </SI:Version>
      </SI:Versions>
    </SI:Service>
    <SI:Service Name="CardService">
      <SI:Versions>
        <SI:Version Version="8.1.2" TargetNamespace="http://ws.gematik.de/conn/CardService/v8.1">
          <SI:Endpoint Location="/ws/CardService"/>
        </SI:Version>
      </SI:Versions>
    </SI:Service>
  </SI:ServiceInformation>
</ConnectorServices>
""";

    private const string GetCardsResponse = """
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <EVT:GetCardsResponse xmlns:EVT="http://ws.gematik.de/conn/EventService/v7.2"
        xmlns:CONN="http://ws.gematik.de/conn/ConnectorCommon/v5.0"
        xmlns:CARD="http://ws.gematik.de/conn/CardService/v8.1"
        xmlns:CARDCMN="http://ws.gematik.de/conn/CardServiceCommon/v2.0">
      <CONN:Status><CONN:Result>OK</CONN:Result></CONN:Status>
      <CARD:Cards>
        <CARD:Card>
          <CONN:CardHandle>SMCB-1</CONN:CardHandle>
          <CARDCMN:CardType>SMC-B</CARDCMN:CardType>
          <CARD:CardVersion/>
          <CARDCMN:Iccsn>80276001011699901234</CARDCMN:Iccsn>
          <CARDCMN:CtId>CT1</CARDCMN:CtId>
          <CARDCMN:SlotId>2</CARDCMN:SlotId>
          <CARD:InsertTime>2026-10-01T07:30:00Z</CARD:InsertTime>
          <CARD:CardHolderName>Praxis Dr. Muster</CARD:CardHolderName>
          <CARD:CertificateExpirationDate>2026-11-30</CARD:CertificateExpirationDate>
        </CARD:Card>
        <CARD:Card>
          <CONN:CardHandle>HBA-1</CONN:CardHandle>
          <CARDCMN:CardType>HBA</CARDCMN:CardType>
          <CARDCMN:Iccsn>80276883110000012345</CARDCMN:Iccsn>
          <CARDCMN:CtId>CT1</CARDCMN:CtId>
          <CARDCMN:SlotId>1</CARDCMN:SlotId>
          <CARD:CardHolderName>Dr. Erika Muster</CARD:CardHolderName>
          <CARD:CertificateExpirationDate>2030-01-31</CARD:CertificateExpirationDate>
        </CARD:Card>
      </CARD:Cards>
    </EVT:GetCardsResponse>
  </soap:Body>
</soap:Envelope>
""";

    private const string ResourceInfoResponse = """
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <EVT:GetResourceInformationResponse xmlns:EVT="http://ws.gematik.de/conn/EventService/v7.2"
        xmlns:CONN="http://ws.gematik.de/conn/ConnectorCommon/v5.0"
        xmlns:CONNECTOR="http://ws.gematik.de/conn/Connector/v1.0">
      <CONN:Status><CONN:Result>OK</CONN:Result></CONN:Status>
      <CONNECTOR:Connector>
        <CONNECTOR:VPNTIStatus>
          <CONNECTOR:ConnectionStatus>Offline</CONNECTOR:ConnectionStatus>
          <CONNECTOR:Timestamp>2026-10-16T08:00:00Z</CONNECTOR:Timestamp>
        </CONNECTOR:VPNTIStatus>
        <CONNECTOR:VPNSISStatus>
          <CONNECTOR:ConnectionStatus>Online</CONNECTOR:ConnectionStatus>
          <CONNECTOR:Timestamp>2026-10-16T08:00:00Z</CONNECTOR:Timestamp>
        </CONNECTOR:VPNSISStatus>
        <CONNECTOR:OperatingState>
          <CONNECTOR:ErrorState>
            <CONNECTOR:ErrorCondition>EC_TI_Network_Not_Reachable</CONNECTOR:ErrorCondition>
            <CONNECTOR:Severity>Error</CONNECTOR:Severity>
            <CONNECTOR:Type>Operation</CONNECTOR:Type>
            <CONNECTOR:Value>true</CONNECTOR:Value>
            <CONNECTOR:ValidFrom>2026-10-16T07:59:00Z</CONNECTOR:ValidFrom>
          </CONNECTOR:ErrorState>
          <CONNECTOR:ErrorState>
            <CONNECTOR:ErrorCondition>EC_Time_Sync_Not_Successful</CONNECTOR:ErrorCondition>
            <CONNECTOR:Severity>Warning</CONNECTOR:Severity>
            <CONNECTOR:Type>Operation</CONNECTOR:Type>
            <CONNECTOR:Value>false</CONNECTOR:Value>
          </CONNECTOR:ErrorState>
        </CONNECTOR:OperatingState>
      </CONNECTOR:Connector>
    </EVT:GetResourceInformationResponse>
  </soap:Body>
</soap:Envelope>
""";

    private const string PinStatusResponse = """
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <CARD:GetPinStatusResponse xmlns:CARD="http://ws.gematik.de/conn/CardService/v8.1"
        xmlns:CONN="http://ws.gematik.de/conn/ConnectorCommon/v5.0">
      <CONN:Status><CONN:Result>OK</CONN:Result></CONN:Status>
      <CARD:PinStatus>TRANSPORT_PIN</CARD:PinStatus>
      <CARD:LeftTries>3</CARD:LeftTries>
    </CARD:GetPinStatusResponse>
  </soap:Body>
</soap:Envelope>
""";

    private const string FaultResponse = """
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <soap:Fault>
      <faultcode>soap:Server</faultcode>
      <faultstring>Ungültiger Aufrufkontext</faultstring>
      <detail>
        <GERROR:Error xmlns:GERROR="http://ws.gematik.de/tel/error/v2.0">
          <GERROR:Trace>
            <GERROR:Code>4004</GERROR:Code>
            <GERROR:ErrorText>Ungültige Mandanten-ID</GERROR:ErrorText>
          </GERROR:Trace>
        </GERROR:Error>
      </detail>
    </soap:Fault>
  </soap:Body>
</soap:Envelope>
""";

    private const string GetCardTerminalsResponse = """
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <EVT:GetCardTerminalsResponse xmlns:EVT="http://ws.gematik.de/conn/EventService/v7.2"
        xmlns:CONN="http://ws.gematik.de/conn/ConnectorCommon/v5.0"
        xmlns:CT="http://ws.gematik.de/conn/CardTerminalInfo/v8.0"
        xmlns:CARDCMN="http://ws.gematik.de/conn/CardServiceCommon/v2.0">
      <CONN:Status><CONN:Result>OK</CONN:Result></CONN:Status>
      <CT:CardTerminals>
        <CT:CardTerminal>
          <CARDCMN:CtId>CT1</CARDCMN:CtId>
          <CT:Name>Empfang ORGA 6141</CT:Name>
          <CT:IPAddress><CT:IPV4Address>192.168.1.50</CT:IPV4Address></CT:IPAddress>
          <CT:Slots>4</CT:Slots>
          <CT:IS_PHYSICAL>true</CT:IS_PHYSICAL>
          <CT:Connected>false</CT:Connected>
        </CT:CardTerminal>
      </CT:CardTerminals>
    </EVT:GetCardTerminalsResponse>
  </soap:Body>
</soap:Envelope>
""";

    [Fact]
    public void Sds_parses_product_information()
    {
        var sds = SdsParser.Parse(SampleSds);
        Assert.Equal("secunet Security Networks AG", sds.Product.VendorName);
        Assert.Equal("secunet konnektor 2.1.0", sds.Product.ProductName);
        Assert.Equal("SECU", sds.Product.VendorId);
        Assert.Equal("Konnektor", sds.Product.ProductType);
        Assert.Equal("5.6.0", sds.Product.ProductTypeVersion);
        Assert.Equal("5.1.2", sds.Product.FirmwareVersion);
        Assert.Equal("2.0.0", sds.Product.HardwareVersion);
        Assert.True(sds.TlsMandatory);
        Assert.True(sds.ClientAuthMandatory);
    }

    [Fact]
    public void Sds_parses_services_and_selects_preferred_versions()
    {
        var sds = SdsParser.Parse(SampleSds);
        Assert.Equal(2, sds.Services.Count);

        var evt = SdsParser.SelectVersion(sds.FindService("EventService"), 7)!;
        Assert.Equal("7.2.0", evt.Version);
        Assert.Equal("http://ws.gematik.de/conn/EventService/v7.2", evt.TargetNamespace);
        Assert.Equal("https://10.0.0.5:443/ws/EventService", evt.PreferredEndpoint);

        var card = SdsParser.SelectVersion(sds.FindService("CardService"), 8)!;
        Assert.Equal("/ws/CardService", card.PreferredEndpoint);
        Assert.Equal(new Uri("https://10.0.0.5/ws/CardService"),
            KonnektorClient.ResolveEndpoint(new Uri("https://10.0.0.5/"), card.PreferredEndpoint!));

        // Fallback to highest available when the preferred major is missing.
        Assert.Equal("7.2.0", SdsParser.SelectVersion(sds.FindService("EventService"), 9)!.Version);
    }

    [Fact]
    public void GetCards_response_is_parsed()
    {
        var cards = KonnektorSoap.ParseGetCards(GetCardsResponse);
        Assert.Equal(2, cards.Count);
        var smcb = cards[0];
        Assert.Equal("SMC-B", smcb.CardType);
        Assert.Equal("SMCB-1", smcb.CardHandle);
        Assert.Equal("80276001011699901234", smcb.Iccsn);
        Assert.Equal("CT1", smcb.CtId);
        Assert.Equal("2", smcb.SlotId);
        Assert.Equal("Praxis Dr. Muster", smcb.CardHolderName);
        Assert.Equal(new DateTime(2026, 11, 30), smcb.CertificateExpirationDate);
        Assert.NotNull(smcb.InsertTime);
        Assert.Equal("HBA", cards[1].CardType);
    }

    [Fact]
    public void GetResourceInformation_response_is_parsed()
    {
        var info = KonnektorSoap.ParseGetResourceInformation(ResourceInfoResponse);
        Assert.False(info.VpnTiOnline);
        Assert.True(info.VpnSisOnline);
        Assert.Equal(2, info.Errors.Count);
        var active = Assert.Single(info.ActiveErrors);
        Assert.Equal("EC_TI_Network_Not_Reachable", active.Condition);
        Assert.Equal("Error", active.Severity);
    }

    [Fact]
    public void GetPinStatus_response_is_parsed()
    {
        var (status, tries) = KonnektorSoap.ParseGetPinStatus(PinStatusResponse);
        Assert.Equal("TRANSPORT_PIN", status);
        Assert.Equal(3, tries);
    }

    [Fact]
    public void GetCardTerminals_response_is_parsed()
    {
        var terminal = Assert.Single(KonnektorSoap.ParseGetCardTerminals(GetCardTerminalsResponse));
        Assert.Equal("CT1", terminal.CtId);
        Assert.Equal("Empfang ORGA 6141", terminal.Name);
        Assert.Equal("192.168.1.50", terminal.IpAddress);
        Assert.Equal(4, terminal.Slots);
        Assert.False(terminal.Connected);
        Assert.True(terminal.IsPhysical);
    }

    [Fact]
    public void Soap_fault_text_is_surfaced()
    {
        var ex = Assert.Throws<KonnektorSoapFaultException>(() => KonnektorSoap.ParseGetCards(FaultResponse));
        Assert.Contains("Ungültiger Aufrufkontext", ex.Message);
        Assert.Contains("4004: Ungültige Mandanten-ID", ex.Message);
    }

    [Fact]
    public void Requests_contain_context_escaped_and_sds_namespace()
    {
        var ctx = new KonnektorContext("Mandant<1>", "CS&1", "WP1", "U1");
        var xml = KonnektorSoap.GetCardsRequest("http://ws.gematik.de/conn/EventService/v7.3", ctx);
        Assert.Contains("xmlns:OP=\"http://ws.gematik.de/conn/EventService/v7.3\"", xml);
        Assert.Contains("<CONN:MandantId>Mandant&lt;1&gt;</CONN:MandantId>", xml);
        Assert.Contains("<CONN:ClientSystemId>CS&amp;1</CONN:ClientSystemId>", xml);
        Assert.Contains("<CONN:UserId>U1</CONN:UserId>", xml);
        System.Xml.Linq.XDocument.Parse(xml); // well-formed

        var pin = KonnektorSoap.GetPinStatusRequest(KonnektorSoap.DefaultCardServiceNs, ctx, "H1", "PIN.SMC");
        Assert.Contains("<CARDCMN:PinTyp>PIN.SMC</CARDCMN:PinTyp>", pin);
        Assert.Equal("http://ws.gematik.de/conn/CardService/v8.1#GetPinStatus",
            KonnektorSoap.SoapAction(KonnektorSoap.DefaultCardServiceNs, "GetPinStatus"));
    }

    [Theory]
    [InlineData("HBA", "PIN.CH")]
    [InlineData("SMC-B", "PIN.SMC")]
    [InlineData("EGK", null)]
    public void Pin_type_per_card(string cardType, string? expected) =>
        Assert.Equal(expected, KonnektorSoap.PinTypeFor(cardType));

    [Fact]
    public void Fingerprint_pinning()
    {
        var der = Encoding.ASCII.GetBytes("fake-der-certificate");
        var fp = CertificatePinning.Fingerprint(der);
        Assert.Equal(95, fp.Length); // 32 bytes as AA:BB:…

        Assert.Equal(PinEvaluation.NotPinned, CertificatePinning.Evaluate(fp, null));
        Assert.Equal(PinEvaluation.NotPinned, CertificatePinning.Evaluate(fp, "  "));
        Assert.Equal(PinEvaluation.Match, CertificatePinning.Evaluate(fp, fp));
        Assert.Equal(PinEvaluation.Match, CertificatePinning.Evaluate(fp, fp.Replace(":", "").ToLowerInvariant()));

        var other = CertificatePinning.Fingerprint(Encoding.ASCII.GetBytes("other"));
        Assert.Equal(PinEvaluation.Mismatch, CertificatePinning.Evaluate(other, fp));
        Assert.Equal(PinEvaluation.Mismatch, CertificatePinning.Evaluate(null, fp));
        Assert.Equal(PinEvaluation.Mismatch, CertificatePinning.Evaluate(fp, "ABCD"));
    }

    private sealed class FakeProtector : ISecretProtector
    {
        public int ProtectCalls;
        public byte[] Protect(byte[] plaintext) { ProtectCalls++; return plaintext.Select(b => (byte)(b ^ 0x5A)).Reverse().ToArray(); }
        public byte[] Unprotect(byte[] data) => data.Reverse().Select(b => (byte)(b ^ 0x5A)).ToArray();
    }

    [Fact]
    public void Secret_store_never_writes_plaintext_and_roundtrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tilage-k-{Guid.NewGuid():N}");
        try
        {
            var protector = new FakeProtector();
            var store = new KonnektorSecretStore(protector, dir);
            var p12 = Encoding.ASCII.GetBytes("PKCS12-SECRET-BYTES");
            store.SaveClientCertificate(p12, "Geheim123!");

            Assert.Equal(2, protector.ProtectCalls);
            foreach (var file in Directory.GetFiles(dir))
            {
                var text = Encoding.ASCII.GetString(File.ReadAllBytes(file));
                Assert.DoesNotContain("PKCS12-SECRET", text);
                Assert.DoesNotContain("Geheim123", text);
            }

            var loaded = store.LoadClientCertificate();
            Assert.NotNull(loaded);
            Assert.Equal(p12, loaded!.Value.P12);
            Assert.Equal("Geheim123!", loaded.Value.Password);

            store.Delete();
            Assert.False(store.HasClientCertificate);
            Assert.Null(store.LoadClientCertificate());
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Health_and_warnings()
    {
        var now = new DateTime(2026, 10, 16);
        var snapshot = new KonnektorSnapshot { Reachable = true, Resource = KonnektorSoap.ParseGetResourceInformation(ResourceInfoResponse) };
        snapshot.Cards.AddRange(KonnektorSoap.ParseGetCards(GetCardsResponse));
        snapshot.Cards[0].PinStatus = "VERIFIABLE"; // SMC-B not unlocked
        snapshot.Cards[1].PinStatus = "VERIFIABLE"; // HBA normal
        KonnektorAnalysis.AddWarnings(snapshot, clientCertNotAfter: now.AddDays(400), now);

        Assert.Equal(KonnektorHealth.Down, snapshot.Health); // VPN TI offline
        Assert.Equal(2, snapshot.Warnings.Count);            // SMC-B expiry (<90d) + SMC-B PIN
        Assert.True(KonnektorAnalysis.IsPinProblem("BLOCKED", "HBA"));
        Assert.False(KonnektorAnalysis.IsPinProblem("VERIFIABLE", "HBA"));
        Assert.Equal(KonnektorHealth.Down, new KonnektorSnapshot { Reachable = false }.Health);
        Assert.Equal(KonnektorHealth.Ok, new KonnektorSnapshot { Reachable = true }.Health);
    }

    [Theory]
    [InlineData("10.0.0.5", 443, "https://10.0.0.5/")]
    [InlineData("https://konnektor.local/", 8443, "https://konnektor.local:8443/")]
    [InlineData("fe80::1", 443, "https://[fe80::1]/")]
    public void Base_uri_is_built(string host, int port, string expected) =>
        Assert.Equal(new Uri(expected), KonnektorSettings.BuildBaseUri(host, port));
}
