namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Interface for sensor control operations
/// Provides methods to control device sensors and outputs
/// </summary>
public interface ISensorControl
{
    /// <summary>
    /// Sets the state of a digital output
    /// </summary>
    /// <param name="outputName">Digital output name (e.g., "do0", "do1")</param>
    /// <param name="state">Output state (true = ON, false = OFF)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> SetDigitalOutputAsync(string outputName, bool state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the value of an analog output
    /// </summary>
    /// <param name="outputName">Analog output name (e.g., "ao0", "ao1")</param>
    /// <param name="value">Output value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> SetAnalogOutputAsync(string outputName, double value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests current device configuration
    /// </summary>
    /// <param name="configIndex">Configuration index (0 = all)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configuration data as dictionary</returns>
    Task<Dictionary<string, object>> GetConfigurationAsync(ushort configIndex = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates device configuration
    /// </summary>
    /// <param name="configIndex">Configuration index</param>
    /// <param name="configData">Configuration data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> SetConfigurationAsync(ushort configIndex, Dictionary<string, object> configData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables a sensor
    /// </summary>
    /// <param name="sensorName">Sensor name (e.g., "ai0", "di1")</param>
    /// <param name="enabled">Enable state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> SetSensorEnabledAsync(string sensorName, bool enabled, CancellationToken cancellationToken = default);
}
