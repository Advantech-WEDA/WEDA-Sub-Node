using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Communication;
using Weda.SubNode.Core.Devices;

namespace Weda.SubNode.Devices.Generic;

/// <summary>
/// Pre-configured Modbus TCP device implementation
/// Simplified API using ApplicationContext
/// </summary>
public class TcpModbusDevice : ModbusDevice
{
    /// <summary>
    /// Factory method required by IDevice interface
    /// Creates a TCP Modbus device from configuration
    /// </summary>
    public static IDevice Create(IWedaApplicationContext context, DeviceConfiguration configuration)
    {
        return new TcpModbusDevice(context, configuration);
    }

    /// <summary>
    /// Creates a TCP Modbus device with ApplicationContext only.
    /// Automatically retrieves configuration from context and creates TCP communication.
    /// </summary>
    public TcpModbusDevice(IWedaApplicationContext context)
        : this(context, context.DeviceConfiguration
            ?? throw new InvalidOperationException("DeviceConfiguration not found in ApplicationContext"))
    {
    }

    /// <summary>
    /// Creates a TCP Modbus device with ApplicationContext and explicit configuration.
    /// Automatically creates TCP communication from configuration (Host, Port).
    /// </summary>
    public TcpModbusDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateTcpCommunication(context, configuration))
    {
    }

    private static IRequestResponseCommunication<byte[], byte[]> CreateTcpCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var host = configuration.Communication.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost";
        var port = configuration.Communication.TryGetValue("Port", out var p) ? Convert.ToInt32(p) : 502;

        // Use ConnectionSettings from configuration (retry, timeout, security)
        var connectionSettings = configuration.ConnectionSettings ?? new ConnectionSettings();

        // Create TCP communication directly
        // Connection will be established automatically by DeviceBase.InitializeAsync via ConnectionManager
        var logger = context.GetLogger<CommunicationBase>();
        return new TcpCommunication(host, port, connectionSettings, logger);
    }
}
