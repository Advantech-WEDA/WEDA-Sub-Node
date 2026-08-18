using System.Text.Json;
using Microsoft.Extensions.Logging;
using StockMonitor.Communication;
using StockMonitor.Models;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace StockMonitor.Protocols;

/// <summary>
/// Parser for Taiwan Stock Exchange (TWSE) stock quote API.
/// </summary>
public class TwseStockParser : IRequestResponseProtocolParser
{
    private readonly DeviceConfiguration _configuration;
    private readonly HttpCommunication _communication;
    private readonly ILogger<TwseStockParser> _logger;
    private readonly Dictionary<string, List<SensorMetricsConfig>> _sensorConfigByStockCode;

    public ICommunication Communication => _communication;

    public TwseStockParser(
        DeviceConfiguration configuration,
        HttpCommunication communication,
        ILogger<TwseStockParser> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Pre-process sensor configuration at construction time
        _sensorConfigByStockCode = PreprocessSensorReporturation(configuration.Sensors);
        _logger.LogDebug("TwseStockParser initialized with {StockCount} stocks, {SensorCount} sensors",
            _sensorConfigByStockCode.Count, configuration.Sensors.Count);
    }

    /// <summary>
    /// Pre-process sensor configuration at construction time.
    /// Deserializes parameters once and groups by StockCode for efficient lookup.
    /// </summary>
    private Dictionary<string, List<SensorMetricsConfig>> PreprocessSensorReporturation(IReadOnlyList<Sensor> sensors)
    {
        var result = new Dictionary<string, List<SensorMetricsConfig>>();

        foreach (var sensor in sensors)
        {
            // Deserialize parameters to strongly-typed object
            var parameters = DeserializeParameters(sensor);
            if (string.IsNullOrEmpty(parameters.StockCode))
            {
                _logger.LogWarning("Sensor {Name} missing StockCode parameter, skipping", sensor.Name);
                continue;
            }

            if (!result.TryGetValue(parameters.StockCode, out var configList))
            {
                configList = [];
                result[parameters.StockCode] = configList;
            }

            configList.Add(new SensorMetricsConfig(sensor, parameters));

            _logger.LogDebug("Sensor {Name}: StockCode={StockCode}, Metrics=[{Metrics}]",
                sensor.Name, parameters.StockCode, string.Join(", ", parameters.Metrics));
        }

        return result;
    }

    /// <summary>
    /// Extract StockSensorParameters from sensor.Parameters dictionary.
    /// Handles both JsonElement (from direct JSON) and Dictionary (from Configuration binding).
    /// </summary>
    private static StockSensorParameters DeserializeParameters(Sensor sensor)
    {
        if (sensor.Parameters == null)
            throw new InvalidOperationException("No parameters defined for stocks");

        string stockCode = string.Empty;
        List<string> metrics = new();

        // --- StockCode ---
        if (sensor.Parameters.TryGetValue("StockCode", out var stockCodeObj))
        {
            if (stockCodeObj is string s)
                stockCode = s;
            else if (stockCodeObj is JsonElement json && json.ValueKind == JsonValueKind.String)
                stockCode = json.GetString() ?? throw new JsonException("Unable to parse StockCode");
        }

        // --- Metrics ---

        if (sensor.Parameters.TryGetValue("Metrics", out var metricsObj))
        {
            if (metricsObj is string s)
                metrics = [.. s.Split(",").Select(x => x.Trim())];
            else if (metricsObj is JsonElement json && json.ValueKind == JsonValueKind.String)
            {
                var t = json.GetString() ?? throw new JsonException("Unable to parse StockCode");
                metrics = [.. t.Split(",").Select(x => x.Trim())];
            }
        }

        return new StockSensorParameters(stockCode, metrics);
    }


    /// <summary>
    /// Read telemetry data for all enabled sensors.
    /// </summary>
    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var enabledResourceIds = _configuration.Sensors
            .Where(s => s.Report.Enabled)
            .Select(s => s.ResourceId)
            .ToHashSet();

