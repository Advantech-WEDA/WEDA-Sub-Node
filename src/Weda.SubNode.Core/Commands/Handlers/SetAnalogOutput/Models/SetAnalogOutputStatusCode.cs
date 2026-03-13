namespace Weda.SubNode.Core.Commands.Handlers.SetAnalogOutput.Models;

/// <summary>
/// Status codes for SetAnalogOutput command execution result.
/// </summary>
public static class SetAnalogOutputStatusCode
{
    /// <summary>
    /// All outputs set successfully.
    /// </summary>
    public const int Success = 200;

    /// <summary>
    /// Some outputs failed to set.
    /// </summary>
    public const int PartialSuccess = 206;

    /// <summary>
    /// Invalid input arguments.
    /// </summary>
    public const int InvalidArguments = 400;

    /// <summary>
    /// Device not found.
    /// </summary>
    public const int DeviceNotFound = 404;

    /// <summary>
    /// Output not found on device.
    /// </summary>
    public const int OutputNotFound = 404;

    /// <summary>
    /// Device does not support analog output control.
    /// </summary>
    public const int NotSupported = 405;

    /// <summary>
    /// Command execution failed.
    /// </summary>
    public const int ExecutionFailed = 500;

    /// <summary>
    /// Command timed out.
    /// </summary>
    public const int Timeout = 504;

    /// <summary>
    /// Gets the description for a status code.
    /// </summary>
    public static string GetDescription(int code) => code switch
    {
        Success => "All outputs set successfully",
        PartialSuccess => "Some outputs failed to set",
        InvalidArguments => "Invalid input arguments",
        DeviceNotFound => "Device not found",
        NotSupported => "Device does not support analog output control",
        ExecutionFailed => "Command execution failed",
        Timeout => "Command execution exceeded timeout",
        _ => "Unknown status"
    };
}
