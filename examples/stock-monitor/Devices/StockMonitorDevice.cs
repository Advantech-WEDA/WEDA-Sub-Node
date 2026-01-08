using Microsoft.Extensions.Logging;


using StockMonitor.Communication;
using StockMonitor.Protocols;


using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Core.Devices;

namespace StockMonitor.Devices;

/// <summary>
/// Base class for stock monitoring devices that fetch real-time stock prices.
/// Inherits from RequestResponseDeviceBase for Request/Response communication pattern.
///
/// This class is responsible for creating the Parser layer (TwseStockParser).
/// Subclasses are responsible for providing the Communication layer.
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: TwseStockMonitorDevice -> StockMonitorDevice -> RequestResponseDeviceBase -> DeviceBase
///
/// Layered Responsibility (following ModbusDevice/TcpModbusDevice pattern):
/// - StockMonitorDevice: Creates Parser (protocol layer)
/// - TwseStockMonitorDevice: Creates Communication (transport layer)
/// </summary>
public class StockMonitorDevice : RequestResponseDeviceBase
{
    /// <summary>
    /// Initializes a new instance of StockMonitorDevice.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing sensor settings.</param>
    /// <param name="communication">Communication instance for stock data fetching.</param>
    public StockMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        HttpCommunication communication)
        : base(context, configuration, CreateParser(context, configuration, communication))
    {
        _logger.LogDebug(
            "StockMonitorDevice initialized ({SensorCount} sensors)",
            configuration.Sensors.Count);
    }

    /// <summary>
    /// Creates the TwseStockParser for this device.
    /// Parser uses the provided communication instance and DeviceConfiguration for sensor mapping.
    /// </summary>
    private static IRequestResponseProtocolParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        HttpCommunication communication)
    {
        return new TwseStockParser(
            configuration,
            communication,
            context.GetLogger<TwseStockParser>());
    }
}
