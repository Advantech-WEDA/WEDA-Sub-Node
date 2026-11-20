namespace Weda.SubNode.Abstractions.Devices.Capabilities;

/// <summary>
/// Interface for devices that support reading digital output states.
/// Devices implementing this interface can report their current DO states
/// via cloud commands (do.get).
/// </summary>
/// <remarks>
/// This interface is used by GetDigitalOutputCommandHandler to locate and read
/// devices that support DO read operations. Typical implementations include:
/// - ModbusDevice (via Modbus FC01 Read Coils)
/// - ISensingDevice (via MQTT query)
///
/// Digital outputs are typically coils that can be both read and written.
/// This interface provides real-time reading capability, separate from
/// the write capability in IDigitalOutputControllable.
/// </remarks>
public interface IDigitalOutputReadable : IDevice
{
    /// <summary>
    /// Reads the current state of a digital output asynchronously.
    /// </summary>
    /// <param name="outputName">
    /// The name of the digital output to read.
    /// This should match the sensor name configured in devicecfg.json (e.g., "do_0", "do_1").
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// The current output state: true = ON/HIGH, false = OFF/LOW.
    /// Returns null if the output cannot be read or does not exist.
    /// </returns>
    /// <example>
    /// <code>
    /// // Read digital output 0 state
    /// var state = await device.GetDigitalOutputAsync("do_0");
    /// if (state.HasValue)
    ///     Console.WriteLine($"DO0 is {(state.Value ? "ON" : "OFF")}");
    /// </code>
    /// </example>
    Task<bool?> GetDigitalOutputAsync(string outputName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the names of every digital output this device has: the outputs an empty
    /// <c>do.get</c> (<c>"outputs": []</c>) reads.
    /// </summary>
    /// <remarks>
    /// Each name must be one <see cref="GetDigitalOutputAsync"/> accepts. The default is the
    /// configured sensors whose names start with <c>do</c> (<see cref="DigitalIoNames.WithPrefix"/>).
    /// Override it when the sensor name does not say whether it is an output, as with the
    /// System Agent's GPIO pins, whose direction is only known on the hardware.
    /// </remarks>
    /// <returns>The output names, without duplicates; empty when the device has none.</returns>
    IReadOnlyList<string> GetDigitalOutputNames() =>
        DigitalIoNames.WithPrefix(Configuration, DigitalIoNames.OutputPrefix);
}
