using Microsoft.Extensions.Logging;
using StockMonitor.Models;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication.Common;

namespace StockMonitor.Communication;

/// <summary>
/// HTTP request for stock quotes.
/// </summary>
public record StockQuoteRequest(IEnumerable<string> StockCodes);

/// <summary>
/// HTTP-based communication implementation for TWSE stock API.
/// Inherits from RequestResponseCommunicationBase following SDK architecture pattern.
///
/// Architecture: TwseStockMonitorDevice -> StockMonitorDevice -> RequestResponseDeviceBase
///               -> TwseStockParser -> HttpCommunication (Request-Response pattern)
/// </summary>
public class HttpCommunication : RequestResponseCommunicationBase<StockQuoteRequest, TwseStockResponse?>
{
    private readonly TwseStockClient _stockClient;

    /// <summary>
    /// Gets the underlying TWSE stock client for direct access if needed.
    /// </summary>
    public TwseStockClient StockClient => _stockClient;

    public HttpCommunication(
        TwseStockClient stockClient,
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings, logger)
    {
        _stockClient = stockClient ?? throw new ArgumentNullException(nameof(stockClient));
    }

    /// <summary>
    /// HTTP is stateless - "connect" just marks us as ready to make requests.
    /// </summary>
    protected override Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("HTTP communication connected (stateless)");
        State = CommunicationState.Connected;
        return Task.FromResult(true);
    }

    /// <summary>
    /// HTTP is stateless - "disconnect" just marks us as not ready.
    /// </summary>
    public override Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("HTTP communication disconnected");
        State = CommunicationState.Disconnected;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Implements the Request-Response pattern for stock quote requests.
    /// </summary>
    /// <param name="request">Stock quote request containing stock codes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TWSE stock response or null if failed</returns>
    public override async Task<TwseStockResponse?> RequestAsync(
        StockQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _stockClient.GetStockQuotesAsync(request.StockCodes, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for stock codes: {StockCodes}",
                string.Join(", ", request.StockCodes));
            State = CommunicationState.Error;
            throw;
        }
    }
}
