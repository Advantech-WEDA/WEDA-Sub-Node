namespace SystemAgentExample.Communication;

/// <summary>
/// Holds discovered system resource names for sensor auto-expansion.
/// </summary>
public record DiscoveredResources(
    IReadOnlyList<string> NetworkInterfaces,
    IReadOnlyList<string> GpioPins,
    IReadOnlyList<string> TemperatureSources);
