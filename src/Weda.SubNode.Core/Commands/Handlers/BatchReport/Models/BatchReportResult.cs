using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;

namespace Weda.SubNode.Core.Commands.Handlers.BatchReport.Models;


/// <summary>
/// Result of the BatchReport command execution.
/// This object is serialized as the "data" field in the command response.
/// </summary>
/// <remarks>
/// Follows the REPORT Command Specification response format.
/// </remarks>
public class BatchReportResult : IResult
{
    /// <summary>
    /// The device command name.
    /// </summary>
    [JsonPropertyName("deviceCmd")]
    public string DeviceCmd { get; init; } = "report";

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
        string message,
        BatchReportResultData resultData,
        long executedAt,
        long completedAt = 0) => new()
        {
            DeviceCmd = deviceCmd,
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
        string errorCode,
        string errorMessage,
        long executedAt) => new()
        {
            DeviceCmd = deviceCmd,
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
/// Uses ISO 8601 format strings.
/// </summary>
public class BatchReportTimeRange
{
    [JsonPropertyName("startTime")]
    public string StartTime { get; init; } = string.Empty;

    [JsonPropertyName("endTime")]
    public string EndTime { get; init; } = string.Empty;

    /// <summary>
    /// Creates a BatchReportTimeRange from Unix timestamps in milliseconds.
    /// Converts to ISO 8601 format strings.
    /// </summary>
    public static BatchReportTimeRange FromUnixTimeMs(long startTimeMs, long endTimeMs) => new()
    {
        StartTime = DateTimeOffset.FromUnixTimeMilliseconds(startTimeMs).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
        EndTime = DateTimeOffset.FromUnixTimeMilliseconds(endTimeMs).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
    };
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

/// <summary>
/// Initial acknowledgment data for BatchReport command.
/// Sent at the start of execution with estimated metrics.
/// </summary>
public class BatchReportInitialAckData
{
    [JsonPropertyName("deviceCmd")]
    public string DeviceCmd { get; init; } = "report.historical";

    [JsonPropertyName("estimatedBatches")]
    public int EstimatedBatches { get; init; }

    [JsonPropertyName("estimatedSamples")]
    public int EstimatedSamples { get; init; }

    [JsonPropertyName("estimatedDurationSeconds")]
    public int EstimatedDurationSeconds { get; init; }

    [JsonPropertyName("storageAvailable")]
    public bool StorageAvailable { get; init; }
}

/// <summary>
/// Internal record for holding estimated metrics calculated before initial ack.
/// </summary>
/// <param name="EstimatedBatches">Estimated total number of batches to send.</param>
/// <param name="EstimatedSamples">Estimated total number of samples.</param>
/// <param name="EstimatedDurationSeconds">Estimated duration in seconds.</param>
internal record EstimatedMetrics(int EstimatedBatches, int EstimatedSamples, int EstimatedDurationSeconds);