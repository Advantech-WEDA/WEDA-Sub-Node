using System.Diagnostics;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Core.Devices.Health;

namespace Weda.SubNode.Core.Telemetry;

/// <summary>
/// Thread-safe telemetry pipeline implementation.
/// Processes telemetry through Transform → Filter → Send stages with event notifications.
/// </summary>
public sealed class TelemetryPipeline : ITelemetryPipeline
{
    private string _deviceId;
    private readonly ILogger<TelemetryPipeline> _logger;
    private readonly IWedaCloudService _cloudService;
    private readonly IDeviceHealthMonitor? _healthMonitor;
    private readonly List<ITelemetryTransform> _transforms = new();
    private readonly List<IDspFilter> _filters = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Statistics tracking
    private long _totalProcessed;
    private long _successfullySent;
    private long _failedToSend;
    private long _filteredOut;
    private readonly List<TimeSpan> _transformDurations = new();
    private readonly List<TimeSpan> _filterDurations = new();
    private readonly List<TimeSpan> _sendDurations = new();
    private readonly List<TimeSpan> _totalDurations = new();
    private DateTimeOffset? _lastProcessedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelemetryPipeline"/> class.
    /// </summary>
    public TelemetryPipeline(
        string deviceId,
        IWedaCloudService cloudService,
        ILogger<TelemetryPipeline> logger,
        IDeviceHealthMonitor? healthMonitor = null)
    {
        _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthMonitor = healthMonitor;
    }

    /// <inheritdoc/>
    public event EventHandler<TelemetryPipelineStageEvent>? StageExecuting;

    /// <summary>
    /// Updates the device ID. Should be called after device registration.
    /// </summary>
    public void SetDeviceId(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        _deviceId = deviceId;
        _logger.LogDebug("TelemetryPipeline device ID updated to: {DeviceId}", _deviceId);
    }

