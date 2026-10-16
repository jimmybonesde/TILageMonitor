using System.Xml.Linq;

namespace TILageMonitor.Konnektor;

/// <summary>
/// Parses connector.sds (ServiceDirectory v3.x). Namespace-agnostic (LocalName) so
/// vendor/version namespace differences (RISE, secunet, KoCoBox) do not break parsing.
/// </summary>
public static class SdsParser
{
    public static KonnektorSds Parse(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.None);
        var root = doc.Root ?? throw new FormatException("connector.sds ist leer.");
        var sds = new KonnektorSds();

        var product = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "ProductInformation");
        if (product is not null)
        {
            var p = sds.Product;
            p.InformationDate = Text(product, "InformationDate");
            p.ProductType = Text(product, "ProductType");
            p.ProductTypeVersion = Text(product, "ProductTypeVersion");
            p.VendorId = Text(product, "ProductVendorID");
            p.ProductCode = Text(product, "ProductCode");
            p.HardwareVersion = Text(product, "HWVersion");
            p.FirmwareVersion = Text(product, "FWVersion");
            p.VendorName = Text(product, "ProductVendorName");
            p.ProductName = Text(product, "ProductName");
        }

        sds.TlsMandatory = Bool(Text(root, "TLSMandatory"));
        sds.ClientAuthMandatory = Bool(Text(root, "ClientAutMandatory"));

        foreach (var service in root.Descendants().Where(e => e.Name.LocalName == "Service"))
        {
            var name = (string?)service.Attribute("Name");
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var entry = new KonnektorService { Name = name.Trim() };
            foreach (var version in service.Descendants().Where(e => e.Name.LocalName == "Version"))
            {
                var v = new KonnektorServiceVersion
                {
                    Version = ((string?)version.Attribute("Version"))?.Trim() ?? "",
                    TargetNamespace = ((string?)version.Attribute("TargetNamespace"))?.Trim(),
                    EndpointTls = LocationOf(version, "EndpointTLS"),
                    Endpoint = LocationOf(version, "Endpoint")
                };
                entry.Versions.Add(v);
            }

            sds.Services.Add(entry);
        }

        return sds;
    }

    /// <summary>
    /// Picks the version to talk to: the highest version with the preferred major (e.g. 7 for
    /// EventService) and an endpoint; otherwise the highest version with an endpoint.
    /// </summary>
    public static KonnektorServiceVersion? SelectVersion(KonnektorService? service, int preferredMajor)
    {
        if (service is null)
            return null;

        var usable = service.Versions
            .Where(v => !string.IsNullOrWhiteSpace(v.PreferredEndpoint))
            .Select(v => (v, parsed: ParseVersion(v.Version)))
            .OrderByDescending(t => t.parsed)
            .ToList();

        return usable.FirstOrDefault(t => t.parsed.Major == preferredMajor).v
               ?? usable.FirstOrDefault().v;
    }

    private static Version ParseVersion(string value) =>
        Version.TryParse(value, out var v) ? v : new Version(0, 0);

    private static string? LocationOf(XElement version, string localName) =>
        ((string?)version.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Attribute("Location"))?.Trim();

    internal static string? Text(XElement scope, string localName)
    {
        var value = scope.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static bool? Bool(string? value) =>
        value is null ? null : string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
}
