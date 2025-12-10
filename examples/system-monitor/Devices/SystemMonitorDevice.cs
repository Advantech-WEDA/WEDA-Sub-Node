using Microsoft.Extensions.Logging;
using SystemMonitorExample.Communication;
using SystemMonitorExample.Protocols.SystemMetrics;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Core.Devices;

namespace SystemMonitorExample.Devices;

/// <summary>
/// Base class for system monitoring devices that collect CPU, memory, disk, and network metrics.
/// Inherits from RequestResponseDeviceBase for Request/Response communication pattern.
///
/// This class is responsible for creating the Parser layer (SystemMetricsParser).
/// Subclasses are responsible for providing the Communication layer.
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: LocalSystemMonitorDevice -> SystemMonitorDevice -> RequestResponseDeviceBase -> DeviceBase
///
/// Layered Responsibility (following TcpModbusDevice pattern):
/// - SystemMonitorDevice: Creates Parser (protocol layer)
/// - LocalSystemMonitorDevice: Creates Communication (transport layer)
/// </summary>
public class SystemMonitorDevice : RequestResponseDeviceBase
{
    /// <summary>
    /// Initializes a new instance of SystemMonitorDevice.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing sensor settings.</param>
    /// <param name="communication">Communication instance for system metrics collection.</param>
    public SystemMonitorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        LocalSystemCommunication communication)
        : base(context, configuration, CreateParser(context, configuration, communication))
    {
        _logger.LogDebug(
            "SystemMonitorDevice initialized ({SensorCount} sensors)",
            configuration.Sensors.Count);
    }

    /// <summary>
    /// Creates the SystemMetricsParser for this device.
    /// Parser uses the provided communication instance.
    /// </summary>
    private static IRequestResponseProtocolParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        LocalSystemCommunication communication)
    {
        var loggerFactory = context.LoggerFactory;
        return new SystemMetricsParser(
            configuration,
            communication,
            loggerFactory.CreateLogger<SystemMetricsParser>());
    }
}
