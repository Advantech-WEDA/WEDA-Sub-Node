namespace Weda.SubNode.Abstractions.Devices.Capabilities;

/// <summary>
/// Interface for devices that support analog output control.
/// Devices implementing this interface can have their analog outputs (AO) controlled
/// via cloud commands.
/// </summary>
/// <remarks>
/// This interface is used by SetAnalogOutputCommandHandler to locate and control
/// devices that support AO operations. Typical implementations include:
/// - TcpModbusDevice (via Modbus FC06/FC16)
/// - RtuModbusDevice (via Modbus FC06/FC16)
///
/// Analog outputs are typically Holding Registers that can be written to set
/// values like setpoints, thresholds, or control parameters.
/// </remarks>
public interface IAnalogOutputControllable : IDevice
{
    /// <summary>
    /// Sets the value of an analog output asynchronously.
    /// </summary>
    /// <param name="outputName">
    /// The name of the analog output to control.
    /// This should match the sensor name configured in devicecfg.json (e.g., "ao_0", "setpoint").
    /// </param>
    /// <param name="value">
    /// The desired output value. The interpretation depends on the register configuration:
    /// - For single registers (UInt16): value is written directly
    /// - For multi-register types (Int32, Float, etc.): value is converted according to ByteOrder
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the operation succeeded, false otherwise</returns>
    /// <example>
    /// <code>
    /// // Set analog output to integer value
    /// var success = await device.SetAnalogOutputAsync("ao_0", 1000);
    ///
    /// // Set analog output to floating point value
    /// var success = await device.SetAnalogOutputAsync("setpoint", 25.5);
    /// </code>
    /// </example>
    Task<bool> SetAnalogOutputAsync(string outputName, object value, CancellationToken cancellationToken = default);
}
