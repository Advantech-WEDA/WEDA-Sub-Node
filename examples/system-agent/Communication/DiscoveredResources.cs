namespace SystemAgentExample.Communication;

/// <summary>
/// Holds discovered system resource names for sensor resolution (Auto-detect mode).
/// </summary>
/// <param name="NetworkInterfaces">Network interface names.</param>
/// <param name="GpioPins">GPIO pin names.</param>
/// <param name="TemperatureSources">Onboard temperature source names, as the hardware driver reports them.</param>
/// <param name="VoltageSources">Onboard voltage rail names (e.g. <c>Vcore</c>, <c>CMOS Battery</c>); empty when omitted.</param>
/// <param name="FanSources">Onboard fan names; empty when omitted.</param>
public record DiscoveredResources(
    IReadOnlyList<string> NetworkInterfaces,
    IReadOnlyList<string> GpioPins,
    IReadOnlyList<string> TemperatureSources,
    IReadOnlyList<string>? VoltageSources = null,
    IReadOnlyList<string>? FanSources = null)
{
    public IReadOnlyList<string> VoltageSources { get; init; } = VoltageSources ?? [];

    public IReadOnlyList<string> FanSources { get; init; } = FanSources ?? [];
}
