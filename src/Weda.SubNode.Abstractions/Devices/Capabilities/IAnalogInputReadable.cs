namespace Weda.SubNode.Abstractions.Devices.Capabilities;

/// <summary>
/// Interface for devices that support reading analog input values.
/// Devices implementing this interface can report their current AI values
/// via cloud commands (ai.get).
/// </summary>
/// <remarks>
/// This interface is used by GetAnalogInputCommandHandler to locate and read
/// devices that support AI read operations. Typical implementations include:
/// - ModbusDevice (via Modbus FC04 Read Input Registers)
/// - ISensingDevice (via MQTT query)
///
/// Analog inputs are typically read-only Input Registers such as temperature
/// sensors, pressure sensors, or other measurement devices.
/// </remarks>
public interface IAnalogInputReadable : IDevice
{
    /// <summary>
    /// Reads the current value of an analog input asynchronously.
    /// </summary>
    /// <param name="inputName">
    /// The name of the analog input to read.
    /// This should match the sensor name configured in devicecfg.json (e.g., "ai_0", "temperature").
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The current input value as a double.
    /// Returns null if the input cannot be read or does not exist.
    /// </returns>
    /// <example>
    /// <code>
    /// // Read analog input 0 value
    /// var value = await device.GetAnalogInputAsync("ai_0");
    /// if (value.HasValue)
    ///     Console.WriteLine($"AI0 = {value.Value}");
    /// </code>
    /// </example>
    Task<double?> GetAnalogInputAsync(string inputName, CancellationToken cancellationToken = default);
}