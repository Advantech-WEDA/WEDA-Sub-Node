namespace Weda.SubNode.Abstractions.Transforms;

using ErrorOr;
using Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Interface for self-registering transforms that can be automatically discovered by the factory.
/// Uses C# 11+ static abstract interface members for compile-time type safety.
/// </summary>
/// <remarks>
/// Implementing this interface allows transforms to be automatically discovered and registered
/// without modifying the TransformFactory. It also provides parameter validation and runtime
/// update capabilities for configuration-driven transforms.
///
/// Example:
/// <code>
/// public class MyTransform : ITelemetryTransform, IConfigurableTransform&lt;MyTransform&gt;
/// {
///     public static string TypeName => "mytransform";
///     public static MyTransform Create(Dictionary&lt;string, object&gt; parameters) => new(...);
///     public ErrorOr&lt;Success&gt; ValidateParameters(Dictionary&lt;string, object&gt; parameters) => Result.Success;
///     public void UpdateParameters(Dictionary&lt;string, object&gt; parameters) { }
/// }
/// </code>
/// </remarks>
/// <typeparam name="TSelf">The implementing type itself (CRTP pattern)</typeparam>
public interface IConfigurableTransform<TSelf> where TSelf : ITelemetryTransform, IConfigurableTransform<TSelf>
{
    // === Static Members (Factory) ===

    /// <summary>
    /// The type name used in configuration (e.g., "unitconversion", "calibration").
    /// Matched case-insensitively against TransformConfig.Type.
    /// </summary>
    static abstract string TypeName { get; }

    /// <summary>
    /// Creates an instance of the transform from configuration parameters.
    /// </summary>
    /// <param name="parameters">Configuration parameters from TransformConfig.Parameters</param>
    /// <returns>A new instance of the transform</returns>
    static abstract TSelf Create(Dictionary<string, object> parameters);

    // === Instance Members (Parameter Management) ===

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
}

/// <summary>
/// Telemetry data transformation interface for data transformation pipeline.
/// Applied after protocol parsing and before DSP filters.
/// </summary>
/// <remarks>
/// This interface defines the core transformation behavior. For configuration-driven
/// transforms that support parameter validation and runtime updates, also implement
/// <see cref="IConfigurableTransform{TSelf}"/>.
/// </remarks>
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
