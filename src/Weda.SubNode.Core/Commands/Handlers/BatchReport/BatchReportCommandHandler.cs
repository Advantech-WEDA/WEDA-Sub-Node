using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;
using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Commands.Handlers.BatchReport.Models;

namespace Weda.SubNode.Core.Commands.Handlers.BatchReport;

/// <summary>
/// Handler for the "report" command that queries historical telemetry and sends batch records.
/// </summary>
/// <remarks>
/// Response handling (Received/Success/Failed) is managed by CommandDispatcher.
/// This handler sends its own initial ack with estimated metrics, so auto ack is disabled.
/// </remarks>
[Validation(typeof(BatchReportCommandValidator))]
[Logging(LogLevel.Information)]
[AutoAck(false)]
public class BatchReportCommandHandler : ICommandHandler<BatchReportCommand, BatchReportResult>
{
    public async Task<ErrorOr<BatchReportResult>> HandleAsync(
        BatchReportCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<BatchReportCommandHandler>();
        var recordingService = context.RecordingService;
        var cloudService = context.CloudService;
        var subNodeId = context.SubNodeInfo.Id;
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Validate recording service is available
        if (recordingService is null)
        {
            return BatchReportResult.Error(
                BatchReportStatusCode.StorageError,
                "STORAGE_UNAVAILABLE",
                "RecordingService is not configured",
                executedAt);
        }

        // Validate SubNode is registered
        if (string.IsNullOrEmpty(subNodeId))
        {
            return BatchReportResult.Error(
                BatchReportStatusCode.PermissionDenied,
                "NOT_REGISTERED",
                "SubNode is not registered",
                executedAt);
        }

        // Get effective time range (applies defaults if TimeRange is null)
        var effectiveTimeRange = command.GetEffectiveTimeRange();

        // Validate time range: startTime must be before endTime
        if (effectiveTimeRange.StartTime >= effectiveTimeRange.EndTime)
        {
            return BatchReportResult.Error(
                BatchReportStatusCode.InvalidTimeRange,
                "INVALID_TIME_RANGE",
                "Start time must be before end time",
                executedAt);
        }

        var maxQueryDays = context.RecordingOptions?.MaxQueryTimeRangeDays ?? 30;
        if (maxQueryDays > 0)
        {
            var maxRangeMs = (long)TimeSpan.FromDays(maxQueryDays).TotalMilliseconds;
            if (effectiveTimeRange.EndTime - effectiveTimeRange.StartTime > maxRangeMs)
            {
                return BatchReportResult.Error(
                    BatchReportStatusCode.InvalidTimeRange,
                    "TIME_RANGE_TOO_LARGE",
                    $"Time range exceeds maximum of {maxQueryDays} days",
                    executedAt);
            }
        }

        var startTime = DateTimeOffset.FromUnixTimeMilliseconds(effectiveTimeRange.StartTime);
        var endTime = DateTimeOffset.FromUnixTimeMilliseconds(effectiveTimeRange.EndTime);

        logger.LogInformation(
            "Executing BatchReport: {StartTime} to {EndTime}, MaxBatches={MaxBatches}, RateLimit={RateLimit}",
            startTime, endTime, command.MaxBatchesPerMessage, command.TransmissionRateLimit);

        // Step 1. Get all sensor IDs
        var sensorIdsResult = await recordingService.GetSensorIdsAsync(cancellationToken);
        if (sensorIdsResult.IsError)
        {
            return BatchReportResult.Error(
                BatchReportStatusCode.StorageError,
                "STORAGE_ERROR",
                sensorIdsResult.FirstError.Description,
                executedAt);
        }

        var sensorIds = FilterSensors(sensorIdsResult.Value, command.SensorFilter);
        if (sensorIds.Count == 0)
        {
            logger.LogInformation("No sensors match the filter criteria");
            return BatchReportResult.Success(
                BatchReportStatusCode.NoDataAvailable,
                "No sensors match the filter criteria",
                new BatchReportResultData
                {
                    BatchesSent = 0,
                    TotalSamples = 0,
                    TimeRange = BatchReportTimeRange.FromUnixTimeMs(effectiveTimeRange.StartTime, effectiveTimeRange.EndTime),
                    Sensors = []
                },
                executedAt);
        }

        logger.LogDebug("Processing {Count} sensors", sensorIds.Count);

        // Calculate estimated metrics for initial ack based on sensor intervals and command parameters
        var estimate = CalculateEstimatedMetrics(
            sensorIds, effectiveTimeRange, command.MaxBatchesPerMessage, command.MaxBatchSize,
            command.TransmissionRateLimit, context, logger);

        // Send initial ack with estimates (include SeqId and ReqSeqId from command)
        await SendInitialAckAsync(cloudService, subNodeId, command.RespTopic, command.DeviceCmd,
            command.SeqId, command.ReqSeqId, estimate.EstimatedBatches, estimate.EstimatedSamples,
            estimate.EstimatedDurationSeconds, logger, cancellationToken);

        // Step 2. Query and batch send with timeout support
        var batchBuffer = new List<BatchTelemetryMeasureDto>();
        var batchBufferSampleCount = 0;
        var messageCount = 0;
        var failedSensors = new List<string>();
        var failedBatches = 0;
        var totalSamples = 0;
        var processedSensors = new List<string>();
        var estimatedTotalBatches = estimate.EstimatedBatches;

        // Create timeout-linked cancellation token
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(command.Timeout));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var linkedToken = linkedCts.Token;

