namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event raised before and after each telemetry pipeline stage.
/// </summary>
public sealed record TelemetryPipelineStageEvent(
    string DeviceId,
    PipelineStage Stage,
    string StageName,
    int InputCount,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Gets the output count (only set for 'After' events).
    /// </summary>
    public int? OutputCount { get; init; }

    /// <summary>
    /// Gets the duration of the stage (only set for 'After' events).
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Gets whether this is a 'Before' or 'After' event.
    /// </summary>
    public StagePhase Phase { get; init; }

    /// <summary>
    /// Gets any error that occurred during the stage.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Pipeline stage types.
/// </summary>
public enum PipelineStage
{
    /// <summary>
    /// Telemetry transformation stage.
    /// </summary>
    Transform = 0,

    /// <summary>
    /// Telemetry filtering stage.
    /// </summary>
    Filter = 1,

    /// <summary>
    /// Telemetry sending stage.
    /// </summary>
    Send = 2
}

/// <summary>
/// Stage execution phase.
/// </summary>
public enum StagePhase
{
    /// <summary>
    /// Before stage execution.
    /// </summary>
    Before = 0,

    /// <summary>
    /// After stage execution.
    /// </summary>
    After = 1
}