    /// <inheritdoc/>
    public async Task<ErrorOr<Success>> ProcessAsync(
        List<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default)
    {
        if (measures == null || measures.Count == 0)
        {
            return Result.Success;
        }

        var totalStopwatch = Stopwatch.StartNew();
        Interlocked.Increment(ref _totalProcessed);

        try
        {
            // Stage 1: Transform
            var transformResult = await ExecuteTransformStageAsync(measures, cancellationToken);
            if (transformResult.IsError)
            {
                Interlocked.Increment(ref _failedToSend);
                return transformResult.Errors;
            }

            // Stage 2: Filter
            var filterResult = await ExecuteFilterStageAsync(transformResult.Value, cancellationToken);
            if (filterResult.IsError)
            {
                Interlocked.Increment(ref _failedToSend);
                return filterResult.Errors;
            }

            // Check if all measures were filtered out
            if (filterResult.Value.Count == 0)
            {
                _logger.LogDebug(
                    "All {Count} telemetry measures were filtered out for device {DeviceId}",
                    measures.Count, _deviceId);
                Interlocked.Add(ref _filteredOut, measures.Count);
                totalStopwatch.Stop();
                RecordDuration(_totalDurations, totalStopwatch.Elapsed);
                _lastProcessedAt = DateTimeOffset.UtcNow;
                return Result.Success;
            }

            // Stage 3: Send
            var sendResult = await ExecuteSendStageAsync(filterResult.Value, cancellationToken);
            if (sendResult.IsError)
            {
                Interlocked.Increment(ref _failedToSend);
                return sendResult.Errors;
            }

            Interlocked.Increment(ref _successfullySent);
            totalStopwatch.Stop();
            RecordDuration(_totalDurations, totalStopwatch.Elapsed);
            _lastProcessedAt = DateTimeOffset.UtcNow;

            // Record success in health monitor
            _healthMonitor?.RecordSuccess("TelemetryPipeline");

            _logger.LogDebug(
                "Telemetry pipeline completed for device {DeviceId}: {InputCount} → {OutputCount} measures in {Duration}ms",
                _deviceId, measures.Count, filterResult.Value.Count, totalStopwatch.ElapsedMilliseconds);

            return Result.Success;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedToSend);

            // Record failure in health monitor
            _healthMonitor?.RecordFailure("TelemetryPipeline", ex);

            _logger.LogError(ex, "Telemetry pipeline failed for device {DeviceId}", _deviceId);
            return Error.Failure(
                code: "TelemetryPipeline.Failed",
                description: $"Pipeline failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void AddTransform(ITelemetryTransform transform)
    {
        if (transform == null) throw new ArgumentNullException(nameof(transform));

        _lock.Wait();
        try
        {
            _transforms.Add(transform);
            _logger.LogInformation(
                "Added transform '{TransformName}' to pipeline for device {DeviceId}",
                transform.Name, _deviceId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public void AddFilter(IDspFilter filter)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        _lock.Wait();
        try
        {
            _filters.Add(filter);
            _logger.LogInformation(
                "Added filter to pipeline for device {DeviceId}",
                _deviceId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public void ClearStages()
    {
        _lock.Wait();
        try
        {
            _transforms.Clear();
            _filters.Clear();
            _logger.LogInformation("Cleared all pipeline stages for device {DeviceId}", _deviceId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public PipelineStatistics GetStatistics()
    {
        return new PipelineStatistics
        {
            DeviceId = _deviceId,
            TotalProcessed = (int)Interlocked.Read(ref _totalProcessed),
            SuccessfullySent = (int)Interlocked.Read(ref _successfullySent),
            FailedToSend = (int)Interlocked.Read(ref _failedToSend),
            FilteredOut = (int)Interlocked.Read(ref _filteredOut),
            AverageTransformDuration = CalculateAverage(_transformDurations),
            AverageFilterDuration = CalculateAverage(_filterDurations),
            AverageSendDuration = CalculateAverage(_sendDurations),
            AverageTotalDuration = CalculateAverage(_totalDurations),
            LastProcessedAt = _lastProcessedAt
        };
    }

    private async Task<ErrorOr<List<TelemetryMeasure>>> ExecuteTransformStageAsync(
        List<TelemetryMeasure> measures,
        CancellationToken cancellationToken)
    {
        if (_transforms.Count == 0)
        {
            return measures;
        }

        var stopwatch = Stopwatch.StartNew();
        EmitStageEvent(PipelineStage.Transform, "Transforms", measures.Count, StagePhase.Before);

        var current = measures;
        string? errorMessage = null;

        try
        {
            foreach (var transform in _transforms)
            {
                _logger.LogTrace(
                    "Executing transform '{TransformName}' for device {DeviceId}",
                    transform.Name, _deviceId);

                // Create transformation context
                var context = new TelemetryTransformContext
                {
                    DeviceId = _deviceId,
                    Timestamp = DateTimeOffset.UtcNow
                };

                // Call transform without ErrorOr
                var result = await transform.TransformAsync(current, context, cancellationToken);

                current = result;

                if (current.Count == 0)
                {
                    _logger.LogWarning(
                        "Transform '{TransformName}' returned empty result for device {DeviceId}",
                        transform.Name, _deviceId);
                    break;
                }
            }

            stopwatch.Stop();
            RecordDuration(_transformDurations, stopwatch.Elapsed);
            EmitStageEvent(PipelineStage.Transform, "Transforms", measures.Count, StagePhase.After,
                current.Count, stopwatch.Elapsed);

            return current;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            errorMessage = ex.Message;
            EmitStageEvent(PipelineStage.Transform, "Transforms", measures.Count, StagePhase.After,
                null, stopwatch.Elapsed, errorMessage);

            _logger.LogError(ex,
                "Transform stage failed for device {DeviceId}",
                _deviceId);

            return Error.Failure(
                code: "TelemetryPipeline.TransformFailed",
                description: $"Transform stage failed: {ex.Message}");
        }
    }

    private async Task<ErrorOr<List<TelemetryMeasure>>> ExecuteFilterStageAsync(
        List<TelemetryMeasure> measures,
        CancellationToken cancellationToken)
    {
        if (_filters.Count == 0)
        {
            return measures;
        }

        var stopwatch = Stopwatch.StartNew();
        EmitStageEvent(PipelineStage.Filter, "Filters", measures.Count, StagePhase.Before);

        string? errorMessage = null;

        try
        {
            // Convert List to IAsyncEnumerable
            IAsyncEnumerable<TelemetryMeasure> current = ToAsyncEnumerable(measures);

            foreach (var filter in _filters)
            {
                _logger.LogTrace(
                    "Executing filter for device {DeviceId}",
                    _deviceId);

                // Call filter with IAsyncEnumerable
                current = filter.ApplyAsync(current, cancellationToken);
            }

            // Convert result back to List
            var result = await ToListAsync(current, cancellationToken);

            stopwatch.Stop();
            RecordDuration(_filterDurations, stopwatch.Elapsed);

            if (result.Count == 0)
            {
                _logger.LogDebug(
                    "Filters filtered out all measures for device {DeviceId}",
                    _deviceId);
            }

            EmitStageEvent(PipelineStage.Filter, "Filters", measures.Count, StagePhase.After,
                result.Count, stopwatch.Elapsed);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            errorMessage = ex.Message;
            EmitStageEvent(PipelineStage.Filter, "Filters", measures.Count, StagePhase.After,
                null, stopwatch.Elapsed, errorMessage);

            _logger.LogError(ex,
                "Filter stage failed for device {DeviceId}",
                _deviceId);

            return Error.Failure(
                code: "TelemetryPipeline.FilterFailed",
                description: $"Filter stage failed: {ex.Message}");
        }
    }

    private async Task<ErrorOr<Success>> ExecuteSendStageAsync(
        List<TelemetryMeasure> measures,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        EmitStageEvent(PipelineStage.Send, "CloudSend", measures.Count, StagePhase.Before);

        string? errorMessage = null;

        try
        {
            var telemetryData = new TelemetryData
            {
                Measures = measures
            };

            var success = await _cloudService.SendTelemetryAsync(_deviceId, telemetryData, cancellationToken);

            stopwatch.Stop();
            RecordDuration(_sendDurations, stopwatch.Elapsed);

            // Record cloud send duration in health monitor
            _healthMonitor?.RecordCloudSendDuration(stopwatch.Elapsed);

            if (!success)
            {
                errorMessage = "Cloud service returned false";
                EmitStageEvent(PipelineStage.Send, "CloudSend", measures.Count, StagePhase.After,
                    null, stopwatch.Elapsed, errorMessage);
                return Error.Failure(
                    code: "TelemetryPipeline.SendFailed",
                    description: "Failed to send telemetry to cloud");
            }

            EmitStageEvent(PipelineStage.Send, "CloudSend", measures.Count, StagePhase.After,
                measures.Count, stopwatch.Elapsed);

            return Result.Success;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            errorMessage = ex.Message;
            EmitStageEvent(PipelineStage.Send, "CloudSend", measures.Count, StagePhase.After,
                null, stopwatch.Elapsed, errorMessage);
            throw;
        }
    }

    private void EmitStageEvent(
        PipelineStage stage,
        string stageName,
        int inputCount,
        StagePhase phase,
        int? outputCount = null,
        TimeSpan? duration = null,
        string? error = null)
    {
        StageExecuting?.Invoke(this, new TelemetryPipelineStageEvent(
            DeviceId: _deviceId,
            Stage: stage,
            StageName: stageName,
            InputCount: inputCount,
            Timestamp: DateTimeOffset.UtcNow)
        {
            Phase = phase,
            OutputCount = outputCount,
            Duration = duration,
            Error = error
        });
    }

    private void RecordDuration(List<TimeSpan> durations, TimeSpan duration)
    {
        _lock.Wait();
        try
        {
            durations.Add(duration);
            // Keep only last 100 samples
            if (durations.Count > 100)
            {
                durations.RemoveAt(0);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private TimeSpan CalculateAverage(List<TimeSpan> durations)
    {
        if (durations.Count == 0)
        {
            return TimeSpan.Zero;
        }

        var avgMs = durations.Average(d => d.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(avgMs);
    }

    private static IAsyncEnumerable<TelemetryMeasure> ToAsyncEnumerable(
        List<TelemetryMeasure> measures)
    {
        return ToAsyncEnumerableCore(measures);

        static async IAsyncEnumerable<TelemetryMeasure> ToAsyncEnumerableCore(
            List<TelemetryMeasure> measures)
        {
            foreach (var measure in measures)
            {
                yield return measure;
            }
            await Task.CompletedTask; // Satisfy async requirement
        }
    }

    private static async Task<List<TelemetryMeasure>> ToListAsync(
        IAsyncEnumerable<TelemetryMeasure> asyncEnumerable,
        CancellationToken cancellationToken = default)
    {
        var result = new List<TelemetryMeasure>();
        await foreach (var item in asyncEnumerable.WithCancellation(cancellationToken))
        {
            result.Add(item);
        }
        return result;
    }
}
