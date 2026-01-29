namespace Weda.SubNode.Core.Commands.Handlers.BatchReport.Models;

/// <summary>
/// Status codes for BatchReport command execution result.
/// </summary>
public static class BatchReportStatusCode
{
    /// <summary>
    /// Command executed successfully, all data retrieved.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Command completed with data gaps or quality issues.
    /// </summary>
    public const int PartialSuccess = 1;

    /// <summary>
    /// Time range is invalid or exceeds retention period.
    /// </summary>
    public const int InvalidTimeRange = 2;

    /// <summary>
    /// No data found for specified time range.
    /// </summary>
    public const int NoDataAvailable = 3;

    /// <summary>
    /// Local storage unavailable or corrupted.
    /// </summary>
    public const int StorageError = 4;

    /// <summary>
    /// Command execution exceeded timeout.
    /// </summary>
    public const int Timeout = 5;

    /// <summary>
    /// Insufficient permissions for data access.
    /// </summary>
    public const int PermissionDenied = 6;

    /// <summary>
    /// Device resources insufficient for query.
    /// </summary>
    public const int ResourceExhausted = 7;

    /// <summary>
    /// Gets the description for a status code.
    /// </summary>
    public static string GetDescription(int code) => code switch
    {
        Success => "Command executed successfully, all data retrieved",
        PartialSuccess => "Command completed with data gaps or quality issues",
        InvalidTimeRange => "Time range is invalid or exceeds retention period",
        NoDataAvailable => "No data found for specified time range",
        StorageError => "Local storage unavailable or corrupted",
        Timeout => "Command execution exceeded timeout",
        PermissionDenied => "Insufficient permissions for data access",
        ResourceExhausted => "Device resources insufficient for query",
        _ => "Unknown status"
    };
}
