using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Devices;

namespace StockMonitor;

/// <summary>
/// Stock monitor device that fetches real-time stock prices from TWSE API.
/// Architecture: StockMonitorDevice -> TwseStockParser (IRequestResponseProtocolParser)
///                                       -> HttpCommunication -> TwseStockClient
/// Uses RequestResponseDeviceBase for standard periodic telemetry polling.
/// </summary>
public class StockMonitorDevice : RequestResponseDeviceBase
{
    private readonly ILogger<StockMonitorDevice> _deviceLogger;

    /// <summary>
    /// Creates a StockMonitorDevice with the standard WedaBuilder pattern.
    /// </summary>
    public StockMonitorDevice(IWedaApplicationContext context)
        : this(context, context.DeviceConfiguration
            ?? throw new InvalidOperationException("DeviceConfiguration not found in ApplicationContext"))
    {
    }

    /// <summary>
    /// Creates a StockMonitorDevice with explicit configuration.
    /// Parser is created first, then its Communication is passed to DeviceBase.
    /// </summary>
    public StockMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : this(context, configuration, CreateParser(context, configuration))
    {
    }

    /// <summary>
    /// Internal constructor that accepts a pre-created parser.
    /// Passes parser to RequestResponseDeviceBase.
    /// </summary>
    private StockMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        TwseStockParser parser)
        : base(context, configuration, parser)
    {
        _deviceLogger = context.GetLogger<StockMonitorDevice>();

        // Enable telemetry tracking for logging
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;

        var stockCodes = GetStockCodesFromConfiguration(configuration);
        _deviceLogger.LogInformation(
            "StockMonitorDevice initialized with {Count} stock codes: {Codes}",
            stockCodes.Count,
            string.Join(", ", stockCodes));
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    /// <summary>
    /// Creates the full parser stack: TwseStockParser -> HttpCommunication -> TwseStockClient
    /// </summary>
    private static TwseStockParser CreateParser(IWedaApplicationContext context, DeviceConfiguration configuration)
    {
        var httpClient = CreateHttpClient();
        var stockClient = new TwseStockClient(httpClient, context.GetLogger<TwseStockClient>());
        var settings = new ConnectionSettings();
        var httpCommLogger = context.GetLogger<HttpCommunication>();
        var httpCommunication = new HttpCommunication(stockClient, settings, httpCommLogger);

        var stockCodes = GetStockCodesFromConfiguration(configuration);
        return new TwseStockParser(
            httpCommunication,
            stockCodes,
            context.GetLogger<TwseStockParser>());
    }

    private static List<string> GetStockCodesFromConfiguration(DeviceConfiguration configuration)
    {
        var stockCodes = configuration.Sensors.Select(sensor => sensor.Name).ToList();
        if (stockCodes.Any())
        {
            return stockCodes;
        }
        return ["2395"]; // Default: Advantech
    }

    public override Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _deviceLogger.LogWarning("StockMonitorDevice does not support commands: {Command}", command.DeviceCmd);
        return Task.FromResult(false);
    }

    private void OnDataReceived(object? sender, Weda.SubNode.Abstractions.Events.DataReceivedEvent e)
    {
        _deviceLogger.LogInformation("Received {Count} telemetry measures", e.Data.Count);

        foreach (var measure in e.Data)
        {
            var stockCode = measure.Metadata?.TryGetValue("StockCode", out var code) == true ? code?.ToString() : "N/A";
            var stockName = measure.Metadata?.TryGetValue("StockName", out var name) == true ? name?.ToString() : "";
            var field = measure.Metadata?.TryGetValue("Field", out var f) == true ? f?.ToString() : "Value";
            var unit = measure.Metadata?.TryGetValue("Unit", out var u) == true ? u?.ToString() : "";

            _deviceLogger.LogInformation(
                "  [{Code}] {Name} | {Field}: {Value} {Unit}",
                stockCode, stockName, field, measure.Value, unit);
        }
    }

    ~StockMonitorDevice()
    {
        DataReceived -= OnDataReceived;
    }
}