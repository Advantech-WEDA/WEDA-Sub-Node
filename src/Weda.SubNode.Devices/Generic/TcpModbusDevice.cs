using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Devices;

namespace Weda.SubNode.Devices.Generic;

/// <summary>
/// Pre-configured Modbus TCP device implementation
/// Simplified API using ApplicationContext
/// </summary>
public class TcpModbusDevice : ModbusDevice
{
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

        // Create TCP communication directly
        // Connection will be established automatically by DeviceBase.InitializeAsync via ConnectionManager
        var logger = context.GetLogger<Core.Communication.CommunicationBase>();
        return new Core.Communication.TcpCommunication(host, port, null, logger);
    }
}
