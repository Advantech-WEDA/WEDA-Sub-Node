namespace Weda.SubNode.Abstractions.Transforms;

/// <summary>
/// Telemetry transformation context passed alongside measurements through the
/// transform pipeline.
/// </summary>
public class TelemetryTransformContext
{
    /// <summary>
    /// Device identifier.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    // TODO: maybe add sensor name here
    // TODO: maybe add sensor group here

    /// <summary>
    /// Transformation timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional context metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
