using ErrorOr;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace Weda.SubNode.Core.Telemetry;

/// <summary>
/// Manages telemetry processing pipeline: Transform → Filter → Send.
/// Provides clear separation of concerns with before/after events for each stage.
/// </summary>
public interface ITelemetryPipeline
{
    /// <summary>
    /// Processes telemetry data through the pipeline.
    /// </summary>
    /// <param name="measures">Raw telemetry measures to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success if telemetry was sent, or an error.</returns>
    Task<ErrorOr<Success>> ProcessAsync(
        List<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a transform to the pipeline.
    /// </summary>
    void AddTransform(ITelemetryTransform transform);

    /// <summary>
    /// Adds a filter to the pipeline.
    /// </summary>
    void AddFilter(IDspFilter filter);

    /// <summary>
    /// Removes all transforms and filters.
    /// </summary>
    void ClearStages();

    /// <summary>
    /// Gets pipeline statistics.
    /// </summary>
    PipelineStatistics GetStatistics();

    /// <summary>
    /// Updates the device ID. Should be called after device registration.
    /// </summary>
    void SetDeviceId(string deviceId);

    /// <summary>
    /// Event raised before and after each pipeline stage.
    /// </summary>
    event EventHandler<TelemetryPipelineStageEvent>? StageExecuting;
}

/// <summary>
/// Pipeline execution statistics.
/// </summary>
public sealed record PipelineStatistics
{
    public required string DeviceId { get; init; }
    public required int TotalProcessed { get; init; }
    public required int SuccessfullySent { get; init; }
    public required int FailedToSend { get; init; }
    public required int FilteredOut { get; init; }
    public TimeSpan AverageTransformDuration { get; init; }
    public TimeSpan AverageFilterDuration { get; init; }
    public TimeSpan AverageSendDuration { get; init; }
    public TimeSpan AverageTotalDuration { get; init; }
    public DateTimeOffset? LastProcessedAt { get; init; }
}
