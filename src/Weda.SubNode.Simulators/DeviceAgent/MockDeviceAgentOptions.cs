namespace Weda.SubNode.Simulators.DeviceAgent;

/// <summary>
/// Configuration options for MockDeviceAgent.
/// </summary>
public class MockDeviceAgentOptions
{
    /// <summary>
    /// NATS server URL.
    /// Default: "nats://localhost:4224"
    /// </summary>
    public string NatsUrl { get; set; } = "nats://localhost:4224";

    /// <summary>
    /// Prefix for auto-generated device IDs.
    /// Default: "mock-"
    /// </summary>
    public string DeviceIdPrefix { get; set; } = "mock-";

    /// <summary>
    /// NATS topic prefix for generated topic assignments.
    /// Default: "eco1j.weda"
    /// </summary>
    public string TopicPrefix { get; set; } = "eco1j.weda";
}