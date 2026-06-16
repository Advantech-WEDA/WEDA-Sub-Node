using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Dsp;

/// <summary>
/// DSP filter interface for the telemetry data processing pipeline.
/// </summary>
/// <remarks>
/// This interface defines the core filtering behavior. For configuration-driven
/// filters that support parameter validation and runtime updates, also implement
/// <see cref="IConfigurableDspFilter{TSelf, TParameter}"/>.
/// </remarks>
public interface IDspFilter
{
    /// <summary>
    /// Whether this filter is enabled. When disabled, input passes through unchanged.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Apply DSP filter to telemetry stream.
    /// </summary>
    /// <param name="input">Input telemetry measures.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filtered telemetry measures.</returns>
    IAsyncEnumerable<TelemetryMeasure> ApplyAsync(
        IAsyncEnumerable<TelemetryMeasure> input,
        CancellationToken cancellationToken = default);
}
