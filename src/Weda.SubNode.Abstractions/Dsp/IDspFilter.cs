using ErrorOr;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Dsp;

/// <summary>
/// DSP filter interface for telemetry data processing pipeline
/// </summary>
public interface IDspFilter
{
    /// <summary>
    /// Whether this filter is enabled. When disabled, input passes through unchanged.
    /// </summary>
    bool Enabled { get; set; }

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