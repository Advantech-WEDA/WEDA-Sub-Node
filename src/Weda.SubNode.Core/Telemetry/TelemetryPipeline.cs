using System.Diagnostics;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Telemetry.Validation;
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Core.Devices.Health;
using Weda.SubNode.Core.Dsp;
using Weda.SubNode.Core.Telemetry.Validation;
using Weda.SubNode.Core.Transforms;

namespace Weda.SubNode.Core.Telemetry;

/// <summary>
/// Thread-safe telemetry pipeline implementation.
/// Processes telemetry through Transform → Filter → Send stages with event notifications.
/// </summary>
public sealed class TelemetryPipeline : ITelemetryPipeline
{
    private string _deviceId;
    private readonly DeviceConfiguration? _configuration;
    private readonly ILogger<TelemetryPipeline> _logger;
    private readonly IWedaCloudService _cloudService;
    private readonly IDeviceHealthMonitor? _healthMonitor;
    private readonly ITelemetryValidator? _validator;
    private readonly List<ITelemetryTransform> _transforms = new();
    private readonly List<IDspFilter> _filters = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Cache for sensor-level config-based DSP filters (to preserve state across invocations)
    // DSP filters like MovingAverage, Kalman are stateful and need to persist buffer state
    private readonly Dictionary<string, List<IDspFilter>> _sensorFilterCache = new();

    // Statistics tracking
    private long _totalProcessed;
    private long _successfullySent;
    private long _failedToSend;
    private readonly List<TimeSpan> _transformDurations = new();
    private readonly List<TimeSpan> _filterDurations = new();
    private readonly List<TimeSpan> _sendDurations = new();
    private readonly List<TimeSpan> _totalDurations = new();
    private long _validationSuccessCount;
    private long _validationFailureCount;
    private DateTimeOffset _lastMetricsLogTime = DateTimeOffset.UtcNow;
    private static readonly TimeSpan MetricsLogInterval = TimeSpan.FromSeconds(120);
    private DateTimeOffset? _lastProcessedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelemetryPipeline"/> class.
    /// </summary>
    public TelemetryPipeline(
        string deviceId,
        DeviceConfiguration? configuration,
        IWedaCloudService cloudService,
        ILogger<TelemetryPipeline> logger,
        IDeviceHealthMonitor? healthMonitor = null,
        ITelemetryValidator? validator = null)
    {
        _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _configuration = configuration;
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthMonitor = healthMonitor;
        _validator = validator;
    }

    /// <inheritdoc/>
    public event EventHandler<TelemetryPipelineStageEvent>? StageExecuting;

    /// <inheritdoc/>
    public event EventHandler<TelemetryValueChangedEvent>? ValueChanged;

    /// <inheritdoc/>
    public bool EnableValueChangeTracking { get; set; }

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
            // Stage 0: Validate
            bool[] valid = ValidateMeasures(measures);
            var validMeasures = new List<TelemetryMeasure>();
            var invalidMeasures = new List<TelemetryMeasure>();
            for (int i = 0; i < measures.Count; i++)
            {
                if (valid[i])
                    validMeasures.Add(measures[i]);
                else
                    invalidMeasures.Add(measures[i]);
            }

            // Stage 1: Transform
            var transformResult = await ExecuteTransformStageAsync(validMeasures, cancellationToken);
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

            // Stage 3: Send
            var allMeasures = new List<TelemetryMeasure>();
            for (int i = 0, j = 0, k = 0; i < measures.Count; i++)
            {
                if (valid[i])
                    allMeasures.Add(filterResult.Value[j++]);
                else
                    allMeasures.Add(invalidMeasures[k++]);

            }
            var sendResult = await ExecuteSendStageAsync(allMeasures, cancellationToken);
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

            LogValidationMetricsIfNeeded();

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