        return await ReadTelemetryForSensorsAsync(enabledResourceIds, cancellationToken);
    }

    /// <summary>
    /// Read telemetry data for specific sensors only.
    /// </summary>
    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(
        IEnumerable<string> sensorResourceIds,
        CancellationToken cancellationToken = default)
    {
        var requestedIds = sensorResourceIds.ToHashSet();
        return await ReadTelemetryForSensorsAsync(requestedIds, cancellationToken);
    }

    /// <summary>
    /// Internal method to read telemetry for a specific set of sensors.
    /// Uses pre-processed configuration for efficient lookup.
    /// </summary>
    private async Task<List<TelemetryMeasure>> ReadTelemetryForSensorsAsync(
        HashSet<string> requestedResourceIds,
        CancellationToken cancellationToken)
    {
        if (!_communication.IsConnected)
        {
            _logger.LogWarning("Cannot read stock data: communication not connected");
            return [];
        }

        // Filter to only requested sensors and get unique stock codes
        var stockCodesToFetch = _sensorConfigByStockCode
            .Where(kvp => kvp.Value.Any(config =>
                config.Sensor.Report.Enabled && requestedResourceIds.Contains(config.Sensor.ResourceId)))
            .Select(kvp => kvp.Key)
            .ToList();

        if (stockCodesToFetch.Count == 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Fetching stock data for codes: {Codes}", string.Join(", ", stockCodesToFetch));

            // Use Request-Response pattern via HttpCommunication.RequestAsync
            var request = new StockQuoteRequest(stockCodesToFetch);
            var response = await _communication.RequestAsync(request, cancellationToken);

            // Parse response using pre-processed configuration
            return ParseResponse(response, requestedResourceIds);
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
    /// Parses TWSE API response to TelemetryMeasure list using pre-processed configuration.
    /// </summary>
    private List<TelemetryMeasure> ParseResponse(TwseStockResponse? response, HashSet<string> requestedResourceIds)
    {
        var measures = new List<TelemetryMeasure>();

        if (response?.IsSuccess != true || response.MsgArray == null)
        {
            _logger.LogWarning("TWSE API returned invalid response: {Code} - {Message}",
                response?.RtCode, response?.RtMessage);
            return measures;
        }

        // Build lookup: StockCode -> Quote
        var quotesByCode = response.MsgArray
            .Where(q => q.Code != null)
            .ToDictionary(q => q.Code!, q => q);

        // Use pre-processed configuration
        foreach (var (stockCode, configList) in _sensorConfigByStockCode)
        {
            if (!quotesByCode.TryGetValue(stockCode, out var quote))
            {
                _logger.LogDebug("No quote data for stock {Code}", stockCode);
                continue;
            }

            var timestamp = (quote.Timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds();

            foreach (var config in configList)
            {
                // Skip if sensor is not enabled or not requested
                if (!config.Sensor.Report.Enabled || !requestedResourceIds.Contains(config.Sensor.ResourceId))
                    continue;

                // Use pre-parsed metrics list
                foreach (var metricName in config.Parameters.Metrics!)
                {
                    var value = GetMetricValue(quote, metricName);
                    if (value != null)
                    {
                        measures.Add(new TelemetryMeasure
                        {
                            ResourceId = config.Sensor.ResourceId,
                            Value = value,
                            Timestamp = timestamp,
                            Metadata = new Dictionary<string, object>
                            {
                                ["StockCode"] = stockCode,
                                ["StockName"] = quote.Name ?? "",
                                ["MetricName"] = metricName
                            }
                        });
                    }
                }
            }
        }

        _logger.LogDebug("Parsed {Count} telemetry measures from TWSE response", measures.Count);
        return measures;
    }

    /// <summary>
    /// Get metric value from quote based on MetricName.
    /// </summary>
    private static object? GetMetricValue(TwseStockQuote quote, string metricName)
    {
        return metricName.ToLowerInvariant() switch
        {
            "current" or "price" => quote.CurrentPrice.HasValue ? (double)quote.CurrentPrice.Value : null,
            "open" => TryParseDecimal(quote.OpenPrice, out var o) ? (double)o : null,
            "high" => TryParseDecimal(quote.HighPrice, out var h) ? (double)h : null,
            "low" => TryParseDecimal(quote.LowPrice, out var l) ? (double)l : null,
            "previousclose" or "close" => quote.PreviousClose.HasValue ? (double)quote.PreviousClose.Value : null,
            "volume" => TryParseLong(quote.AccumulatedVolume, out var v) ? v : null,
            "change" => quote.PriceChange.HasValue ? (double)quote.PriceChange.Value : null,
            "changepercent" => quote.PriceChangePercent.HasValue ? (double)quote.PriceChangePercent.Value : null,
            _ => null
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
}
