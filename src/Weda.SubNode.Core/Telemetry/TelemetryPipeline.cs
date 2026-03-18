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

    // Statistics tracking - using running average to avoid unbounded memory growth
    // Formula: avg = avg * n/(n+1) + value * 1/(n+1)
    private readonly bool _enableStatistics;
    private long _totalProcessed;
    private long _successfullySent;
    private long _failedToSend;
    private double _avgTransformDurationMs;
    private double _avgFilterDurationMs;
    private double _avgSendDurationMs;
    private double _avgTotalDurationMs;
    private long _transformSampleCount;
    private long _filterSampleCount;
    private long _sendSampleCount;
    private long _totalSampleCount;
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
        ITelemetryValidator? validator = null,
        TelemetryOptions? options = null)
    {
        _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _configuration = configuration;
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthMonitor = healthMonitor;
        _validator = validator;
        _enableStatistics = options?.EnableDataPipelineMetrics ?? false;
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

        var totalStopwatch = (_enableStatistics || _logger.IsEnabled(LogLevel.Debug))
            ? Stopwatch.StartNew()
            : null;
        Interlocked.Increment(ref _totalProcessed);

        try
        {
            // Stage 0: Validate - filter out invalid measures based on sensor schema
            // This prevents FormatException when Transform/Filter try to convert invalid values
            bool[] valid = ValidateMeasures(measures);
            var validMeasures = new List<TelemetryMeasure>();
            for (int i = 0; i < measures.Count; i++)
            {
                if (valid[i])
                    validMeasures.Add(measures[i]);
            }

            if (validMeasures.Count == 0)
            {
                _logger.LogDebug(
                    "All {Count} measures filtered out by validation for device {DeviceId}",
                    measures.Count, _deviceId);
                return new List<TelemetryMeasure>();
            }

            // Stage 1: Transform
            var transformResult = await ExecuteTransformStageAsync(validMeasures, cancellationToken);
            if (transformResult.IsError)
            {
                Interlocked.Increment(ref _failedToSend);
                var errorMsg = string.Join(", ", transformResult.Errors.Select(e => e.Description));
                _healthMonitor?.RecordFailure("TelemetryPipeline.Transform", new InvalidOperationException(errorMsg));
                return transformResult.Errors;
            }

            // Stage 2: Filter
            var filterResult = await ExecuteFilterStageAsync(transformResult.Value, cancellationToken);
            if (filterResult.IsError)
            {
                Interlocked.Increment(ref _failedToSend);
                var errorMsg = string.Join(", ", filterResult.Errors.Select(e => e.Description));
                _healthMonitor?.RecordFailure("TelemetryPipeline.Filter", new InvalidOperationException(errorMsg));
                return filterResult.Errors;
            }

            if (totalStopwatch != null)
            {
                totalStopwatch.Stop();
                RecordDuration(ref _avgTotalDurationMs, ref _totalSampleCount, totalStopwatch.Elapsed);
            }
            _lastProcessedAt = DateTimeOffset.UtcNow;

            _healthMonitor?.RecordSuccess("TelemetryPipeline");

            if (totalStopwatch != null)
            {
                _logger.LogDebug(
                    "TransformAndFilter completed for device {DeviceId}: {InputCount} → {OutputCount} measures in {Duration}ms",
                    _deviceId, measures.Count, filterResult.Value.Count, totalStopwatch.ElapsedMilliseconds);
            }

            LogValidationMetricsIfNeeded();

            return filterResult.Value;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedToSend);
            _healthMonitor?.RecordFailure("TelemetryPipeline", ex);

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
            AverageTransformDuration = TimeSpan.FromMilliseconds(_avgTransformDurationMs),
            AverageFilterDuration = TimeSpan.FromMilliseconds(_avgFilterDurationMs),
            AverageSendDuration = TimeSpan.FromMilliseconds(_avgSendDurationMs),
            AverageTotalDuration = TimeSpan.FromMilliseconds(_avgTotalDurationMs),
            LastProcessedAt = _lastProcessedAt
        };
    }

    private async Task<ErrorOr<List<TelemetryMeasure>>> ExecuteTransformStageAsync(
        List<TelemetryMeasure> measures,
        CancellationToken cancellationToken)
    {
        var stopwatch = (_enableStatistics || StageExecuting != null)
            ? Stopwatch.StartNew()
            : null;
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

                // Per-sensor try-catch: isolate failures so one sensor doesn't affect others
                try
                {
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
                catch (Exception ex)
                {
                    // Log warning and skip this sensor's measures, continue with others
                    _logger.LogWarning(ex,
                        "Transform failed for sensor {ResourceId} with {MeasureCount} measures, skipping. Other sensors will continue processing.",
                        resourceId, resourceMeasures.Count);
                }
            }

            if (stopwatch != null)
            {
                stopwatch.Stop();
                RecordDuration(ref _avgTransformDurationMs, ref _transformSampleCount, stopwatch.Elapsed);
            }
            EmitStageEvent(PipelineStage.Transform, "Transforms", measures.Count, StagePhase.After,
                result.Count, stopwatch?.Elapsed);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch?.Stop();
            errorMessage = ex.Message;
            EmitStageEvent(PipelineStage.Transform, "Transforms", measures.Count, StagePhase.After,
                null, stopwatch?.Elapsed, errorMessage);

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
        var stopwatch = (_enableStatistics || StageExecuting != null)
            ? Stopwatch.StartNew()
            : null;
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

                // Per-sensor try-catch: isolate failures so one sensor doesn't affect others
                try
                {
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
                catch (Exception ex)
                {
                    // Log warning and skip this sensor's measures, continue with others
                    _logger.LogWarning(ex,
                        "Filter failed for sensor {ResourceId} with {MeasureCount} measures, skipping. Other sensors will continue processing.",
                        resourceId, resourceMeasures.Count);
                }
            }

            if (stopwatch != null)
            {
                stopwatch.Stop();
                RecordDuration(ref _avgFilterDurationMs, ref _filterSampleCount, stopwatch.Elapsed);
            }

            if (result.Count == 0)
            {
                _logger.LogDebug(
                    "Filters filtered out all measures for device {DeviceId}",
                    _deviceId);
            }

            EmitStageEvent(PipelineStage.Filter, "Filters", measures.Count, StagePhase.After,
                result.Count, stopwatch?.Elapsed);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch?.Stop();
            errorMessage = ex.Message;
            EmitStageEvent(PipelineStage.Filter, "Filters", measures.Count, StagePhase.After,
                null, stopwatch?.Elapsed, errorMessage);

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
        var stopwatch = (_enableStatistics || StageExecuting != null)
            ? Stopwatch.StartNew()
            : null;
        EmitStageEvent(PipelineStage.Send, "CloudSend", measures.Count, StagePhase.Before);

        string? errorMessage = null;

        try
        {
            var telemetryData = new TelemetryData
            {
                Measures = measures
            };

            var success = await _cloudService.SendTelemetryAsync(_deviceId, telemetryData, cancellationToken);

            if (stopwatch != null)
            {
                stopwatch.Stop();
                RecordDuration(ref _avgSendDurationMs, ref _sendSampleCount, stopwatch.Elapsed);
            }

            // Record cloud send duration in health monitor
            if (stopwatch != null)
            {
                _healthMonitor?.RecordCloudSendDuration(stopwatch.Elapsed);
            }

            if (!success)
            {
                errorMessage = "Cloud service returned false";
                EmitStageEvent(PipelineStage.Send, "CloudSend", measures.Count, StagePhase.After,
                    null, stopwatch?.Elapsed, errorMessage);
                return Error.Failure(
                    code: "TelemetryPipeline.SendFailed",
                    description: "Failed to send telemetry to cloud");
            }

            EmitStageEvent(PipelineStage.Send, "CloudSend", measures.Count, StagePhase.After,
                measures.Count, stopwatch?.Elapsed);

            return Result.Success;
        }
        catch (Exception ex)
        {
            stopwatch?.Stop();
            errorMessage = ex.Message;
            EmitStageEvent(PipelineStage.Send, "CloudSend", measures.Count, StagePhase.After,
                null, stopwatch?.Elapsed, errorMessage);
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

    /// <summary>
    /// Updates running average using incremental formula: avg = avg * n/(n+1) + value/(n+1)
    /// This avoids unbounded memory growth from storing all samples.
    /// </summary>
    private void RecordDuration(ref double avgMs, ref long sampleCount, TimeSpan duration)
    {
        if (!_enableStatistics) return;

        var n = Interlocked.Read(ref sampleCount);
        var newValue = duration.TotalMilliseconds;

        // Thread-safe running average update
        // For simplicity, we use a lock here since statistics are optional
        _lock.Wait();
        try
        {
            if (n == 0)
            {
                avgMs = newValue;
            }
            else
            {
                // avg = avg * n/(n+1) + value * 1/(n+1)
                avgMs = avgMs * n / (n + 1) + newValue / (n + 1);
            }
            Interlocked.Increment(ref sampleCount);
        }
        finally
        {
            _lock.Release();
        }
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
