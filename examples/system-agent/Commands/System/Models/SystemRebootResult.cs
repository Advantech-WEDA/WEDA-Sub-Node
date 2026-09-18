using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;

namespace Weda.SubNode.Core.Commands.Handlers.System.Models;

/// <summary>
/// Result of the system.reboot command execution.
/// </summary>
public class SystemRebootResult : IResult
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

    // IResult explicit implementation
    object? IResult.ResultData => ResultData;

    /// <summary>
    /// Creates a successful result indicating the reboot is commencing.
    /// </summary>
    public static SystemRebootResult Success(SystemCommandResultData resultData) => new()
    {
        Status = SystemCommandStatusCode.Success,
        Message = "System reboot is commencing",
        ResultData = resultData
    };

    /// <summary>
    /// Creates an error result.
    /// </summary>
    public static SystemRebootResult Error(int status, string errorMessage) => new()
    {
        Status = status,
        Message = errorMessage
    };
}

/// <summary>
/// Result data for system commands (reboot, shutdown).
/// </summary>
public class SystemCommandResultData
{
    /// <summary>
    /// The operation being performed ("reboot" or "shutdown").
    /// </summary>
    [JsonPropertyName("operation")]
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    /// Delay in seconds before the operation starts.
    /// </summary>
    [JsonPropertyName("delaySeconds")]
    public int DelaySeconds { get; init; }
}
