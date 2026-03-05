using System.Net.Http.Json;
using AirQualityMonitor.Models;
using Microsoft.Extensions.Logging;

namespace AirQualityMonitor.Communication;

public class AirQualityClient(HttpClient httpClient, ILogger<AirQualityClient> logger, string apiKey)
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly ILogger<AirQualityClient> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _apiKey = apiKey;
    private const string BaseUrl = "https://data.moenv.gov.tw/api/v2/aqx_p_136";

    public async Task<List<AirQualityRecord>?> GetAirQualityAsync(int offset = 0, int limit = 10, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}?language=zh&offset={offset}&limit={limit}&api_key={_apiKey}";

        _logger.LogDebug("Fetching air quality data from MOENV API");

        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<AirQualityRecord>>(url, cancellationToken);
            _logger.LogDebug("Received {Count} records", response?.Count ?? 0);
            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch air quality data");
            throw;
        }
    }
}