using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Utilities;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Tcp;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Core.Protocols.Modbus;

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
    /// Reads SlaveId and ByteOrder from Properties dictionary.
    /// </summary>
    public TcpModbusDevice(IWedaApplicationContext context)
        : this(context, context.DeviceConfiguration
            ?? throw new InvalidOperationException("DeviceConfiguration not found in ApplicationContext"))
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
            CreateTcpCommunication(context, configuration),
            slaveId: GetSlaveId(configuration),
            byteOrder: GetByteOrder(configuration))
    {
    }

    private static IRequestResponseCommunication<byte[], byte[]> CreateTcpCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        // Convert Communication dictionary directly to strongly-typed settings
        var tcpSettings = configuration.Communication.GetObject<TcpCommunicationSettings>()
            ?? new TcpCommunicationSettings();

        // Use ConnectionSettings from configuration (retry, timeout, security)
        var connectionSettings = configuration.ConnectionSettings ?? new ConnectionSettings();

        // Create TCP communication directly
        // Connection will be established automatically by DeviceBase.InitializeAsync via ConnectionManager
        var logger = context.GetLogger<CommunicationBase>();
        return new TcpCommunication(tcpSettings.Host, tcpSettings.Port, connectionSettings, logger);
    }

    private static byte GetSlaveId(DeviceConfiguration configuration)
    {
        // Check Properties first (recommended), then Communication for backwards compatibility
        return configuration.Properties.TryGetValue("SlaveId", out _)
            ? (byte)configuration.Properties.GetInt32("SlaveId", 1)
            : (byte)configuration.Communication.GetInt32("SlaveId", 1);
    }

    private static ModbusByteOrder GetByteOrder(DeviceConfiguration configuration)
    {
        // Check Properties first (recommended), then Communication for backwards compatibility
        return configuration.Properties.TryGetValue("ByteOrder", out _)
            ? configuration.Properties.GetEnum("ByteOrder", ModbusByteOrder.BigEndian)
            : configuration.Communication.GetEnum("ByteOrder", ModbusByteOrder.BigEndian);
    }
}
