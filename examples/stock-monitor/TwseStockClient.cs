using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace StockMonitor;

/// <summary>
/// Client for Taiwan Stock Exchange (TWSE) real-time stock quote API.
/// Responsible for HTTP communication with TWSE API.
/// </summary>
public class TwseStockClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TwseStockClient> _logger;

    private const string TwseApiBaseUrl = "https://mis.twse.com.tw/stock/api/getStockInfo.jsp";

    public TwseStockClient(HttpClient httpClient, ILogger<TwseStockClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches real-time stock quotes from TWSE API.
    /// </summary>
    /// <param name="stockCodes">Stock codes to query (e.g., "2395", "2330")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TWSE API response containing stock quotes</returns>
    public async Task<TwseStockResponse?> GetStockQuotesAsync(
        IEnumerable<string> stockCodes,
        CancellationToken cancellationToken = default)
    {
        // Build query string for stock codes
        // Format: tse_2395.tw|tse_2330.tw (for listed stocks)
        var stockQuery = string.Join("|", stockCodes.Select(code => $"tse_{code}.tw"));
        var url = $"{TwseApiBaseUrl}?ex_ch={stockQuery}&json=1&delay=0";

        _logger.LogDebug("Fetching stock data from TWSE API: {Url}", url);

        try
        {
            var response = await _httpClient.GetFromJsonAsync<TwseStockResponse>(url, cancellationToken);

            if (response?.IsSuccess != true)
            {
                _logger.LogWarning("TWSE API returned error: {Code} - {Message}",
                    response?.RtCode, response?.RtMessage);
            }

            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch stock data from TWSE API");
            throw;
        }
    }
}
