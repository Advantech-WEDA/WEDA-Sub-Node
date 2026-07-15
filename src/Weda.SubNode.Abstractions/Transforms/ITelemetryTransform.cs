namespace Weda.SubNode.Abstractions.Transforms;

using Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Telemetry data transformation interface for the data transformation pipeline.
/// Applied after protocol parsing and before DSP filters.
/// </summary>
/// <remarks>
/// This interface defines the core transformation behavior. For configuration-driven
/// transforms that support parameter validation and runtime updates, also implement
/// <see cref="IConfigurableTransform{TSelf, TParameter}"/>.
/// </remarks>
public interface ITelemetryTransform
{
    /// <summary>
    /// Transform name / identifier.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this transform is enabled. When disabled, input passes through unchanged.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Applies transformation to telemetry measurements.
    /// </summary>
    /// <param name="measures">Input list of measurements.</param>
    /// <param name="context">Transformation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transformed list of measurements.</returns>
    Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default);
}
