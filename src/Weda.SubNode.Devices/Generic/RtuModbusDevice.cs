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
    private readonly string _portName;

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
            CreateModbusRtuCommunication(context, configuration, out var portName),
            slaveId: GetSlaveId(configuration),
            byteOrder: GetByteOrder(configuration))
    {
        _portName = portName;
    }

    private static IRequestResponseCommunication<byte[], byte[]> CreateModbusRtuCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        out string portName)
    {
        // Convert DeviceCommunication dictionary directly to strongly-typed settings
        var serialSettings = configuration.DeviceCommunication.GetObject<SerialCommunicationSettings>()
            ?? new SerialCommunicationSettings();

        // Use ConnectionSettings from configuration (retry, timeout, security)
        var connectionSettings = configuration.ConnectionSettings ?? new ConnectionSettings();

        portName = serialSettings.PortName;

        // Use shared factory to get or create serial communication
        var factory = SerialCommunicationFactory.GetInstance(context.LoggerFactory);
        var serialCommunication = factory.GetOrCreate(
            serialSettings.PortName,
            serialSettings,
            connectionSettings);

        // Create RTU communication and wrap with ModbusRtuCommunication for CRC handling
        // Connection will be established automatically by DeviceBase.InitializeAsync via ConnectionManager
        var logger = context.GetLogger<CommunicationBase>();

        return new ModbusRtuCommunication(serialCommunication, logger);
    }

    /// <summary>
    /// Decrease reference count when device disposed
    /// </summary>
    public override void Dispose()
    {
        SerialCommunicationFactory.GetInstance(null!).Release(_portName);
        base.Dispose();
    }
}