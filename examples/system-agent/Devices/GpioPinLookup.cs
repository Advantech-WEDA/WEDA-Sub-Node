using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Devices;

/// <summary>
/// Maps a bare hardware pin name back to the sensor bound to it.
///
/// A resolved GPIO sensor is named after its template — <c>gpio_pinState_UIO_GPIO2</c> —
/// while <c>gpio.list</c> reports the pin as <c>UIO_GPIO2</c>. The built-in di/do command
/// handlers look a device up by sensor name before delegating, so without this lookup the
/// pin name an operator reads off <c>gpio.list</c> is rejected before the device sees it.
/// </summary>
public static class GpioPinLookup
{
    private const string PinIdParameter = "PinId";
    private const string GpioMetricType = "gpio";
    private const string PinStateMetricName = "pinState";

    /// <summary>
    /// The hardware pin a sensor is bound to, or null when it is not bound to one.
    /// Only <c>pinState</c> sensors bind a single pin; <c>isSupported</c> is device-wide.
    /// </summary>
    public static string? BoundPinOf(Sensor sensor)
    {
        if (!string.Equals(Parameter(sensor, "MetricType"), GpioMetricType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Parameter(sensor, "MetricName"), PinStateMetricName, StringComparison.OrdinalIgnoreCase))
            return null;

        var pinId = Parameter(sensor, PinIdParameter);
        return string.IsNullOrWhiteSpace(pinId) ? null : pinId;
    }

    /// <summary>
    /// Finds the sensor bound to <paramref name="pinName"/>.
    /// </summary>
    /// <param name="sensors">The device's configured sensors.</param>
    /// <param name="pinName">A bare hardware pin name, as reported by <c>gpio.list</c>.</param>
    /// <param name="ambiguousWith">
    /// Names of every sensor bound to the pin when more than one is — empty otherwise.
    /// </param>
    /// <returns>
    /// The matching sensor, or null when none is bound to that pin. Two sensors on one pin
    /// is reported as ambiguous and resolves to null: driving the wrong pin is worse than
    /// failing the command.
    /// </returns>
    public static Sensor? FindByPinName(
        IEnumerable<Sensor> sensors,
        string pinName,
        out IReadOnlyList<string> ambiguousWith)
    {
        ambiguousWith = [];

        if (string.IsNullOrWhiteSpace(pinName))
            return null;

        var matches = sensors
            .Where(s => string.Equals(BoundPinOf(s), pinName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count > 1)
        {
            ambiguousWith = matches.Select(m => m.Name).ToList();
            return null;
        }

        return matches.SingleOrDefault();
    }

    /// <summary>
    /// Finds the sensor addressed by <paramref name="name"/>, accepting either the
    /// configured sensor name or the hardware pin it is bound to. The configured name
    /// is matched first so an alias can never shadow a real sensor.
    /// </summary>
    /// <param name="sensors">The device's configured sensors.</param>
    /// <param name="name">A sensor name or a bare hardware pin name.</param>
    /// <param name="ambiguousWith">
    /// Names of every sensor bound to the pin when more than one is — empty otherwise.
    /// </param>
    /// <returns>The matching sensor, or null when unknown or ambiguous.</returns>
    public static Sensor? FindByNameOrPin(
        IEnumerable<Sensor> sensors,
        string name,
        out IReadOnlyList<string> ambiguousWith)
    {
        ambiguousWith = [];

        if (string.IsNullOrWhiteSpace(name))
            return null;

        var sensorList = sensors as IReadOnlyCollection<Sensor> ?? sensors.ToList();

        var byName = sensorList.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        return byName ?? FindByPinName(sensorList, name, out ambiguousWith);
    }

    /// <summary>
    /// Maps a name as addressed by a di/do command onto the pin name the driver expects:
    /// a sensor name yields its <c>PinId</c>, a bare pin name yields itself.
    /// </summary>
    /// <remarks>
    /// A name with no bound sensor passes through unchanged — the driver rejects it if it
    /// is not a real pin. That is only reachable once the command handler's
    /// <c>FindSensor</c> gate has already accepted the name.
    /// </remarks>
    /// <param name="sensors">The device's configured sensors.</param>
    /// <param name="requestedName">The name carried in the command parameters.</param>
    /// <returns>The hardware pin name to hand to the driver.</returns>
    public static string ResolveHardwarePin(IEnumerable<Sensor> sensors, string requestedName)
    {
        var sensor = FindByNameOrPin(sensors, requestedName, out _);
        var pinId = sensor is null ? null : Parameter(sensor, PinIdParameter);

        return string.IsNullOrWhiteSpace(pinId) ? requestedName : pinId;
    }

    private static string? Parameter(Sensor sensor, string key) =>
        sensor.Parameters != null && sensor.Parameters.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;
}
