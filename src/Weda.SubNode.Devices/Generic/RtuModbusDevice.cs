using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Utilities;
using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Serial;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Core.Protocols.Modbus.Communication;

namespace Weda.SubNode.Devices.Generic;

/// <summary>
/// Pre-configured Modbus RTU device implementation
/// Simplified API using ApplicationContext
/// </summary>
public class RtuModbusDevice : ModbusDevice
{
    /// <summary>
    /// Creates a RTU Modbus with ApplicationContext and config key.
    /// AUtomatically retrieves configuration from context.DeviceConfigs[configKey].
    /// </summary>
    /// <param name="context">The application context</param>
    /// <param name="configKey">The configuration key from deviceconfig.json in DeviceConfigs section.</param>
    public RtuModbusDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey])
    {   
    }

    public RtuModbusDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(
            context,
            configuration,
            CreateModbusRtuCommunication(context, configuration),
            slaveId: GetSlaveId(configuration),
            byteOrder: GetByteOrder(configuration))
    {   
    }

    private static IRequestResponseCommunication<byte[], byte[]> CreateModbusRtuCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        // Convert DeviceCommunication dictionary directly to strongly-typed settings
        var serialSettings = configuration.DeviceCommunication.GetObject<SerialCommunicationSettings>()
            ?? new SerialCommunicationSettings();

        // Use ConnectionSettings from configuration (retry, timeout, security)
        var connectionSettings = configuration.ConnectionSettings ?? new ConnectionSettings();

        // Create RTU communication and wrap with ModbusRtuCommunication for CRC handling
        // Connection will be established automatically by DeviceBase.InitializeAsync via ConnectionManager
        var logger = context.GetLogger<CommunicationBase>();
        var serialCommunication = new SerialCommunication(serialSettings, connectionSettings, logger);
        return new ModbusRtuCommunication(serialCommunication, logger);
    }
}