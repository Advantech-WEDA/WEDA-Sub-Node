using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Utilities;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Tcp;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Core.Protocols.Modbus.Communication;

namespace Weda.SubNode.Devices.Generic;

/// <summary>
/// Pre-configured Modbus TCP device implementation
/// Simplified API using ApplicationContext
/// </summary>
public class TcpModbusDevice : ModbusDevice
{
    /// <summary>
    /// Creates a TCP Modbus device with ApplicationContext and config key.
    /// Automatically retrieves configuration from context.DeviceConfigs[configKey].
    /// </summary>
    /// <param name="context">The application context</param>
    /// <param name="configKey">The configuration key from deviceconfig.json in DeviceConfigs section.</param>
    public TcpModbusDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey])
    {
    }

    /// <summary>
    /// Creates a TCP Modbus device with ApplicationContext and explicit configuration.
    /// Automatically creates TCP communication from configuration (Host, Port).
    /// Reads SlaveId and ByteOrder from Properties dictionary.
    /// </summary>
    public TcpModbusDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(
            context,
            configuration,
            CreateModbusTcpCommunication(context, configuration),
            slaveId: GetSlaveId(configuration),
            byteOrder: GetByteOrder(configuration))
    {
    }

    private static IRequestResponseCommunication<byte[], byte[]> CreateModbusTcpCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        // Convert DeviceCommunication dictionary directly to strongly-typed settings
        var tcpSettings = configuration.DeviceCommunication.GetObject<TcpCommunicationSettings>()
            ?? new TcpCommunicationSettings();

        // Use ConnectionSettings from configuration (retry, timeout, security)
        var connectionSettings = configuration.ConnectionSettings ?? new ConnectionSettings();

        // Create TCP communication and wrap with ModbusTcpCommunication for MBAP handling
        // Connection will be established automatically by DeviceBase.InitializeAsync via ConnectionManager
        var logger = context.GetLogger<CommunicationBase>();
        var tcpCommunication = new TcpCommunication(tcpSettings.Host, tcpSettings.Port, connectionSettings, logger);
        return new ModbusTcpCommunication(tcpCommunication, logger);
    }
}