    private bool[] ValidateMeasures(List<TelemetryMeasure> measures)
    {
        int n = measures.Count;
        bool[] valid = new bool[n];

        if (_validator == null || _configuration == null)
        {
            Array.Fill(valid, true);
            return valid;
        }

        var sensorMap = _configuration.Sensors.ToDictionary(s => s.ResourceId);
        for (int i = 0; i < n; i++)
        {
            var measure = measures[i];
            if (!sensorMap.TryGetValue(measure.ResourceId, out var sensor))
            {
                valid[i] = true;  // sensor not found, pass through
                continue;   
            }

            var result = _validator.Validate(measure.Value, sensor.SensorInfo.Schema);
            valid[i] = !result.IsError;

            if (result.IsError)
            {
                Interlocked.Increment(ref _validationFailureCount);
                _logger.LogWarning("Validation failed, skipping tranform/filter: Sensor={ResourceId}, Schema={Schema}, Error={Error}",
                    measure.ResourceId, sensor.SensorInfo.Schema, result.FirstError.Description);
            }
            else
            {
                Interlocked.Increment(ref _validationSuccessCount);
            }
        }

        return valid;
    }

    private void LogValidationMetricsIfNeeded()
    {
        var now = DateTime.UtcNow;
        if (now - _lastMetricsLogTime < MetricsLogInterval)
            return;

        _lastMetricsLogTime = now;

        var success = Interlocked.Read(ref _validationSuccessCount);
        var failure = Interlocked.Read(ref _validationFailureCount);
        var total = success + failure;

        if (total == 0) return;

        var invalidRate = (double)failure / total * 100;
        _logger.LogInformation(
            "Telemetry validation statistics for {DeviceId}: Total={Total}, Valid={Valid}, Invalid={Invalid}, InvalidRate:{InvalidRate:F2}%",
            _deviceId, total, success, failure, invalidRate);        
    }

