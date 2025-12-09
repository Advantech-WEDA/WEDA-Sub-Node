using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace StockMonitor;

/// <summary>
/// Parser for Taiwan Stock Exchange (TWSE) stock quote API.
/// Implements IRequestResponseProtocolParser pattern:
/// - Owns HttpCommunication (which contains TwseStockClient)
/// - Fetches data via Communication layer
/// - Parses API response to TelemetryMeasure
/// </summary>
public class TwseStockParser : IRequestResponseProtocolParser
{
    private readonly HttpCommunication _communication;
    private readonly ILogger<TwseStockParser> _logger;
    private readonly List<string> _stockCodes;

    public ICommunication Communication => _communication;
    public string ProtocolName => "TWSE Stock API";
    public IReadOnlyList<string> SupportedDataTypes => ["StockPrice", "StockVolume"];
    public bool SupportsBidirectional => false; // Read-only API

    public TwseStockParser(
        HttpCommunication communication,
        List<string> stockCodes,
        ILogger<TwseStockParser> logger)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _stockCodes = stockCodes ?? throw new ArgumentNullException(nameof(stockCodes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reads stock data via HttpCommunication and parses to TelemetryMeasure list.
    /// Implements IRequestResponseProtocolParser.ReadTelemetryAsync interface.
    /// Note: This method requires sensor mapping to be provided separately.
    /// Use ReadSensorDataAsync(sensorMapping) instead, or ensure sensors are configured.
    /// </summary>
    public Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        // Build sensor mapping from stock codes
        // Each stock code maps to itself as ResourceId (stock code becomes ResourceId)
        var sensorMapping = new SensorMapping();
        foreach (var stockCode in _stockCodes)
        {
            sensorMapping.FieldToResourceId[stockCode] = stockCode;
            sensorMapping.FieldToSensorType[stockCode] = SensorType.Other;
        }

        return ReadSensorDataAsync(sensorMapping, cancellationToken);
    }

    /// <summary>
    /// Internal method that reads stock data via HttpCommunication and parses to TelemetryMeasure list.
    /// </summary>
    private async Task<List<TelemetryMeasure>> ReadSensorDataAsync(
        SensorMapping sensorMapping,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Fetch via HttpCommunication -> TwseStockClient
            var response = await _communication.GetStockQuotesAsync(_stockCodes, cancellationToken);

            // Parse response to TelemetryMeasure
            return ParseResponse(response, sensorMapping);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch stock data from TWSE API");
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading stock data");
            return [];
        }
    }

    /// <summary>
    /// Parses TWSE API response to TelemetryMeasure list.
    /// Each stock generates multiple telemetry values: Current, Open, High, Low, Close, Volume.
    /// </summary>
    private List<TelemetryMeasure> ParseResponse(TwseStockResponse? response, SensorMapping sensorMapping)
    {
        var measures = new List<TelemetryMeasure>();

        if (response?.IsSuccess != true || response.MsgArray == null)
        {
            _logger.LogWarning("TWSE API returned invalid response: {Code} - {Message}",
                response?.RtCode, response?.RtMessage);
            return measures;
        }

        foreach (var quote in response.MsgArray)
        {
            if (quote.Code == null) continue;

            // Look up ResourceId from sensor mapping
            if (!sensorMapping.FieldToResourceId.TryGetValue(quote.Code, out var resourceId))
            {
                _logger.LogDebug("No sensor mapping for stock {Code}, skipping", quote.Code);
                continue;
            }

            var timestamp = (quote.Timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds();

            // Common metadata for this stock
            var baseMetadata = new Dictionary<string, object>
            {
                ["StockCode"] = quote.Code,
                ["StockName"] = quote.Name ?? "",
                ["FullName"] = quote.FullName ?? "",
                ["Exchange"] = quote.Exchange ?? "tse",
                ["Unit"] = "TWD"
            };

            // Current Price (成交價)
            if (quote.CurrentPrice.HasValue)
            {
                measures.Add(CreateMeasure(resourceId, "Current", (double)quote.CurrentPrice.Value, timestamp, baseMetadata));
            }

            // Open Price (開盤價)
            if (TryParseDecimal(quote.OpenPrice, out var openPrice))
            {
                measures.Add(CreateMeasure(resourceId, "Open", (double)openPrice, timestamp, baseMetadata));
            }

            // High Price (最高價)
            if (TryParseDecimal(quote.HighPrice, out var highPrice))
            {
                measures.Add(CreateMeasure(resourceId, "High", (double)highPrice, timestamp, baseMetadata));
            }

            // Low Price (最低價)
            if (TryParseDecimal(quote.LowPrice, out var lowPrice))
            {
                measures.Add(CreateMeasure(resourceId, "Low", (double)lowPrice, timestamp, baseMetadata));
            }

            // Yesterday Close (昨收價)
            if (quote.PreviousClose.HasValue)
            {
                measures.Add(CreateMeasure(resourceId, "PreviousClose", (double)quote.PreviousClose.Value, timestamp, baseMetadata));
            }

            // Accumulated Volume (累積成交量)
            if (TryParseLong(quote.AccumulatedVolume, out var volume))
            {
                measures.Add(CreateMeasure(resourceId, "Volume", volume, timestamp, baseMetadata, "shares"));
            }

            // Price Change (漲跌)
            if (quote.PriceChange.HasValue)
            {
                measures.Add(CreateMeasure(resourceId, "Change", (double)quote.PriceChange.Value, timestamp, baseMetadata));
            }

            // Price Change Percent (漲跌幅)
            if (quote.PriceChangePercent.HasValue)
            {
                measures.Add(CreateMeasure(resourceId, "ChangePercent", (double)quote.PriceChangePercent.Value, timestamp, baseMetadata, "%"));
            }

            _logger.LogDebug("[{Code}] {Name}: Current={Price:F2}, Open={Open:F2}, High={High:F2}, Low={Low:F2}, Volume={Volume}",
                quote.Code, quote.Name,
                quote.CurrentPrice ?? 0,
                openPrice,
                highPrice,
                lowPrice,
                volume);
        }

        _logger.LogDebug("Parsed {Count} telemetry measures from TWSE response", measures.Count);
        return measures;
    }

    /// <summary>
    /// Creates a TelemetryMeasure with metadata.
    /// </summary>
    private static TelemetryMeasure CreateMeasure(
        string resourceId,
        string field,
        object value,
        long timestamp,
        Dictionary<string, object> baseMetadata,
        string? unit = null)
    {
        var metadata = new Dictionary<string, object>(baseMetadata)
        {
            ["Field"] = field
        };
        if (unit != null)
        {
            metadata["Unit"] = unit;
        }

        return new TelemetryMeasure
        {
            ResourceId = resourceId,
            Value = value,
            Timestamp = timestamp,
            Metadata = metadata
        };
    }

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        result = 0;
        return value != null && value != "-" && decimal.TryParse(value, out result);
    }

    private static bool TryParseLong(string? value, out long result)
    {
        result = 0;
        return value != null && value != "-" && long.TryParse(value, out result);
    }

    /// <summary>
    /// Stock API is read-only, commands are not supported.
    /// </summary>
    public Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Commands are not supported for TWSE Stock API");
        return Task.FromResult<ErrorOr<object>>(Error.Failure("TWSE.NotSupported", "TWSE Stock API is read-only"));
    }

    /// <summary>
    /// Stock API is read-only, write operations are not supported.
    /// </summary>
    public Task<bool> WriteSensorDataAsync(
        IEnumerable<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Write operations are not supported for TWSE Stock API");
        return Task.FromResult(false);
    }
}