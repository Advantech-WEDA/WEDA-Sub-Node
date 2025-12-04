using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Core;
using Weda.SubNode.Core.Communication;
using Weda.SubNode.Core.Devices;

namespace WebSocketStreamingExample;

/// <summary>
/// WebSocket streaming device that demonstrates streaming communication pattern.
/// Inherits from StreamingDeviceBase for Streaming communication pattern.
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: WebSocketStreamingDevice -> StreamingDeviceBase -> DeviceBase
/// </summary>
public class WebSocketStreamingDevice : StreamingDeviceBase
{
    /// <summary>
    /// Initializes a new instance of WebSocketStreamingDevice.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing sensor settings.</param>
    /// <param name="webSocketUri">WebSocket server URI to connect to.</param>
    public WebSocketStreamingDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        string webSocketUri)
        : base(context, configuration, CreateParser(context, configuration, webSocketUri))
    {
        _logger.LogDebug(
            "WebSocketStreamingDevice initialized ({SensorCount} sensors, Uri={Uri})",
            configuration.Sensors.Count,
            webSocketUri);

        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Creates the WebSocketStreamingParser for this device.
    /// </summary>
    private static IStreamingProtocolParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        string webSocketUri)
    {
        var loggerFactory = context.LoggerFactory;
        var communication = new WebSocketCommunication(
            webSocketUri,
            configuration.ConnectionSettings,
            loggerFactory.CreateLogger<CommunicationBase>());

        return new WebSocketStreamingParser(
            configuration,
            communication,
            loggerFactory.CreateLogger<WebSocketStreamingParser>());
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("MyFirstDevice: Data received, Count={Count}", e.Data.Count);

        // Print all sensor values based on configuration
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value != null)
            {
                _logger.LogInformation("{SensorName}: {Value} (Enabled={Enabled})",
                    sensor.Name,
                    measure.Value,
                    sensor.Config.Enabled);
            }
        }
    }
}
