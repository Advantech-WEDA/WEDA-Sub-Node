using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Dsp;

/// <summary>
/// DSP filter interface for telemetry data processing pipeline
/// </summary>
public interface IDspFilter
{
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