using ErrorOr;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace Weda.SubNode.Core.Telemetry;

/// <summary>
/// Manages telemetry processing pipeline: Validate → Transform → Filter → Send.
/// Provides clear separation of concerns with before/after events for each stage.
/// </summary>
public interface ITelemetryPipeline
{
    /// <summary>
    /// Processes telemetry data through Validate, Transform and Filter stages (no sending).
    /// Invalid measures are filtered out based on sensor schema before processing.
    /// Per-sensor exception isolation ensures one sensor's failure doesn't affect others.
    /// </summary>
    /// <param name="measures">Raw telemetry measures to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Processed measures after validation, transforms and filters, or an error.</returns>
    Task<ErrorOr<List<TelemetryMeasure>>> TransformAndFilterAsync(
        List<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends telemetry data directly to cloud without Transform or Filter stages.
    /// Use this for batch sending data that has already been processed through TransformAndFilterAsync.
    /// </summary>
    /// <param name="measures">Already processed telemetry measures to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success if telemetry was sent, or an error.</returns>
    Task<ErrorOr<Success>> SendAsync(
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

    /// <summary>
    /// Event raised when telemetry values are processed through a transform or filter.
    /// Provides detailed value-level monitoring including input/output values.
    /// </summary>
    event EventHandler<TelemetryValueChangedEvent>? ValueChanged;

    /// <summary>
    /// Gets or sets whether value change tracking is enabled.
    /// When disabled, ValueChanged events are not emitted (better performance).
    /// Default is false.
    /// </summary>
    bool EnableValueChangeTracking { get; set; }
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
    public TimeSpan AverageTransformDuration { get; init; }
    public TimeSpan AverageFilterDuration { get; init; }
    public TimeSpan AverageSendDuration { get; init; }
    public TimeSpan AverageTotalDuration { get; init; }
    public DateTimeOffset? LastProcessedAt { get; init; }
}
