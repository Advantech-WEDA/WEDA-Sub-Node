using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;

namespace Weda.SubNode.Core.Commands.Handlers.System.Models;

/// <summary>
/// Result of the system.shutdown command execution.
/// </summary>
public class SystemShutdownResult : IResult
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
    /// Result data containing operation details.
    /// </summary>
    public SystemCommandResultData? ResultData { get; init; }

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
    /// Creates a successful result indicating the shutdown is commencing.
    /// </summary>
    public static SystemShutdownResult Success(SystemCommandResultData resultData, long executedAt) => new()
    {
        Status = SystemCommandStatusCode.Success,
        Message = "System shutdown is commencing",
        ResultData = resultData,
        ExecutedAt = executedAt,
        CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };

    /// <summary>
    /// Creates an error result.
    /// </summary>
    public static SystemShutdownResult Error(int status, string errorMessage, long executedAt) => new()
    {
        Status = status,
        Message = errorMessage,
        ExecutedAt = executedAt,
        CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };
}
