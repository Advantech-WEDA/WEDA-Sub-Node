namespace Weda.SubNode.Abstractions.Devices.Capabilities;

/// <summary>
/// Interface for devices that support digital output control.
/// Devices implementing this interface can have their digital outputs (DO) controlled
/// via cloud commands.
/// </summary>
/// <remarks>
/// This interface is used by SetDigitalOutputCommandHandler to locate and control
/// devices that support DO operations. Typical implementations include:
/// - TcpModbusDevice (via Modbus FC05/FC15)
/// - ISensingDevice (via ISensing protocol)
/// </remarks>
public interface IDigitalOutputControllable : IDevice
{
    /// <summary>
    /// Sets the state of a digital output asynchronously.
    /// </summary>
    /// <param name="outputName">
    /// The name of the digital output to control.
    /// This should match the sensor name configured in devicecfg.json (e.g., "do_0", "do_1").
    /// </param>
    /// <param name="state">
    /// The desired output state: true = ON/HIGH, false = OFF/LOW
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the operation succeeded, false otherwise</returns>
    /// <example>
    /// <code>
    /// // Turn on digital output 0
    /// var success = await device.SetDigitalOutputAsync("do_0", true);
    ///
    /// // Turn off digital output 1
    /// var success = await device.SetDigitalOutputAsync("do_1", false);
    /// </code>
    /// </example>
    Task<bool> SetDigitalOutputAsync(string outputName, bool state, CancellationToken cancellationToken = default);
}
