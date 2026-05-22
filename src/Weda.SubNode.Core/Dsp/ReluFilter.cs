using System.Runtime.CompilerServices;

using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Dsp;

/// <summary>
/// Empty parameters class for <see cref="ReluFilter"/>. ReLU has no
/// configurable parameters but the framework requires a TParameter type.
/// </summary>
public class ReluParameters
{
}

/// <summary>
/// ReLU (Rectified Linear Unit) DSP filter — sets negative values to 0.
/// </summary>
public class ReluFilter
    : IDspFilter,
      IConfigurableDspFilter<ReluFilter, ReluParameters>
{
    public static string TypeName => "relu";

    public static string? Description =>
        "Rectified Linear Unit: clamps negative numeric measurements to zero.";

    public static ReluFilter Create(ReluParameters parameters) => new();

    public bool Enabled { get; set; } = true;

    public void UpdateParameters(ReluParameters parameters)
    {
        // ReluFilter has no configurable state.
    }

    public async IAsyncEnumerable<TelemetryMeasure> ApplyAsync(
        IAsyncEnumerable<TelemetryMeasure> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var measure in input.WithCancellation(cancellationToken))
        {
            if (!Enabled)
            {
                yield return measure;
                continue;
            }

            if (measure.Value is not double and not int and not float and not long)
            {
                yield return measure;
                continue;
            }

            var value = Convert.ToDouble(measure.Value);

            yield return value < 0
                ? measure with { Value = 0.0 }
                : measure;
        }
    }
}
