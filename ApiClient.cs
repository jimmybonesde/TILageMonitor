using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace TILageMonitor;

public sealed class ApiClient
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3)
    ];

    private readonly HttpClient _http;

    public ApiClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string V2 = "https://ti-lage.prod.ccs.gematik.solutions/lageapi/v2/tilage";
    private const string Incidents = "https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/incident";
    private const string Outages = "https://ti-lage.prod.ccs.gematik.solutions/lageapi/v1/tistatus/outage";

    public Task<LageV2> GetLageAsync(CancellationToken ct) => GetAsync<LageV2>(V2, ct);
    public Task<IncidentResponse> GetIncidentsAsync(CancellationToken ct) => GetAsync<IncidentResponse>(Incidents, ct);
    public Task<OutageResponse> GetOutagesAsync(CancellationToken ct) => GetAsync<OutageResponse>(Outages, ct);

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("TILageMonitor/1.0");
                using var response = await _http.SendAsync(request, ct);

                if (IsTransient(response.StatusCode) && attempt < RetryDelays.Length)
                {
                    await Task.Delay(RetryDelays[attempt], ct);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<T>(json, JsonOptions)
                    ?? throw new InvalidOperationException("Leere oder ungültige API-Antwort.");
            }
            catch (HttpRequestException) when (attempt < RetryDelays.Length)
            {
                await Task.Delay(RetryDelays[attempt], ct);
            }
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;
}