    /// <inheritdoc/>
    public async Task<ErrorOr<List<TelemetryMeasure>>> TransformAndFilterAsync(
        List<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default)
    {
        if (measures == null || measures.Count == 0)
        {
            return new List<TelemetryMeasure>();
        }

        try
        {
            // Stage 1: Transform
            var transformResult = await ExecuteTransformStageAsync(measures, cancellationToken);
            if (transformResult.IsError)
            {
                return transformResult.Errors;
            }

            // Stage 2: Filter
            var filterResult = await ExecuteFilterStageAsync(transformResult.Value, cancellationToken);
            if (filterResult.IsError)
            {
                return filterResult.Errors;
            }

            _logger.LogTrace(
                "TransformAndFilter completed for device {DeviceId}: {InputCount} → {OutputCount} measures",
                _deviceId, measures.Count, filterResult.Value.Count);

            return filterResult.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TransformAndFilter failed for device {DeviceId}", _deviceId);
            return Error.Failure(
                code: "TelemetryPipeline.TransformAndFilterFailed",
                description: $"TransformAndFilter failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<ErrorOr<Success>> SendAsync(
        List<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default)
    {
        if (measures == null || measures.Count == 0)
        {
            return Result.Success;
        }

        try
        {
            var sendResult = await ExecuteSendStageAsync(measures, cancellationToken);
            if (sendResult.IsError)
            {
                return sendResult.Errors;
            }

            _logger.LogTrace(
                "SendAsync completed for device {DeviceId}: {Count} measures sent",
                _deviceId, measures.Count);

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendAsync failed for device {DeviceId}", _deviceId);
            return Error.Failure(
                code: "TelemetryPipeline.SendFailed",
                description: $"SendAsync failed: {ex.Message}");
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
            _sensorFilterCache.Clear();
            _logger.LogInformation("Cleared all pipeline stages for device {DeviceId}", _deviceId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Clears cached DSP filters for a specific sensor.
    /// Call this when sensor configuration changes to force re-creation of filters.
    /// </summary>
    /// <param name="resourceId">The sensor resource ID</param>
    public void ClearSensorFilterCache(string resourceId)
    {
        _lock.Wait();
        try
        {
            if (_sensorFilterCache.Remove(resourceId))
            {
                _logger.LogDebug("Cleared DSP filter cache for sensor {ResourceId}", resourceId);
            }
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
        var stopwatch = Stopwatch.StartNew();
        EmitStageEvent(PipelineStage.Transform, "Transforms", measures.Count, StagePhase.Before);

        string? errorMessage = null;

        try
        {
            var result = new List<TelemetryMeasure>();

            // Group measures by ResourceId for sensor-level processing
            var groupedMeasures = measures.GroupBy(m => m.ResourceId).ToList();

            foreach (var group in groupedMeasures)
            {
                var resourceId = group.Key;
                var resourceMeasures = group.ToList();

                // Find sensor configuration
                var sensor = _configuration?.Sensors.FirstOrDefault(s => s.ResourceId == resourceId);

                List<ITelemetryTransform> transformsToApply = new();

                // Merge sensor-level runtime and config-based transforms
                if (sensor?.Report != null)
                {
                    // Priority 1: Add sensor-level runtime transforms (from code) - thread-safe
                    if (sensor.Report.RuntimeTransforms.Count > 0)
                    {
                        transformsToApply.AddRange(sensor.Report.RuntimeTransforms);
                        _logger.LogTrace(
                            "Added {Count} sensor-level runtime transforms for ResourceId {ResourceId}",
                            sensor.Report.RuntimeTransforms.Count, resourceId);
                    }

                    // Priority 2: Add sensor-level config transforms (from appsettings.json or cloud)
                    if (sensor.Report.TransformPipeline.Count > 0)
                    {
                        var configBasedTransforms = TransformFactory.CreateFromConfigs(sensor.Report.TransformPipeline);
                        transformsToApply.AddRange(configBasedTransforms);
                        _logger.LogTrace(
                            "Added {Count} sensor-level config transforms for ResourceId {ResourceId}",
                            configBasedTransforms.Count, resourceId);
                    }
                }

                // Priority 3: If no sensor-level transforms, use device-level transforms
                if (transformsToApply.Count == 0)
                {
                    transformsToApply = _transforms;
                    _logger.LogTrace(
                        "Using {Count} device-level transforms for ResourceId {ResourceId}",
                        transformsToApply.Count, resourceId);
                }

                // Apply transforms
                var current = resourceMeasures;
                var transformIndex = 0;
                foreach (var transform in transformsToApply)
                {
                    _logger.LogTrace(
                        "Executing transform '{TransformName}' for ResourceId {ResourceId}",
                        transform.Name, resourceId);

                    var context = new TelemetryTransformContext
                    {
                        DeviceId = _deviceId,
                        Timestamp = DateTimeOffset.UtcNow
                    };

                    var inputSnapshot = EnableValueChangeTracking ? current.ToList() : null;
                    var transformStopwatch = EnableValueChangeTracking ? Stopwatch.StartNew() : null;

                    current = await transform.TransformAsync(current, context, cancellationToken);

                    // Emit value change event if tracking is enabled
                    if (EnableValueChangeTracking)
                    {
                        transformStopwatch!.Stop();
                        EmitValueChangedEvent(
                            resourceId: resourceId,
                            stageName: transform.Name,
                            stage: ValueChangeStage.Transform,
                            stageIndex: transformIndex,
                            inputValues: inputSnapshot!,
                            outputValues: current,
                            duration: transformStopwatch.Elapsed);
                    }

                    transformIndex++;

                    if (current.Count == 0)
                    {
                        _logger.LogWarning(
                            "Transform '{TransformName}' filtered out all measures for ResourceId {ResourceId}",
                            transform.Name, resourceId);
                        break;
                    }
                }

                result.AddRange(current);
            }

            stopwatch.Stop();
            RecordDuration(_transformDurations, stopwatch.Elapsed);
            EmitStageEvent(PipelineStage.Transform, "Transforms", measures.Count, StagePhase.After,
                result.Count, stopwatch.Elapsed);

            return result;
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
        var stopwatch = Stopwatch.StartNew();
        EmitStageEvent(PipelineStage.Filter, "Filters", measures.Count, StagePhase.Before);

        string? errorMessage = null;

        try
        {
            var result = new List<TelemetryMeasure>();

            // Group measures by ResourceId for sensor-level processing
            var groupedMeasures = measures.GroupBy(m => m.ResourceId).ToList();

            foreach (var group in groupedMeasures)
            {
                var resourceId = group.Key;
                var resourceMeasures = group.ToList();

                // Find sensor configuration
                var sensor = _configuration?.Sensors.FirstOrDefault(s => s.ResourceId == resourceId);

                List<IDspFilter> filtersToApply = new();

                // Merge sensor-level runtime and config-based DSP filters
                if (sensor?.Report != null)
                {
                    // Priority 1: Add sensor-level runtime DSP filters (from code) - thread-safe
                    if (sensor.Report.RuntimeDspFilters.Count > 0)
                    {
                        filtersToApply.AddRange(sensor.Report.RuntimeDspFilters);
                        _logger.LogTrace(
                            "Added {Count} sensor-level runtime DSP filters for ResourceId {ResourceId}",
                            sensor.Report.RuntimeDspFilters.Count, resourceId);
                    }

                    // Priority 2: Add sensor-level config DSP filters (from appsettings.json or cloud)
                    // Use cache to preserve stateful filters (e.g., MovingAverage buffer, Kalman state)
                    if (sensor.Report.DspPipeline.Count > 0)
                    {
                        if (!_sensorFilterCache.TryGetValue(resourceId, out var cachedFilters))
                        {
                            cachedFilters = DspFilterFactory.CreateFromConfigs(sensor.Report.DspPipeline);
                            _sensorFilterCache[resourceId] = cachedFilters;
                            _logger.LogDebug(
                                "Created and cached {Count} sensor-level config DSP filters for ResourceId {ResourceId}",
                                cachedFilters.Count, resourceId);
                        }
                        filtersToApply.AddRange(cachedFilters);
                        _logger.LogTrace(
                            "Using {Count} cached sensor-level config DSP filters for ResourceId {ResourceId}",
                            cachedFilters.Count, resourceId);
                    }
                }

                // Priority 3: If no sensor-level filters, use device-level filters
                if (filtersToApply.Count == 0)
                {
                    filtersToApply = _filters;
                    _logger.LogTrace(
                        "Using {Count} device-level DSP filters for ResourceId {ResourceId}",
                        filtersToApply.Count, resourceId);
                }

                // Apply filters
                var currentList = resourceMeasures;
                var filterIndex = 0;
                foreach (var filter in filtersToApply)
                {
                    _logger.LogTrace(
                        "Executing DSP filter for ResourceId {ResourceId}",
                        resourceId);

                    var inputSnapshot = EnableValueChangeTracking ? currentList.ToList() : null;
                    var filterStopwatch = EnableValueChangeTracking ? Stopwatch.StartNew() : null;
                    var filterName = filter.GetType().Name;

                    var asyncInput = ToAsyncEnumerable(currentList);
                    var asyncOutput = filter.ApplyAsync(asyncInput, cancellationToken);
                    currentList = await ToListAsync(asyncOutput, cancellationToken);

                    // Emit value change event if tracking is enabled
                    if (EnableValueChangeTracking)
                    {
                        filterStopwatch!.Stop();
                        EmitValueChangedEvent(
                            resourceId: resourceId,
                            stageName: filterName,
                            stage: ValueChangeStage.Filter,
                            stageIndex: filterIndex,
                            inputValues: inputSnapshot!,
                            outputValues: currentList,
                            duration: filterStopwatch.Elapsed);
                    }

                    filterIndex++;

                    if (currentList.Count == 0)
                    {
                        _logger.LogWarning(
                            "DSP filter '{FilterName}' filtered out all measures for ResourceId {ResourceId}",
                            filterName, resourceId);
                        break;
                    }
                }

                var filtered = currentList;

                if (filtered.Count == 0)
                {
                    _logger.LogWarning(
                        "DSP filters filtered out all measures for ResourceId {ResourceId}",
                        resourceId);
                }

                result.AddRange(filtered);
            }

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

    private void EmitValueChangedEvent(
        string resourceId,
        string stageName,
        ValueChangeStage stage,
        int stageIndex,
        List<TelemetryMeasure> inputValues,
        List<TelemetryMeasure> outputValues,
        TimeSpan? duration = null,
        string? error = null)
    {
        ValueChanged?.Invoke(this, new TelemetryValueChangedEvent(
            DeviceId: _deviceId,
            ResourceId: resourceId,
            StageName: stageName,
            Stage: stage,
            Timestamp: DateTimeOffset.UtcNow)
        {
            InputValues = inputValues,
            OutputValues = outputValues,
            Duration = duration,
            Error = error,
            StageIndex = stageIndex
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
