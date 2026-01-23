using System.Text.Json.Serialization;

using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;
using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Commands.Handlers.BatchReport.Models;

namespace Weda.SubNode.Core.Commands.Handlers.BatchReport;

/// <summary>
/// Handler for the "report" command that queries historical telemetry and sends batch records.
/// </summary>
/// <remarks>
/// Response handling (Received/Success/Failed) is managed by CommandDispatcher.
/// This handler only focuses on business logic and returns the result.
/// </remarks>
[Validation(typeof(BatchReportCommandValidator))]
[Logging(LogLevel.Information)]
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
                command.DeviceCmd,
                command.ReportType,
                "STORAGE_UNAVAILABLE",
                "RecordingService is not configured",
                executedAt);
        }

        // Validate SubNode is registered
        if (string.IsNullOrEmpty(subNodeId))
        {
            return BatchReportResult.Error(
                BatchReportStatusCode.PermissionDenied,
                command.DeviceCmd,
                command.ReportType,
                "NOT_REGISTERED",
                "SubNode is not registered",
                executedAt);
        }

        // Get effective time range (applies defaults if TimeRange is null)
        var effectiveTimeRange = command.GetEffectiveTimeRange();
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
                command.DeviceCmd,
                command.ReportType,
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
                command.DeviceCmd,
                command.ReportType,
                "No sensors match the filter criteria",
                new BatchReportResultData
                {
                    BatchesSent = 0,
                    TotalSamples = 0,
                    TimeRange = new BatchReportTimeRange
                    {
                        StartTime = effectiveTimeRange.StartTime,
                        EndTime = effectiveTimeRange.EndTime
                    },
                    Sensors = []
                },
                executedAt);
        }

        logger.LogDebug("Processing {Count} sensors", sensorIds.Count);

        // Step 2. Query and batch send with timeout support
        var batchBuffer = new List<BatchTelemetryMeasureDto>();
        var batchBufferSampleCount = 0;
        var messageCount = 0;
        var failedSensors = new List<string>();
        var failedBatches = 0;
        var totalSamples = 0;
        var processedSensors = new List<string>();
        var estimatedTotalBatches = sensorIds.Count; // Rough estimate, will be refined

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
            await SendProgressAsync(cloudService, subNodeId, command.RespTopic,
                new BatchReportProgress
                {
                    DeviceCmd = command.DeviceCmd,
                    ReportType = command.ReportType,
                    Progress = new BatchReportProgressData
                    {
                        BatchesSent = messageCount,
                        TotalBatches = estimatedTotalBatches,
                        SamplesSent = totalSamples,
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
                command.DeviceCmd,
                command.ReportType,
                "TIMEOUT",
                $"Command execution exceeded {command.Timeout} seconds timeout",
                executedAt);
        }
        catch (OperationCanceledException)
        {
            // Original cancellation token was triggered (not timeout)
            return BatchReportResult.Error(
                BatchReportStatusCode.Timeout,
                command.DeviceCmd,
                command.ReportType,
                "CANCELLED",
                "Operation was cancelled",
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
            TimeRange = new BatchReportTimeRange
            {
                StartTime = effectiveTimeRange.StartTime,
                EndTime = effectiveTimeRange.EndTime
            },
            Sensors = processedSensors.ToArray(),
            DataGaps = failedSensors.Count > 0
                ? failedSensors.Select(s => new BatchReportDataGap
                {
                    Reason = "sensorError",
                    SensorId = s
                }).ToArray()
                : null
        };

        return BatchReportResult.Success(statusCode, command.DeviceCmd, command.ReportType, message, resultData, executedAt, completedAt);
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

        if (filter.Exclude is { Length: > 0})
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
                Status = CommandResponseStatusCode.Received, // Progress uses status=0
                Message = "Progress update",
                Data = progress
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
}

/// <summary>
/// Result of the BatchReport command execution.
/// This object is serialized as the "data" field in the command response.
/// </summary>
/// <remarks>
/// Follows the REPORT Command Specification response format.
/// </remarks>
public class BatchReportResult
{
    /// <summary>
    /// The device command name.
    /// </summary>
    [JsonPropertyName("deviceCmd")]
    public string DeviceCmd { get; init; } = "report";

    /// <summary>
    /// The report type requested.
    /// </summary>
    [JsonPropertyName("reportType")]
    public string ReportType { get; init; } = string.Empty;

    /// <summary>
    /// Status code indicating the result of the operation.
    /// </summary>
    /// <seealso cref="BatchReportStatusCode"/>
    [JsonPropertyName("status")]
    public int Status { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Result data containing batch statistics and metadata.
    /// </summary>
    [JsonPropertyName("resultData")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BatchReportResultData? ResultData { get; init; }

    /// <summary>
    /// Error details (only for error cases).
    /// </summary>
    [JsonPropertyName("errorDetails")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BatchReportErrorDetails? ErrorDetails { get; init; }

    /// <summary>
    /// Timestamp when execution started (Unix ms).
    /// </summary>
    [JsonPropertyName("executedAt")]
    public long ExecutedAt { get; init; }

    /// <summary>
    /// Timestamp when execution completed (Unix ms).
    /// </summary>
    [JsonPropertyName("completedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long CompletedAt { get; init; }

    /// <summary>
    /// Duration of execution in seconds.
    /// </summary>
    [JsonPropertyName("durationSeconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long DurationSeconds => CompletedAt > 0 ? (CompletedAt - ExecutedAt) / 1000 : 0;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static BatchReportResult Success(
        int status,
        string deviceCmd,
        string reportType,
        string message,
        BatchReportResultData resultData,
        long executedAt,
        long completedAt = 0) => new()
    {
        DeviceCmd = deviceCmd,
        ReportType = reportType,
        Status = status,
        Message = message,
        ResultData = resultData,
        ExecutedAt = executedAt,
        CompletedAt = completedAt > 0 ? completedAt : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };

    /// <summary>
    /// Creates an error result.
    /// </summary>
    public static BatchReportResult Error(
        int status,
        string deviceCmd,
        string reportType,
        string errorCode,
        string errorMessage,
        long executedAt) => new()
    {
        DeviceCmd = deviceCmd,
        ReportType = reportType,
        Status = status,
        Message = errorMessage,
        ErrorDetails = new BatchReportErrorDetails
        {
            ErrorCode = errorCode,
            Recommendation = GetRecommendation(status)
        },
        ExecutedAt = executedAt,
        CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };

    private static string? GetRecommendation(int status) => status switch
    {
        BatchReportStatusCode.InvalidTimeRange => "Adjust time range to available period",
        BatchReportStatusCode.StorageError => "Check device storage health and retry",
        BatchReportStatusCode.Timeout => "Reduce query scope or increase timeout",
        BatchReportStatusCode.ResourceExhausted => "Reduce batch size or add rate limiting",
        _ => null
    };
}

/// <summary>
/// Result data for successful batch report operations.
/// </summary>
public class BatchReportResultData
{
    [JsonPropertyName("batchesSent")]
    public int BatchesSent { get; init; }

    [JsonPropertyName("totalSamples")]
    public int TotalSamples { get; init; }

    [JsonPropertyName("timeRange")]
    public BatchReportTimeRange? TimeRange { get; init; }

    [JsonPropertyName("sensors")]
    public string[]? Sensors { get; init; }

    [JsonPropertyName("dataGaps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BatchReportDataGap[]? DataGaps { get; init; }
}

/// <summary>
/// Time range for batch report results.
/// Uses Unix timestamp in milliseconds.
/// </summary>
public class BatchReportTimeRange
{
    [JsonPropertyName("startTime")]
    public long StartTime { get; init; }

    [JsonPropertyName("endTime")]
    public long EndTime { get; init; }
}

/// <summary>
/// Data gap information for partial success results.
/// </summary>
public class BatchReportDataGap
{
    [JsonPropertyName("sensorId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SensorId { get; init; }

    [JsonPropertyName("startTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartTime { get; init; }

    [JsonPropertyName("endTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EndTime { get; init; }

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    [JsonPropertyName("missingSamples")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int MissingSamples { get; init; }
}

/// <summary>
/// Error details for failed batch report operations.
/// </summary>
public class BatchReportErrorDetails
{
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; init; } = string.Empty;

    [JsonPropertyName("recommendation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Recommendation { get; init; }
}

/// <summary>
/// Progress update data for BatchReport command.
/// Sent periodically during long-running batch report operations.
/// </summary>
public class BatchReportProgress
{
    [JsonPropertyName("deviceCmd")]
    public string DeviceCmd { get; init; } = "report";

    [JsonPropertyName("reportType")]
    public string ReportType { get; init; } = string.Empty;

    [JsonPropertyName("progress")]
    public required BatchReportProgressData Progress { get; init; }
}

/// <summary>
/// Progress data containing batch statistics during execution.
/// </summary>
public class BatchReportProgressData
{
    [JsonPropertyName("batchesSent")]
    public int BatchesSent { get; init; }

    [JsonPropertyName("totalBatches")]
    public int TotalBatches { get; init; }

    [JsonPropertyName("samplesSent")]
    public int SamplesSent { get; init; }

    [JsonPropertyName("totalSamples")]
    public int TotalSamples { get; init; }

    [JsonPropertyName("percentComplete")]
    public double PercentComplete { get; init; }

    [JsonPropertyName("currentTimeRange")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BatchReportTimeRange? CurrentTimeRange { get; init; }
}
