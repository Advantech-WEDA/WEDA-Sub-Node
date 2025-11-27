using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event raised when telemetry values are processed through a transform or filter stage.
/// This provides detailed value-level monitoring for debugging and observability.
/// </summary>
public sealed record TelemetryValueChangedEvent(
    string DeviceId,
    string ResourceId,
    string StageName,
    ValueChangeStage Stage,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Gets the values before processing (input to the stage).
    /// </summary>
    public IReadOnlyList<TelemetryMeasure> InputValues { get; init; } = [];

    /// <summary>
    /// Gets the values after processing (output from the stage).
    /// </summary>
    public IReadOnlyList<TelemetryMeasure> OutputValues { get; init; } = [];

    /// <summary>
    /// Gets the duration of the stage processing.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Gets any error that occurred during processing.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the index of this stage in the pipeline (0-based).
    /// </summary>
    public int StageIndex { get; init; }

    /// <summary>
    /// Gets whether values were actually changed by this stage.
    /// </summary>
    public bool ValuesChanged => !InputValues.SequenceEqual(OutputValues);

    /// <summary>
    /// Gets the count difference (negative means values were filtered out).
    /// </summary>
    public int CountDelta => OutputValues.Count - InputValues.Count;
}

/// <summary>
/// Type of pipeline stage for value change tracking.
/// </summary>
public enum ValueChangeStage
{
    /// <summary>
    /// Telemetry transformation stage.
    /// </summary>
    Transform = 0,

    /// <summary>
    /// DSP filter stage.
    /// </summary>
    Filter = 1
}
