using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Protocols.Cfx;

/// <summary>
/// Reads CFX-specific values out of a sensor's protocol parameter bag.
/// </summary>
/// <remarks>
/// A CFX sensor is bound to exactly one CFX message type. Unlike register-based protocols there is
/// no address or width to configure: the binding key is the fully-qualified message name, and the
/// reported value is that message's body.
/// </remarks>
public static class CfxSensorParameters
{
    /// <summary>Parameter key naming the CFX message a sensor is bound to.</summary>
    public const string MessageNameKey = "MessageName";

    /// <summary>
    /// Gets the fully-qualified CFX message name a sensor is bound to.
    /// </summary>
    /// <param name="sensor">The sensor to inspect.</param>
    /// <returns>The configured message name.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the sensor has no <c>MessageName</c> parameter, or it is blank. Configuration is
    /// validated eagerly so a mis-configured sensor fails at startup rather than silently never
    /// reporting.
    /// </exception>
    public static string GetMessageName(this Sensor sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        if (sensor.Parameters is null
            || !sensor.Parameters.TryGetValue(MessageNameKey, out var raw))
        {
            throw new InvalidOperationException(
                $"CFX sensor '{sensor.Name}' is missing the required '{MessageNameKey}' parameter.");
        }

        var messageName = raw?.ToString();
        if (string.IsNullOrWhiteSpace(messageName))
        {
            throw new InvalidOperationException(
                $"CFX sensor '{sensor.Name}' has a blank '{MessageNameKey}' parameter.");
        }

        return messageName;
    }

    /// <summary>
    /// Attempts to read the CFX message name a sensor is bound to, without throwing.
    /// </summary>
    /// <param name="sensor">The sensor to inspect.</param>
    /// <param name="messageName">The configured message name, when present.</param>
    /// <returns><c>true</c> when the sensor carries a usable message name.</returns>
    public static bool TryGetMessageName(this Sensor sensor, out string messageName)
    {
        messageName = string.Empty;

        if (sensor?.Parameters is null
            || !sensor.Parameters.TryGetValue(MessageNameKey, out var raw))
        {
            return false;
        }

        var value = raw?.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        messageName = value;
        return true;
    }
}
