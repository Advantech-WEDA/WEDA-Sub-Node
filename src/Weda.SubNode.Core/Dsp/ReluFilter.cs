using System.Runtime.CompilerServices;
using ErrorOr;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Dsp;

/// <summary>
/// ReLU (Rectified Linear Unit) DSP filter - sets negative values to 0
/// </summary>
public class ReluFilter : IDspFilter
{
    /// <inheritdoc/>
    public bool Enabled { get; set; } = true;

    /// <inheritdoc/>
    public ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters)
    {
        // ReluFilter has no configurable parameters
        return Result.Success;
    }

    /// <inheritdoc/>
    public void UpdateParameters(Dictionary<string, object> parameters)
    {
        // ReluFilter has no configurable parameters
    }

    public async IAsyncEnumerable<TelemetryMeasure> ApplyAsync(
        IAsyncEnumerable<TelemetryMeasure> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var measure in input.WithCancellation(cancellationToken))
        {
            // Pass through if disabled
            if (!Enabled)
            {
                yield return measure;
                continue;
            }

            // Convert Value to double
            if (measure.Value is not double and not int and not float and not long)
            {
                yield return measure; // Pass through non-numeric values
                continue;
            }

            var value = Convert.ToDouble(measure.Value);

            // Apply ReLU: max(0, x)
            yield return value < 0
                ? measure with { Value = 0.0 }
                : measure;
        }
    }
}
