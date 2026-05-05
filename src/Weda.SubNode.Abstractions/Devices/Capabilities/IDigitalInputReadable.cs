namespace Weda.SubNode.Abstractions.Devices.Capabilities;

/// <summary>
/// Interface for devices that support reading digital input states.
/// Devices implementing this interface can report their current DI states
/// via cloud commands (di.get).
/// </summary>
/// <remarks>
/// This interface is used by GetDigitalInputCommandHandler to locate and read
/// devices that support DI read operations. Typical implementations include:
/// - ModbusDevice (via Modbus FC02 Read Discrete Inputs)
/// - ISensingDevice (via MQTT query)
///
/// Digital inputs are typically read-only discrete inputs such as switches,
/// proximity sensors, or status signals.
/// </remarks>
public interface IDigitalInputReadable : IDevice
{
    /// <summary>
    /// Reads the current state of a digital input asynchronously.
    /// </summary>
    /// <param name="inputName">
    /// The name of the digital input to read.
    /// This should match the sensor name configured in devicecfg.json (e.g., "di_0", "di_1").
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The current input state: true = ON/HIGH, false = OFF/LOW.
    /// Returns null if the input cannot be read or does not exist.
    /// </returns>
    /// <example>
    /// <code>
    /// // Read digital input 0 state
    /// var state = await device.GetDigitalInputAsync("di_0");
    /// if (state.HasValue)
    ///     Console.WriteLine($"DI0 is {(state.Value ? "ON" : "OFF")}");
    /// </code>
    /// </example>
    Task<bool?> GetDigitalInputAsync(string inputName, CancellationToken cancellationToken = default);
}