        // Local function to flush the batch buffer
        async Task<bool> FlushBatchBufferAsync()
        {
            if (batchBuffer.Count == 0)
                return true;

            var success = await SendBatchAsync(cloudService, subNodeId, batchBuffer, logger, linkedToken);
            if (!success)
            {
                failedBatches++;
            }
            batchBuffer.Clear();
            batchBufferSampleCount = 0;
            messageCount++;

            // Progress update
            await SendProgressAsync(cloudService, subNodeId, command.RespTopic, command.SeqId, command.ReqSeqId,
                new BatchReportProgress
                {
                    DeviceCmd = command.DeviceCmd,
                    Progress = new BatchReportProgressData
                    {
                        BatchesSent = messageCount,
                        TotalBatches = estimatedTotalBatches,
                        SamplesSent = totalSamples,
                        TotalSamples = estimate.EstimatedSamples,
                        PercentComplete = Math.Round((double)processedSensors.Count / sensorIds.Count * 100, 1)
                    }
                }, logger, linkedToken);

            if (command.TransmissionRateLimit > 0)
            {
                var delayMs = 1000 / command.TransmissionRateLimit;
                await Task.Delay(delayMs, linkedToken);
            }

            return success;
        }

        try
        {
            foreach (var sensorId in sensorIds)
            {
                linkedToken.ThrowIfCancellationRequested();

                var recordingResult = await recordingService.GetRecordingsAsync(sensorId, startTime, endTime, linkedToken);

                if (recordingResult.IsError)
                {
                    logger.LogWarning("Failed to get recordings for sensor {SensorId}: {Error}", sensorId, recordingResult.Errors.First().Description);
                    failedSensors.Add(sensorId);
                    continue;
                }

                processedSensors.Add(sensorId);

                // Convert recording to batch measures with splitting support
                foreach (var measure in recordingResult.Value.Measures)
                {
                    var values = measure.Values;
                    var valuesProcessed = 0;

                    // Split measure if it exceeds maxBatchSize (vertical split)
                    while (valuesProcessed < values.Count)
                    {
                        // Calculate how many samples we can add to current batch
                        var remainingInBatch = command.MaxBatchSize - batchBufferSampleCount;
                        var remainingInMeasure = values.Count - valuesProcessed;
                        var samplesToTake = Math.Min(remainingInBatch, remainingInMeasure);

                        // If current batch is full, flush it first
                        if (samplesToTake == 0 || batchBuffer.Count >= command.MaxBatchesPerMessage)
                        {
                            await FlushBatchBufferAsync();
                            remainingInBatch = command.MaxBatchSize;
                            samplesToTake = Math.Min(remainingInBatch, remainingInMeasure);
                        }

                        // Take a slice of values
                        var sliceValues = values
                            .Skip(valuesProcessed)
                            .Take(samplesToTake)
                            .Select(v => double.IsFinite(v) ? (double?)v : null)
                            .ToList();

                        // Calculate the start timestamp for this slice
                        var sliceStartTimestamp = measure.StartTimeStamp + (valuesProcessed * measure.Interval);

                        batchBuffer.Add(new BatchTelemetryMeasureDto
                        {
                            Id = sensorId,
                            Interval = measure.Interval,
                            StartTimeStamp = sliceStartTimestamp,
                            Values = sliceValues
                        });

                        batchBufferSampleCount += samplesToTake;
                        totalSamples += samplesToTake;
                        valuesProcessed += samplesToTake;

                        // Check if we should send a batch (horizontal split by measure count)
                        if (batchBuffer.Count >= command.MaxBatchesPerMessage ||
                            batchBufferSampleCount >= command.MaxBatchSize)
                        {
                            await FlushBatchBufferAsync();
                        }
                    }
                }
            }

            // Send remaining batch
            await FlushBatchBufferAsync();
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            logger.LogWarning("BatchReport timed out after {Timeout} seconds", command.Timeout);
            return BatchReportResult.Error(
                BatchReportStatusCode.Timeout,
                "TIMEOUT",
                $"Command execution exceeded {command.Timeout} seconds timeout",
                executedAt);
        }
        catch (OperationCanceledException)
        {
            // Original cancellation token was triggered (not timeout)
            return BatchReportResult.Error(
                BatchReportStatusCode.Timeout,
                "CANCELLED",
                "Operation was cancelled",
                executedAt);
        }
        catch (OutOfMemoryException ex)
        {
            logger.LogError(ex, "Out of memory while loading data from storage");
            return BatchReportResult.Error(
                BatchReportStatusCode.ResourceExhausted,
                "RESOURCE_EXHAUSTED",
                "Insufficient memory to load data. Reduce time range or batch size.",
                executedAt);
        }

