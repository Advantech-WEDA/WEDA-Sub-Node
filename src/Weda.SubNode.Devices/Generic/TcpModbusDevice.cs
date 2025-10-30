using Microsoft.Extensions.Logging;
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

    private static Abstractions.Communication.ICommunication CreateTcpCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var host = configuration.Communication.TryGetValue("Host", out var h) ? h?.ToString() ?? "localhost" : "localhost";
        var port = configuration.Communication.TryGetValue("Port", out var p) ? Convert.ToInt32(p) : 502;

        // Try to use context's factory method if available
        var contextType = context.GetType();
        var createMethod = contextType.GetMethod("CreateTcpCommunication", new[] { typeof(string), typeof(int) });
        if (createMethod != null)
        {
            try
            {
                var result = createMethod.Invoke(context, [host, port]);
                if (result is Abstractions.Communication.ICommunication communication)
                    return communication;
            }
            catch { }
        }

        // Fallback: create directly
        var logger = context.GetLogger<Core.Communication.CommunicationBase>();
        return new Core.Communication.TcpCommunication(host, port, null, logger);
    }
}
