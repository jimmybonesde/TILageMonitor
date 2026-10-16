using System.Text.Json.Serialization;

namespace TILageMonitor.Konnektor;

/// <summary>Non-secret Konnektor configuration (stored in settings.json). Secrets live in <see cref="KonnektorSecretStore"/>.</summary>
public sealed class KonnektorSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("host")]
    public string Host { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 443;

    [JsonPropertyName("mandantId")]
    public string MandantId { get; set; } = "";

    [JsonPropertyName("clientSystemId")]
    public string ClientSystemId { get; set; } = "";

    [JsonPropertyName("workplaceId")]
    public string WorkplaceId { get; set; } = "";

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    /// <summary>SHA-256 fingerprint of the trusted connector TLS certificate (AA:BB:…).</summary>
    [JsonPropertyName("pinnedFingerprint")]
    public string? PinnedFingerprint { get; set; }

    [JsonPropertyName("refreshMinutes")]
    public int RefreshMinutes { get; set; } = 5;

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(MandantId) &&
        !string.IsNullOrWhiteSpace(ClientSystemId) &&
        !string.IsNullOrWhiteSpace(WorkplaceId);

    [JsonIgnore]
    public KonnektorContext Context => new(MandantId.Trim(), ClientSystemId.Trim(), WorkplaceId.Trim(),
        string.IsNullOrWhiteSpace(UserId) ? null : UserId.Trim());

    /// <summary>Base URL https://host[:port]; IPv6 literals are bracketed.</summary>
    public static Uri BuildBaseUri(string host, int port)
    {
        var h = (host ?? "").Trim();
        if (h.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            h = h["https://".Length..];
        h = h.TrimEnd('/');
        if (h.Contains(':') && !h.StartsWith('[') && System.Net.IPAddress.TryParse(h, out _))
            h = $"[{h}]";
        var builder = new UriBuilder(Uri.UriSchemeHttps, h, port is > 0 and <= 65535 ? port : 443);
        return builder.Uri;
    }

    public static int ClampRefreshMinutes(int minutes) => Math.Clamp(minutes, 1, 240);
}
