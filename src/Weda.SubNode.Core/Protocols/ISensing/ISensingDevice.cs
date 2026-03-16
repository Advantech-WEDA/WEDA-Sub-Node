using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Devices.Capabilities;
using Weda.SubNode.Core.Devices;

namespace Weda.SubNode.Core.Protocols.ISensing;

/// <summary>
/// ISensing protocol device implementation.
/// Inherits from PubSubDeviceBase for Pub/Sub communication pattern.
/// Adds ISensing-specific functionality like sensor control and configuration.
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: MyFirstISensingDevice -> MqttISensingDevice -> ISensingDevice -> PubSubDeviceBase -> DeviceBase
/// </summary>
public class ISensingDevice : PubSubDeviceBase,
    IDigitalOutputControllable, IAnalogOutputControllable,
    IDigitalOutputReadable, IAnalogOutputReadable,
    IDigitalInputReadable, IAnalogInputReadable
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

    #region IDigitalOutputControllable Implementation

    /// <summary>
    /// Sets the state of a digital output using ISensing protocol.
    /// </summary>
    /// <param name="outputName">The name of the digital output (must match a DO sensor in configuration)</param>
    /// <param name="state">The desired state: true = ON, false = OFF</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> SetDigitalOutputAsync(string outputName, bool state, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SetDigitalOutputAsync: {OutputName}={State}", outputName, state);

        var command = new DeviceCommand
        {
            DeviceCmd = "SetDO",
            Parameters = new Dictionary<string, object>
            {
                ["name"] = outputName,
                ["state"] = state
            }
        };

        var result = await _parser.ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("SetDigitalOutputAsync failed for {OutputName}: {Error}",
                outputName, result.FirstError.Description);
            return false;
        }

        _logger.LogInformation("SetDigitalOutputAsync succeeded: {OutputName}={State}", outputName, state);
        return true;
    }

    #endregion

    #region IAnalogOutputControllable Implementation

    /// <summary>
    /// Sets the value of a analog output using ISensing protocol.
    /// </summary>
    /// <param name="outputName">The name of the analog output (must match a AO sensor in configuration)</param>
    /// <param name="value">The desired value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> SetAnalogOutputAsync(string outputName, object value, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("SetAnalogOutputAsync: {OutputName}={State}", outputName, value);

        var command = new DeviceCommand
        {
            DeviceCmd = "SetAO",
            Parameters = new Dictionary<string, object>
            {
                ["name"] = outputName,
                ["value"] = value
            }
        };

        var result = await _parser.ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("SetAnalogOutputAsync failed for {OutputName}: {Error}",
                outputName, result.FirstError.Description);
            return false;
        }

        _logger.LogInformation("SetAnalogOutputAsync succeeded: {OutputName}={Value}", outputName, value);
        return true;
    }

    #endregion

    #region IDigitalOutputReadable Implementation

    /// <summary>
    /// Reads the current state of a digital output from the last received telemetry.
    /// ISensing uses pub/sub pattern, so this returns the most recently received value.
    /// </summary>
    /// <param name="outputName">The name of the digital output (must match a DO sensor in configuration)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current state: true = ON, false = OFF, null if not found or no data received yet</returns>
    public Task<bool?> GetDigitalOutputAsync(string outputName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetDigitalOutputAsync: {OutputName}", outputName);
        return Task.FromResult(GetCachedBooleanValue(outputName));
    }

    #endregion

    #region IAnalogOutputReadable Implementation

    /// <summary>
    /// Reads the current value of an analog output from the last received telemetry.
    /// ISensing uses pub/sub pattern, so this returns the most recently received value.
    /// </summary>
    /// <param name="outputName">The name of the analog output (must match an AO sensor in configuration)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current value, or null if not found or no data received yet</returns>
    public Task<double?> GetAnalogOutputAsync(string outputName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetAnalogOutputAsync: {OutputName}", outputName);
        return Task.FromResult(GetCachedDoubleValue(outputName));
    }

    #endregion

    #region IDigitalInputReadable Implementation

    /// <summary>
    /// Reads the current state of a digital input from the last received telemetry.
    /// ISensing uses pub/sub pattern, so this returns the most recently received value.
    /// </summary>
    /// <param name="inputName">The name of the digital input (must match a DI sensor in configuration)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current state: true = ON, false = OFF, null if not found or no data received yet</returns>
    public Task<bool?> GetDigitalInputAsync(string inputName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetDigitalInputAsync: {InputName}", inputName);
        return Task.FromResult(GetCachedBooleanValue(inputName));
    }

    #endregion

    #region IAnalogInputReadable Implementation

    /// <summary>
    /// Reads the current value of an analog input from the last received telemetry.
    /// ISensing uses pub/sub pattern, so this returns the most recently received value.
    /// </summary>
    /// <param name="inputName">The name of the analog input (must match an AI sensor in configuration)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current value, or null if not found or no data received yet</returns>
    public Task<double?> GetAnalogInputAsync(string inputName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetAnalogInputAsync: {InputName}", inputName);
        return Task.FromResult(GetCachedDoubleValue(inputName));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets a cached boolean value from the last received telemetry.
    /// </summary>
    private bool? GetCachedBooleanValue(string sensorName)
    {
        var sensor = FindSensor(sensorName);
        if (sensor is null)
        {
            _logger.LogWarning("Sensor '{SensorName}' not found in configuration", sensorName);
            return null;
        }

        var telemetry = ReadSensorTelemetryAsync(sensor.ResourceId).GetAwaiter().GetResult();
        if (telemetry.Count == 0)
        {
            _logger.LogDebug("No cached telemetry found for sensor '{SensorName}'", sensorName);
            return null;
        }

        var value = telemetry[0].Value;
        return value switch
        {
            bool b => b,
            int i => i != 0,
            double d => d != 0,
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => Convert.ToBoolean(value)
        };
    }

    /// <summary>
    /// Gets a cached double value from the last received telemetry.
    /// </summary>
    private double? GetCachedDoubleValue(string sensorName)
    {
        var sensor = FindSensor(sensorName);
        if (sensor is null)
        {
            _logger.LogWarning("Sensor '{SensorName}' not found in configuration", sensorName);
            return null;
        }

        var telemetry = ReadSensorTelemetryAsync(sensor.ResourceId).GetAwaiter().GetResult();
        if (telemetry.Count == 0)
        {
            _logger.LogDebug("No cached telemetry found for sensor '{SensorName}'", sensorName);
            return null;
        }

        var value = telemetry[0].Value;
        return Convert.ToDouble(value);
    }

    #endregion
}
