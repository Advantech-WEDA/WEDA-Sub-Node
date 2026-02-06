using System.Text.Json.Serialization;
using Weda.SubNode.Abstractions.Commands;

namespace Weda.SubNode.Core.Commands.Handlers.ReportData.Models;

/// <summary>
/// Result of the ReportData command execution.
/// </summary>
public class ReportDataResult : IResult
{
    /// <summary>
    /// Status code indicating the result of the operation.
    /// </summary>
    public int Status { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Result data containing transfer metadata.
    /// </summary>
    public ReportDataResultData? ResultData { get; init; }

    /// <summary>
    /// Timestamp when execution started (Unix ms).
    /// </summary>
    public long ExecutedAt { get; init; }

    /// <summary>
    /// Timestamp when execution completed (Unix ms).
    /// </summary>
    public long CompletedAt { get; init; }

    // IResult explicit implementation
    object? IResult.ResultData => ResultData;
    long? IResult.ExecutedAt => ExecutedAt;
    long? IResult.CompletedAt => CompletedAt > 0 ? CompletedAt : null;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ReportDataResult Success(
        string message,
        ReportDataResultData resultData,
        long executedAt,
        long completedAt = 0) => new()
        {
            Status = ReportDataStatusCode.Success,
            Message = message,
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = completedAt > 0 ? completedAt : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    /// <summary>
    /// Creates an error result.
    /// </summary>
    public static ReportDataResult Error(
        int status,
        string errorMessage,
        ReportDataResultData? resultData,
        long executedAt) => new()
        {
            Status = status,
            Message = errorMessage,
            ResultData = resultData,
            ExecutedAt = executedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
}

/// <summary>
/// Result data for report.data command.
/// </summary>
public record ReportDataResultData
{
    [JsonPropertyName("sensorShortResourceId")]
    public string SensorShortResourceId { get; init; } = string.Empty;

    [JsonPropertyName("resourceTimestamp")]
    public long ResourceTimestamp { get; init; }

    [JsonPropertyName("transferId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransferId { get; init; }

    [JsonPropertyName("dataTransferred")]
    public bool DataTransferred { get; init; }
}

/// <summary>
/// Status codes for report.data command.
/// </summary>
public static class ReportDataStatusCode
{
    /// <summary>Command executed successfully, all data retrieved.</summary>
    public const int Success = 0;

    /// <summary>Command completed with data gaps or quality issues.</summary>
    public const int PartialSuccess = 1;

    /// <summary>Invalid input argument (bad sensor ID, unknown timestamp).</summary>
    public const int InvalidInputArgument = 2;

    /// <summary>No data found for specified sensor and timestamp.</summary>
    public const int NoDataAvailable = 3;

    /// <summary>Local storage unavailable or corrupted.</summary>
    public const int StorageError = 4;

    /// <summary>Command execution exceeded timeout.</summary>
    public const int Timeout = 5;

    /// <summary>Insufficient permissions for data access.</summary>
    public const int PermissionDenied = 6;

    /// <summary>Device resources insufficient for query.</summary>
    public const int ResourceExhausted = 7;

    /// <summary>Failed to read or chunk stored MIME data.</summary>
    public const int ChunkAssemblyFailed = 8;

    /// <summary>Stored data integrity check (CRC32) failed.</summary>
    public const int DataCorrupted = 9;

    /// <summary>Unexpected error.</summary>
    public const int GenericError = 500;

    public static string GetDescription(int code) => code switch
    {
        Success => "Data retrieval complete",
        PartialSuccess => "Data retrieval complete with gaps",
        InvalidInputArgument => "Invalid input argument",
        NoDataAvailable => "No data available",
        StorageError => "Storage error",
        Timeout => "Command timeout",
        PermissionDenied => "Permission denied",
        ResourceExhausted => "Resource exhausted",
        ChunkAssemblyFailed => "Chunk assembly failed",
        DataCorrupted => "Data corrupted",
        _ => "Unknown error"
    };
}