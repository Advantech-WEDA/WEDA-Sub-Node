namespace Weda.SubNode.Core.Transforms;

using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

/// <summary>
/// Pipeline for chaining multiple telemetry transformations
/// Follows middleware/chain-of-responsibility pattern
/// </summary>
public class TelemetryTransformPipeline
{
    private readonly List<ITelemetryTransform> _transforms = new();

    /// <summary>
    /// Adds a transform to the pipeline
    /// </summary>
    public TelemetryTransformPipeline Add(ITelemetryTransform transform)
    {
        _transforms.Add(transform ?? throw new ArgumentNullException(nameof(transform)));
        return this;
    }

    /// <summary>
    /// Executes all transforms in the pipeline
    /// </summary>
    public async Task<List<TelemetryMeasure>> ExecuteAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        var current = measures;

        foreach (var transform in _transforms)
        {
            current = await transform.TransformAsync(current, context, cancellationToken);
        }

        return current;
    }

    /// <summary>
    /// Creates a new empty pipeline
    /// </summary>
    public static TelemetryTransformPipeline Create() => new();
}
