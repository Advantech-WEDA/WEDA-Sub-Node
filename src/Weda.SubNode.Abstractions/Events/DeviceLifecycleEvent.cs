namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event raised during device lifecycle transitions.
/// Provides information about lifecycle stage execution and any errors.
/// </summary>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="Stage">The lifecycle stage being executed.</param>
/// <param name="Timestamp">When the event occurred.</param>
public sealed record DeviceLifecycleEvent(
    string DeviceId,
    LifecycleStage Stage,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Gets the phase of execution (Before or After).
    /// </summary>
    public LifecyclePhase Phase { get; init; }

    /// <summary>
    /// Gets the duration of the stage execution (only available in After phase).
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Gets the error message if the stage failed.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Lifecycle stages for device operations.
/// </summary>
public enum LifecycleStage
{
    /// <summary>
    /// Device initialization stage.
    /// </summary>
    Initialize = 0,

    /// <summary>
    /// Device start stage.
    /// </summary>
    Start = 1,

    /// <summary>
    /// Device stop stage.
    /// </summary>
    Stop = 2,

    /// <summary>
    /// Device disposal stage.
    /// </summary>
    Dispose = 3
}

/// <summary>
/// Lifecycle phase indicating before or after stage execution.
/// </summary>
public enum LifecyclePhase
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
