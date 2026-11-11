using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Devices.Capabilities;

/// <summary>
/// The default rule for which sensors are a device's digital inputs and outputs.
/// </summary>
/// <remarks>
/// An empty <c>di.get</c> / <c>do.get</c> reads every input / output the device reports through
/// <see cref="IDigitalInputReadable.GetDigitalInputNames"/> and
/// <see cref="IDigitalOutputReadable.GetDigitalOutputNames"/>. Devices whose sensor names carry
/// no direction, such as the System Agent's <c>gpio_pinState_*</c> GPIO sensors, override those
/// members; every other device falls back to this naming rule (<c>di_0</c>, <c>do_1</c>, …).
/// </remarks>
public static class DigitalIoNames
{
    /// <summary>Name prefix of a digital input sensor under the default rule.</summary>
    public const string InputPrefix = "di";

    /// <summary>Name prefix of a digital output sensor under the default rule.</summary>
    public const string OutputPrefix = "do";

    /// <summary>
    /// Returns the configured sensor names that start with <paramref name="prefix"/>,
    /// case-insensitively, in configuration order and without duplicates.
    /// </summary>
    /// <param name="configuration">The device configuration whose sensors are searched.</param>
    /// <param name="prefix"><see cref="InputPrefix"/> or <see cref="OutputPrefix"/>.</param>
    /// <returns>The matching sensor names; empty when there are none.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static IReadOnlyList<string> WithPrefix(DeviceConfiguration configuration, string prefix)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return WithPrefix(configuration.Sensors, prefix);
    }

    /// <summary>
    /// Returns the names in <paramref name="sensors"/> that start with <paramref name="prefix"/>,
    /// case-insensitively, in the given order and without duplicates.
    /// </summary>
    /// <param name="sensors">The sensors to search.</param>
    /// <param name="prefix"><see cref="InputPrefix"/> or <see cref="OutputPrefix"/>.</param>
    /// <returns>The matching sensor names; empty when there are none.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static IReadOnlyList<string> WithPrefix(IEnumerable<Sensor> sensors, string prefix)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentException.ThrowIfNullOrEmpty(prefix);

        return sensors
            .Select(s => s.Name)
            .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
