using ErrorOr;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Dsp;

/// <summary>
/// Interface for self-registering DSP filters that can be automatically discovered by the factory.
/// Uses C# 11+ static abstract interface members for compile-time type safety.
/// </summary>
/// <remarks>
/// Implementing this interface allows filters to be automatically discovered and registered
/// without modifying the DspFilterFactory. It also provides parameter validation and runtime
/// update capabilities for configuration-driven filters.
///
/// Example:
/// <code>
/// public class MyFilter : IDspFilter, IConfigurableDspFilter&lt;MyFilter&gt;
/// {
///     public static string TypeName => "myfilter";
///     public static MyFilter Create(Dictionary&lt;string, object&gt; parameters) => new(...);
///     public ErrorOr&lt;Success&gt; ValidateParameters(Dictionary&lt;string, object&gt; parameters) => Result.Success;
///     public void UpdateParameters(Dictionary&lt;string, object&gt; parameters) { }
/// }
/// </code>
/// </remarks>
/// <typeparam name="TSelf">The implementing type itself (CRTP pattern)</typeparam>
public interface IConfigurableDspFilter<TSelf> where TSelf : IDspFilter, IConfigurableDspFilter<TSelf>
{
    // === Static Members (Factory) ===

    /// <summary>
    /// The type name used in configuration (e.g., "kalman", "movingaverage").
    /// Matched case-insensitively against DspFilterConfig.Type.
    /// </summary>
    static abstract string TypeName { get; }

    /// <summary>
    /// Creates an instance of the filter from configuration parameters.
    /// </summary>
    /// <param name="parameters">Configuration parameters from DspFilterConfig.Parameters</param>
    /// <returns>A new instance of the filter</returns>
    static abstract TSelf Create(Dictionary<string, object> parameters);

    // === Instance Members (Parameter Management) ===

    /// <summary>
    /// Validates the parameters before applying them.
    /// </summary>
    /// <param name="parameters">Parameters to validate</param>
    /// <returns>Success or validation error</returns>
    ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters);

    /// <summary>
    /// Updates filter parameters at runtime. State is preserved.
    /// Call ValidateParameters before this method.
    /// </summary>
    /// <param name="parameters">New parameters to apply</param>
    void UpdateParameters(Dictionary<string, object> parameters);
}

/// <summary>
/// DSP filter interface for telemetry data processing pipeline.
/// </summary>
/// <remarks>
/// This interface defines the core filtering behavior. For configuration-driven
/// filters that support parameter validation and runtime updates, also implement
/// <see cref="IConfigurableDspFilter{TSelf}"/>.
/// </remarks>
public interface IDspFilter
{
    /// <summary>
    /// Whether this filter is enabled. When disabled, input passes through unchanged.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Apply DSP filter to telemetry stream
    /// </summary>
    /// <param name="input">Input telemetry measures</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Filtered telemetry measures</returns>
    IAsyncEnumerable<TelemetryMeasure> ApplyAsync(
        IAsyncEnumerable<TelemetryMeasure> input,
        CancellationToken cancellationToken = default);
}


// o         o
// o         o
// o    ->   o   ->  
// o         o
//