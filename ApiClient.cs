using System.Net.Http;
using System.Text.Json;

namespace TILageMonitor;

public sealed class ApiClient
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string V2 = "https://ti-lage.prod.ccs.gematik.solutions/lageapi/v2/tilage";
    private const string Incidents = "https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/incident";
    private const string Outages = "https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/outage";

    public async Task<LageV2> GetLageAsync(CancellationToken ct)
        => await GetAsync<LageV2>(V2, ct);

    public async Task<IncidentResponse> GetIncidentsAsync(CancellationToken ct)
        => await GetAsync<IncidentResponse>(Incidents, ct);

    public async Task<OutageResponse> GetOutagesAsync(CancellationToken ct)
        => await GetAsync<OutageResponse>(Outages, ct);

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("TILageMonitor/1.0");
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException("Leere oder ungültige API-Antwort.");
    }
}
