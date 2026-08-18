using Microsoft.Extensions.Logging;
using StockMonitor.Communication;
using StockMonitor.Devices;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Communication.Common;

namespace StockMonitor;

/// <summary>
/// Pre-configured stock monitor device for Taiwan Stock Exchange (TWSE) API.
/// Automatically creates HTTP communication for accessing TWSE real-time quotes.
/// </summary>
[DeviceType(Sensors.TwseStockDevice.DeviceTypeName)]
public class TwseStockMonitorDevice : StockMonitorDevice
{
    private const int HTTP_TIMEOUT_IN_SECONDS = 30;

    /// <summary>
    /// Creates a TWSE stock monitor device with config key.
    /// Automatically retrieves configuration from context.DeviceConfigs[configKey].
    /// </summary>
    /// <param name="context">The application context</param>
    /// <param name="configKey">The configuration key from appsettings.json DeviceConfigs section</param>
    public TwseStockMonitorDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey])
    {
    }

    /// <summary>
    /// Creates a TWSE stock monitor device with explicit configuration.
    /// Automatically creates HttpCommunication with TwseStockClient for TWSE API access.
    /// </summary>
    public TwseStockMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateHttpCommunication(context, configuration))
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Creates the HttpCommunication with TwseStockClient for accessing TWSE API.
    /// </summary>
    private static HttpCommunication CreateHttpCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var httpClient = CreateHttpClient();
        var stockClient = new TwseStockClient(httpClient, context.GetLogger<TwseStockClient>());
        var settings = configuration.ConnectionSettings ?? new ConnectionSettings();
        var logger = context.GetLogger<CommunicationBase>();
        return new HttpCommunication(stockClient, settings, logger);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.Timeout = TimeSpan.FromSeconds(HTTP_TIMEOUT_IN_SECONDS);
        return client;
    }

    /// <summary>
    /// Event handler for telemetry data received from device.
    /// Prints stock price information.
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("TwseStockMonitorDevice: Data received, Count={Count}", e.Data.Count);

        foreach (var measure in e.Data)
        {
            var stockCode = measure.Metadata?.TryGetValue("StockCode", out var code) == true ? code?.ToString() : "N/A";
            var stockName = measure.Metadata?.TryGetValue("StockName", out var name) == true ? name?.ToString() : "";
            var metricName = measure.Metadata?.TryGetValue("MetricName", out var m) == true ? m?.ToString() : "Value";

            _logger.LogInformation(
                "  [{Code}] {Name} | {MetricName}: {Value}",
                stockCode, stockName, metricName, measure.Value);
        }
    }
}