        var completedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var durationSeconds = (completedAt - executedAt) / 1000;

        logger.LogInformation("BatchReport completed: {MessageCount} messages sent, {TotalSamples} samples, {FailedBatches} failed batches in {Duration}s",
            messageCount, totalSamples, failedBatches, durationSeconds);

        // Determine final status - include failed batches in consideration
        var statusCode = (failedBatches > 0 || failedSensors.Count > 0)
            ? BatchReportStatusCode.PartialSuccess
            : (messageCount == 0 ? BatchReportStatusCode.NoDataAvailable : BatchReportStatusCode.Success);

        var message = statusCode switch
        {
            BatchReportStatusCode.Success => "Historical data retrieval complete",
            BatchReportStatusCode.PartialSuccess => "Historical data retrieval complete with gaps",
            BatchReportStatusCode.NoDataAvailable => "No data found for specified time range",
            _ => BatchReportStatusCode.GetDescription(statusCode)
        };

        var resultData = new BatchReportResultData
        {
            BatchesSent = messageCount,
            TotalSamples = totalSamples,
            TimeRange = BatchReportTimeRange.FromUnixTimeMs(effectiveTimeRange.StartTime, effectiveTimeRange.EndTime),
            Sensors = processedSensors.ToArray(),
            DataGaps = failedSensors.Count > 0
                ? failedSensors.Select(s => new BatchReportDataGap
                {
                    Reason = "sensorError",
                    SensorId = s
                }).ToArray()
                : null
        };

