namespace SystemAgentExample.Communication;

/// <summary>
/// Holds discovered system resource names for sensor resolution (Auto-detect mode).
/// </summary>
public record DiscoveredResources(
    IReadOnlyList<string> NetworkInterfaces,
    IReadOnlyList<string> GpioPins,
    IReadOnlyList<string> TemperatureSources);
