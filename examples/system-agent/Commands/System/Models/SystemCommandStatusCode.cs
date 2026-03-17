namespace Weda.SubNode.Core.Commands.Handlers.System.Models;

/// <summary>
/// Status codes for system commands (reboot, shutdown).
/// </summary>
public static class SystemCommandStatusCode
{
    /// <summary>
    /// Command accepted and operation is commencing.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Invalid input arguments (e.g., negative delay).
    /// </summary>
    public const int InvalidArguments = 2;

    /// <summary>
    /// The system does not support this operation on the current platform.
    /// </summary>
    public const int NotSupported = 3;

    /// <summary>
    /// Insufficient permissions to perform the operation.
    /// </summary>
    public const int PermissionDenied = 6;

    /// <summary>
    /// The operation failed to initiate.
    /// </summary>
    public const int ExecutionFailed = 500;

    /// <summary>
    /// Gets the description for a status code.
    /// </summary>
    public static string GetDescription(int code) => code switch
    {
        Success => "Operation commencing",
        InvalidArguments => "Invalid input arguments",
        NotSupported => "Operation not supported on this platform",
        PermissionDenied => "Insufficient permissions",
        ExecutionFailed => "Operation failed to initiate",
        _ => "Unknown status"
    };
}
