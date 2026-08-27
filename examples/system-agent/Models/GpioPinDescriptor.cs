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
}
