using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.ISensing;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// ISensing protocol device implementation.
/// Inherits from PubSubDeviceBase for Pub/Sub communication pattern.
/// Adds ISensing-specific functionality like sensor control and configuration.
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: MyFirstISensingDevice -> MqttISensingDevice -> ISensingDevice -> PubSubDeviceBase -> DeviceBase
/// </summary>
public class ISensingDevice : PubSubDeviceBase, ISensorControl
{
    /// <summary>
    /// Initializes a new instance of ISensingDevice.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing ISensing settings.</param>
    /// <param name="pubSub">Pub/Sub communication instance (MQTT, NATS, etc.).</param>
    public ISensingDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IPubSub pubSub)
        : base(context, configuration, CreateParser(configuration, pubSub, context.GetLogger<ISensingPubSubParser>()))
    {
    }

    /// <summary>
    /// Creates the ISensingPubSubParser for this device.
    /// </summary>
    private static ISensingPubSubParser CreateParser(
        DeviceConfiguration configuration,
        IPubSub pubSub,
        ILogger<ISensingPubSubParser> logger)
    {
        return new ISensingPubSubParser(configuration, pubSub, logger);
    }

    #region ISensorControl Implementation

    public Task<bool> SetDigitalOutputAsync(string outputName, bool state, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting digital output {OutputName} to {State} on device {SubNodeId}",
            outputName, state, SubNodeId);

        var command = new DeviceCommand
        {
            DeviceCmd = "SetDigitalOutput",
            Parameters = new Dictionary<string, object>
            {
                ["outputName"] = outputName,
                ["state"] = state
            }
        };

        return ExecuteCommandAsync(command, cancellationToken);
    }

    public Task<bool> SetAnalogOutputAsync(string outputName, double value, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting analog output {OutputName} to {Value} on device {SubNodeId}",
            outputName, value, SubNodeId);

        var command = new DeviceCommand
        {
            DeviceCmd = "SetAnalogOutput",
            Parameters = new Dictionary<string, object>
            {
                ["outputName"] = outputName,
                ["value"] = value
            }
        };

        return ExecuteCommandAsync(command, cancellationToken);
    }

    public Task<Dictionary<string, object>> GetConfigurationAsync(ushort configIndex = 0, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting configuration index {ConfigIndex} on device {SubNodeId}",
            configIndex, SubNodeId);

        var command = new DeviceCommand
        {
            DeviceCmd = "GetConfig",
            Parameters = new Dictionary<string, object>
            {
                ["configIndex"] = configIndex
            }
        };

        ExecuteCommandAsync(command, cancellationToken);

        // Note: In real implementation, this would wait for response from device
        // For now, return empty dictionary indicating command was sent
        return Task.FromResult(new Dictionary<string, object>());
    }

    public Task<bool> SetConfigurationAsync(ushort configIndex, Dictionary<string, object> configData, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting configuration index {ConfigIndex} on device {SubNodeId}",
            configIndex, SubNodeId);

        var command = new DeviceCommand
        {
            DeviceCmd = "SetConfig",
            Parameters = new Dictionary<string, object>
            {
                ["configIndex"] = configIndex,
                ["configData"] = configData
            }
        };

        return ExecuteCommandAsync(command, cancellationToken);
    }

    public Task<bool> SetSensorEnabledAsync(string sensorName, bool enabled, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("{Action} sensor {SensorName} on device {SubNodeId}",
            enabled ? "Enabling" : "Disabling", sensorName, SubNodeId);

        var command = new DeviceCommand
        {
            DeviceCmd = "SetSensorEnable",
            Parameters = new Dictionary<string, object>
            {
                ["sensorName"] = sensorName,
                ["enabled"] = enabled
            }
        };

        return ExecuteCommandAsync(command, cancellationToken);
    }

    #endregion
}