        return BatchReportResult.Success(statusCode, message, resultData, executedAt, completedAt);
    }

    /// <summary>
    /// Calculates estimated metrics for initial ack based on sensor intervals and command parameters.
    /// </summary>
    /// <param name="sensorIds">List of sensor IDs to process.</param>
    /// <param name="timeRange">The effective time range for the query.</param>
    /// <param name="maxBatchesPerMessage">Maximum batches per message from command.</param>
    /// <param name="maxBatchSize">Maximum samples per batch from command.</param>
    /// <param name="transmissionRateLimit">Transmission rate limit from command.</param>
    /// <param name="context">Application context for accessing device configurations.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <returns>Estimated metrics for initial ack.</returns>
    private static EstimatedMetrics CalculateEstimatedMetrics(
        IReadOnlyList<string> sensorIds,
        TimeRange timeRange,
        int maxBatchesPerMessage,
        int maxBatchSize,
        int transmissionRateLimit,
        IWedaApplicationContext context,
        ILogger logger)
    {
        var timeRangeMs = timeRange.EndTime - timeRange.StartTime;
        var totalEstimatedSamples = 0L;
        var totalEstimatedBatches = 0;

        // Build a lookup of sensor intervals from DeviceConfigs
        var sensorIntervals = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var deviceConfig in context.DeviceConfigs.Values)
        {
            foreach (var sensor in deviceConfig.Sensors)
            {
                // Use ShortId as the key since recording service uses ShortId
                if (!string.IsNullOrEmpty(sensor.ShortId))
                {
                    sensorIntervals[sensor.ShortId] = sensor.Report.Interval;
                }
                // Also add full ResourceId for compatibility
                if (!string.IsNullOrEmpty(sensor.ResourceId))
                {
                    sensorIntervals[sensor.ResourceId] = sensor.Report.Interval;
                }
            }
        }

        // Calculate estimated samples per sensor
        foreach (var sensorId in sensorIds)
        {
            // Get sensor interval, default to 1000ms if not found
            var intervalMs = sensorIntervals.TryGetValue(sensorId, out var interval) ? interval : 1000.0;

            // Avoid division by zero
            if (intervalMs <= 0)
            {
                intervalMs = 1000.0;
            }

            // Estimate number of samples for this sensor
            var estimatedSamplesForSensor = (long)Math.Ceiling(timeRangeMs / intervalMs) + 1;
            totalEstimatedSamples += estimatedSamplesForSensor;

            // Estimate number of batches for this sensor (based on maxBatchSize)
            var batchesForSensor = (int)Math.Ceiling((double)estimatedSamplesForSensor / maxBatchSize);
            totalEstimatedBatches += Math.Max(1, batchesForSensor);
        }

        // Adjust for maxBatchesPerMessage (how many sensor batches fit in one message)
        var estimatedMessages = (int)Math.Ceiling((double)totalEstimatedBatches / maxBatchesPerMessage);

        // Calculate estimated duration based on transmission rate limit
        int estimatedDurationSeconds;
        if (transmissionRateLimit > 0)
        {
            // Duration = messages / rate limit
            estimatedDurationSeconds = Math.Max(1, (int)Math.Ceiling((double)estimatedMessages / transmissionRateLimit));
        }
        else
        {
            // Without rate limiting, estimate based on number of messages (assume ~10 msg/sec processing)
            estimatedDurationSeconds = Math.Max(1, estimatedMessages / 10);
        }

        logger.LogDebug(
            "Estimated metrics: {Samples} samples, {Batches} batches, {Messages} messages, {Duration}s",
            totalEstimatedSamples, totalEstimatedBatches, estimatedMessages, estimatedDurationSeconds);

        return new EstimatedMetrics(totalEstimatedBatches, (int)Math.Min(totalEstimatedSamples, int.MaxValue), estimatedDurationSeconds);
    }

    private static IReadOnlyList<string> FilterSensors(
        IReadOnlyList<string> sensorIds,
        SensorFilter? filter)
    {
        if (filter is null)
        {
            return sensorIds;
        }

        var result = sensorIds.AsEnumerable();

        if (filter.Include is { Length: > 0 })
        {
            var includeSet = new HashSet<string>(filter.Include, StringComparer.OrdinalIgnoreCase);
            result = result.Where(id => includeSet.Contains(id));
        }

        if (filter.Exclude is { Length: > 0 })
        {
            var excludeSet = new HashSet<string>(filter.Exclude, StringComparer.OrdinalIgnoreCase);
            result = result.Where(id => !excludeSet.Contains(id));
        }

        return result.ToList();
    }

    private static async Task<bool> SendBatchAsync(
        IWedaCloudService cloudService,
        string subNodeId,
        List<BatchTelemetryMeasureDto> measures,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var message = BatchTelemetrySendMessage.Create(subNodeId, measures);
        var success = await cloudService.SendBatchTelemetryAsync(subNodeId, message, cancellationToken);

        if (!success)
        {
            logger.LogWarning("Failed to send batch telemetry with {Count} measures", measures.Count);
        }
        else
        {
            logger.LogDebug("Sent batch with {Count} measures", measures.Count);
        }

        return success;
    }

    private static async Task SendProgressAsync(
        IWedaCloudService cloudService,
        string deviceId,
        string respTopic,
        ulong seqId,
        string? reqSeqId,
        BatchReportProgress progress,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(respTopic))
            return;

        try
        {
            var response = new CommandResponse
            {
                DeviceId = deviceId,
                SeqId = seqId,
                ReqSeqId = reqSeqId,
                Data = new CommandResponseData
                {
                    DeviceCmd = progress.DeviceCmd,
                    MsgType = "progress",
                    Status = BatchReportStatusCode.Success,
                    Message = "Progress update",
                    ResultData = progress.Progress
                }
            };

            await cloudService.SendCommandResponseAsync(respTopic, response, cancellationToken);
            logger.LogDebug("Progress update sent: {BatchesSent}/{TotalBatches} batches",
                progress.Progress.BatchesSent, progress.Progress.TotalBatches);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send progress update");
        }
    }

    private static async Task SendInitialAckAsync(
        IWedaCloudService cloudService,
        string deviceId,
        string respTopic,
        string deviceCmd,
        ulong seqId,
        string? reqSeqId,
        int estimatedBatches,
        int estimatedSamples,
        int estimatedDurationSeconds,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(respTopic))
            return;

        try
        {
            var response = new CommandResponse
            {
                DeviceId = deviceId,
                SeqId = seqId,
                ReqSeqId = reqSeqId,
                Data = new CommandResponseData
                {
                    DeviceCmd = deviceCmd,
                    MsgType = "ack",
                    Status = BatchReportStatusCode.Success,
                    Message = "Historical data query started",
                    ResultData = new
                    {
                        estimatedBatches,
                        estimatedSamples,
                        estimatedDurationSeconds,
                        storageAvailable = true
                    }
                }
            };

            await cloudService.SendCommandResponseAsync(respTopic, response, cancellationToken);
            logger.LogDebug("Initial ack sent: estimated {Batches} batches, {Samples} samples, {Duration}s",
                estimatedBatches, estimatedSamples, estimatedDurationSeconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send initial ack");
        }
    }
}
