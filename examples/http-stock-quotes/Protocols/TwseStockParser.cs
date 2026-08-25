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
    private readonly Dictionary<string, List<StockSensorBinding>> _bindingsByStockCode;

    public ICommunication Communication => _communication;

    public TwseStockParser(
        DeviceConfiguration configuration,
        HttpCommunication communication,
        ILogger<TwseStockParser> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Bind each sensor to its stock code and metric once, at construction time
        _bindingsByStockCode = BindSensorsByStockCode(configuration.Sensors);
        _logger.LogDebug("TwseStockParser initialized with {StockCount} stocks, {SensorCount} sensors",
            _bindingsByStockCode.Count, configuration.Sensors.Count);
    }

    /// <summary>
    /// Resolves each sensor's parameters once and groups the bindings by stock code, so a poll can
    /// fetch one quote per code and fan it back out to the sensors that asked for it.
    /// </summary>
    /// <param name="sensors">The configured sensors.</param>
    /// <returns>Bindings grouped by stock code; sensors with unusable parameters are skipped.</returns>
    private Dictionary<string, List<StockSensorBinding>> BindSensorsByStockCode(IReadOnlyList<Sensor> sensors)
    {
        var result = new Dictionary<string, List<StockSensorBinding>>();

        foreach (var sensor in sensors)
        {
            var parameters = DeserializeParameters(sensor);

            if (string.IsNullOrEmpty(parameters.StockCode))
            {
                _logger.LogWarning("Sensor {Name} missing StockCode parameter, skipping", sensor.Name);
                continue;
            }

            if (!StockMetricCatalog.IsKnown(parameters.Metric))
            {
                _logger.LogWarning(
                    "Sensor {Name} requests unknown metric '{Metric}', skipping. Known metrics: {Known}",
                    sensor.Name, parameters.Metric, string.Join(", ", StockMetricCatalog.Names));
                continue;
            }

            if (!result.TryGetValue(parameters.StockCode, out var bindings))
            {
                bindings = [];
                result[parameters.StockCode] = bindings;
            }

            bindings.Add(new StockSensorBinding(sensor, parameters));

            _logger.LogDebug("Sensor {Name}: StockCode={StockCode}, Metric={Metric}",
                sensor.Name, parameters.StockCode, parameters.Metric);
        }

        return result;
    }

    /// <summary>
    /// Extracts <see cref="StockSensorParameters"/> from a sensor's parameter dictionary.
    /// Handles both <see cref="JsonElement"/> (from direct JSON) and string (from configuration binding).
    /// </summary>
    /// <param name="sensor">The sensor whose parameters to read.</param>
    /// <returns>The stock code and metric declared for the sensor; either may be empty when absent.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the sensor declares no parameters.</exception>
    private static StockSensorParameters DeserializeParameters(Sensor sensor)
    {
        if (sensor.Parameters == null)
            throw new InvalidOperationException(
                $"Sensor '{sensor.Name}' defines no parameters; StockCode and Metric are required.");

        return new StockSensorParameters(
            ReadString(sensor.Parameters, "StockCode"),
            ReadString(sensor.Parameters, "Metric"));
    }

    /// <summary>Reads a string parameter, tolerating both bound strings and raw JSON elements.</summary>
    private static string ReadString(IReadOnlyDictionary<string, object> parameters, string key)
    {
        if (!parameters.TryGetValue(key, out var value))
            return string.Empty;

        return value switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString() ?? string.Empty,
            _ => string.Empty,
        };
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
        var stockCodesToFetch = _bindingsByStockCode
            .Where(kvp => kvp.Value.Any(binding =>
                binding.Sensor.Report.Enabled && requestedResourceIds.Contains(binding.Sensor.ResourceId)))
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
    internal List<TelemetryMeasure> ParseResponse(TwseStockResponse? response, HashSet<string> requestedResourceIds)
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

        foreach (var (stockCode, bindings) in _bindingsByStockCode)
        {
            if (!quotesByCode.TryGetValue(stockCode, out var quote))
            {
                _logger.LogDebug("No quote data for stock {Code}", stockCode);
                continue;
            }

            var timestamp = (quote.Timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds();

            foreach (var binding in bindings)
            {
                if (!binding.Sensor.Report.Enabled || !requestedResourceIds.Contains(binding.Sensor.ResourceId))
                    continue;

                var value = GetMetricValue(quote, binding.Parameters.Metric);
                if (value == null)
                    continue;

                // Metadata is deliberately left unset. It is the framework's chunked-transfer
                // descriptor, and a numeric value is never chunked, so it never receives the
                // transferId the WedaNode telemetry proxy requires — a measure carrying metadata
                // without one is rejected outright. The stock code and metric are already carried
                // by the sensor's own identity.
                measures.Add(new TelemetryMeasure
                {
                    ResourceId = binding.Sensor.ResourceId,
                    Value = value,
                    Timestamp = timestamp,
                });
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
