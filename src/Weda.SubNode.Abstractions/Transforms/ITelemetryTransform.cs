namespace Weda.SubNode.Abstractions.Transforms;

using ErrorOr;
using Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Telemetry data transformation interface for data transformation pipeline
/// Applied after protocol parsing and before DSP filters
/// </summary>
public interface ITelemetryTransform
{
    /// <summary>
    /// Transform name/identifier
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this transform is enabled. When disabled, input passes through unchanged.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Validates the parameters before applying them.
    /// </summary>
    /// <param name="parameters">Parameters to validate</param>
    /// <returns>Success or validation error</returns>
    ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters);

    /// <summary>
    /// Updates transform parameters at runtime. State is preserved.
    /// Call ValidateParameters before this method.
    /// </summary>
    /// <param name="parameters">New parameters to apply</param>
    void UpdateParameters(Dictionary<string, object> parameters);

    /// <summary>
    /// Applies transformation to telemetry measurements
    /// </summary>
    /// <param name="measures">Input list of measurements</param>
    /// <param name="context">Transformation context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transformed list of measurements</returns>
    Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Telemetry transformation context
/// </summary>
public class TelemetryTransformContext
{
    /// <summary>
    /// Device identifier
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    // TODO: maybe add sensor name here
    // TODO: maybe add sensor group here

    /// <summary>
    /// Transformation timestamp
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional context metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
