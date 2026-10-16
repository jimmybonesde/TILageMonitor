namespace TILageMonitor.Konnektor;

/// <summary>Product block of connector.sds (ProductInformation v1.1).</summary>
public sealed class KonnektorProductInfo
{
    public string? VendorId { get; set; }
    public string? VendorName { get; set; }
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductType { get; set; }
    public string? ProductTypeVersion { get; set; }
    public string? HardwareVersion { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? InformationDate { get; set; }
}

public sealed class KonnektorServiceVersion
{
    public string Version { get; set; } = "";
    public string? TargetNamespace { get; set; }
    public string? EndpointTls { get; set; }
    public string? Endpoint { get; set; }

    /// <summary>Preferred endpoint: TLS first, plain as fallback.</summary>
    public string? PreferredEndpoint => !string.IsNullOrWhiteSpace(EndpointTls) ? EndpointTls : Endpoint;
}

public sealed class KonnektorService
{
    public string Name { get; set; } = "";
    public List<KonnektorServiceVersion> Versions { get; } = new();
}

public sealed class KonnektorSds
{
    public KonnektorProductInfo Product { get; } = new();
    public List<KonnektorService> Services { get; } = new();
    public bool? TlsMandatory { get; set; }
    public bool? ClientAuthMandatory { get; set; }

    public KonnektorService? FindService(string name) =>
        Services.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}

public sealed class KonnektorErrorState
{
    public string Condition { get; set; } = "";
    public string? Severity { get; set; }
    public string? Type { get; set; }
    public bool Active { get; set; }
    public string? ValidFrom { get; set; }
}

public sealed class KonnektorResourceInfo
{
    public bool? VpnTiOnline { get; set; }
    public bool? VpnSisOnline { get; set; }
    public string? VpnTiTimestamp { get; set; }
    public string? VpnSisTimestamp { get; set; }
    public List<KonnektorErrorState> Errors { get; } = new();
    public IEnumerable<KonnektorErrorState> ActiveErrors => Errors.Where(e => e.Active);
}

public sealed class KonnektorCard
{
    public string? CardHandle { get; set; }
    public string CardType { get; set; } = "";
    public string? Iccsn { get; set; }
    public string? CtId { get; set; }
    public string? SlotId { get; set; }
    public string? CardHolderName { get; set; }
    public string? Kvnr { get; set; }
    public DateTime? InsertTime { get; set; }
    public DateTime? CertificateExpirationDate { get; set; }
    public string? PinType { get; set; }
    public string? PinStatus { get; set; }
    public int? PinLeftTries { get; set; }
    public string? PinError { get; set; }
}

public sealed class KonnektorTerminal
{
    public string CtId { get; set; } = "";
    public string? Name { get; set; }
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public int? Slots { get; set; }
    public bool? Connected { get; set; }
    public bool? IsPhysical { get; set; }
}

/// <summary>Overall local state used by the main window ampel and toasts.</summary>
public enum KonnektorHealth
{
    NotConfigured,
    Ok,
    Warning,
    Down
}

public sealed class KonnektorSnapshot
{
    public DateTime CheckedAt { get; set; } = DateTime.Now;
    public bool Reachable { get; set; }
    public string? Error { get; set; }
    public bool TrustRequired { get; set; }
    public bool PinMismatch { get; set; }
    public KonnektorServerCertificate? ServerCertificate { get; set; }
    public KonnektorSds? Sds { get; set; }
    public KonnektorResourceInfo? Resource { get; set; }
    public string? ResourceError { get; set; }
    public List<KonnektorCard> Cards { get; } = new();
    public string? CardsError { get; set; }
    public List<KonnektorTerminal> Terminals { get; } = new();
    public string? TerminalsError { get; set; }
    public List<string> Warnings { get; } = new();

    public KonnektorHealth Health
    {
        get
        {
            if (!Reachable || Resource?.VpnTiOnline == false)
                return KonnektorHealth.Down;
            if (Warnings.Count > 0 || Resource?.VpnSisOnline == false ||
                Resource?.ActiveErrors.Any() == true ||
                ResourceError is not null || CardsError is not null || TerminalsError is not null)
                return KonnektorHealth.Warning;
            return KonnektorHealth.Ok;
        }
    }
}

/// <summary>Details of the connector TLS server certificate shown in the trust dialog.</summary>
public sealed record KonnektorServerCertificate(
    string Sha256Fingerprint,
    string Subject,
    string Issuer,
    DateTime NotBefore,
    DateTime NotAfter);
