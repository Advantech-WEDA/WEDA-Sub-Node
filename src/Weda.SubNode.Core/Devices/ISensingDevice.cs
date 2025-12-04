using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Core.Protocols.ISensing;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// ISensing protocol device implementation.
/// Inherits from MessageBrokerDeviceBase for Publish-Subscribe communication pattern.
/// Adds ISensing-specific functionality like sensor control and configuration.
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: MyFirstISensingDevice -> MqttISensingDevice -> ISensingDevice -> MessageBrokerDeviceBase -> DeviceBase
/// </summary>
public class ISensingDevice : MessageBrokerDeviceBase, ISensorControl
{
    /// <summary>
    /// Initializes a new instance of ISensingDevice.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing ISensing settings.</param>
    /// <param name="messageBroker">Message broker instance (MQTT, NATS, etc.).</param>
    public ISensingDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IMessageBroker messageBroker)
        : base(context, configuration, CreateISensingParser(context, configuration, messageBroker))
    {
        _logger.LogDebug(
            "ISensingDevice initialized ({SensorCount} sensors)",
            configuration.Sensors.Count);
    }

    /// <summary>
    /// Creates the ISensingPubSubParser for this device.
    /// </summary>
    private static IPublishSubscribeProtocolParser CreateISensingParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IMessageBroker messageBroker)
    {
        var logger = context.LoggerFactory.CreateLogger<ISensingPubSubParser>();
        return new ISensingPubSubParser(configuration, messageBroker, logger);
    }

    #region ISensorControl Implementation

    /// <summary>
    /// Sets the state of a digital output.
    /// </summary>
    public virtual Task<bool> SetDigitalOutputAsync(string outputName, bool state, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("SetDigitalOutputAsync not yet implemented for ISensing device");
        return Task.FromResult(false);
    }

    /// <summary>
    /// Sets the value of an analog output.
    /// </summary>
    public virtual Task<bool> SetAnalogOutputAsync(string outputName, double value, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("SetAnalogOutputAsync not yet implemented for ISensing device");
        return Task.FromResult(false);
    }

    /// <summary>
    /// Requests current device configuration.
    /// </summary>
    public virtual Task<Dictionary<string, object>> GetConfigurationAsync(ushort configIndex = 0, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetConfigurationAsync not yet implemented for ISensing device");
        return Task.FromResult(new Dictionary<string, object>());
    }

    /// <summary>
    /// Updates device configuration.
    /// </summary>
    public virtual Task<bool> SetConfigurationAsync(ushort configIndex, Dictionary<string, object> configData, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("SetConfigurationAsync not yet implemented for ISensing device");
        return Task.FromResult(false);
    }

    /// <summary>
    /// Enables or disables a sensor.
    /// </summary>
    public virtual Task<bool> SetSensorEnabledAsync(string sensorName, bool enabled, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("SetSensorEnabledAsync not yet implemented for ISensing device");
        return Task.FromResult(false);
    }

    #endregion
}
