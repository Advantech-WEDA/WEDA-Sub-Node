namespace Weda.SubNode.Abstractions.Devices.Capabilities;

/// <summary>
/// Interface for devices that support reading analog output values.
/// Devices implementing this interface can report their current AO values
/// via cloud commands (ao.get).
/// </summary>
/// <remarks>
/// This interface is used by GetAnalogOutputCommandHandler to locate and read
/// devices that support AO read operations. Typical implementations include:
/// - ModbusDevice (via Modbus FC03 Read Holding Registers)
/// - ISensingDevice (via MQTT query)
///
/// Analog outputs are typically Holding Registers that can be both read and written.
/// This interface provides real-time reading capability, separate from
/// the write capability in IAnalogOutputControllable.
/// </remarks>
public interface IAnalogOutputReadable : IDevice
{
    /// <summary>
    /// Reads the current value of an analog output asynchronously.
    /// </summary>
    /// <param name="outputName">
    /// The name of the analog output to read.
    /// This should match the sensor name configured in devicecfg.json (e.g., "ao_0", "setpoint").
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The current output value as a double.
    /// Returns null if the output cannot be read or does not exist.
    /// </returns>
    /// <example>
    /// <code>
    /// // Read analog output 0 value
    /// var value = await device.GetAnalogOutputAsync("ao_0");
    /// if (value.HasValue)
    ///     Console.WriteLine($"AO0 = {value.Value}");
    /// </code>
    /// </example>
    Task<double?> GetAnalogOutputAsync(string outputName, CancellationToken cancellationToken = default);
}