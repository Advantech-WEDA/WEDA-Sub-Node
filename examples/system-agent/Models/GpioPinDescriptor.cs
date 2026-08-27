namespace SystemAgentExample.Models;

/// <summary>
/// Snapshot of a single GPIO pin: hardware name, direction, and current level.
/// </summary>
public record GpioPinDescriptor
{
    public string Name { get; init; } = string.Empty;

    /// <summary>"input", "output", or null when the direction cannot be determined.</summary>
    public string? Direction { get; init; }

    /// <summary>true = HIGH, false = LOW, null = level unavailable.</summary>
    public bool? State { get; init; }

    /// <summary>
    /// Name of the configured sensor bound to this pin (e.g. gpio_pinState_UIO_GPIO2),
    /// which is the name the built-in do.set / do.get / di.get commands address.
    /// Null when no sensor is bound to the pin.
    /// </summary>
    public string? SensorName { get; init; }
}
