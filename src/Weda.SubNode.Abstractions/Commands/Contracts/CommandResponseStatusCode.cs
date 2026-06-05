namespace Weda.SubNode.Abstractions.Commands.Contracts;

/// <summary>
/// Unified command response status codes for all command handlers.
/// </summary>
/// <remarks>
/// Status codes follow the Sub-Node Command Interface specification:
/// <list type="table">
/// <item><term>0</term><description>SUCCESS - Command executed successfully</description></item>
/// <item><term>1</term><description>VALIDATION_FAILED - Parameter validation failed</description></item>
/// <item><term>2</term><description>DEVICE_BUSY - Device is busy, cannot execute command</description></item>
/// <item><term>3</term><description>TIMEOUT - Command execution timeout</description></item>
/// <item><term>4</term><description>HARDWARE_ERROR - Hardware rejected command or execution failed</description></item>
/// <item><term>5</term><description>PERMISSION_DENIED - Insufficient permissions for command</description></item>
/// <item><term>6</term><description>UNSUPPORTED_COMMAND - Command not supported by device</description></item>
/// <item><term>7</term><description>SAFETY_VIOLATION - Command blocked by safety constraints</description></item>
/// <item><term>8</term><description>PARTIAL_SUCCESS - Some operations succeeded, some failed</description></item>
/// <item><term>9</term><description>NOT_FOUND - Device, output, or data not found</description></item>
/// <item><term>10</term><description>RESOURCE_EXHAUSTED - Insufficient resources (OOM)</description></item>
/// </list>
/// </remarks>
public static class CommandStatusCode
{
    /// <summary>
    /// Command executed successfully (status = 0)
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Parameter validation failed (status = 1)
    /// </summary>
    public const int ValidationFailed = 1;

    /// <summary>
    /// Device is busy, cannot execute command (status = 2)
    /// </summary>
    public const int DeviceBusy = 2;

    /// <summary>
    /// Command execution timeout (status = 3)
    /// </summary>
    public const int Timeout = 3;

    /// <summary>
    /// Hardware rejected command or execution failed (status = 4)
    /// </summary>
    public const int HardwareError = 4;

    /// <summary>
    /// Insufficient permissions for command (status = 5)
    /// </summary>
    public const int PermissionDenied = 5;

    /// <summary>
    /// Command not supported by device (status = 6)
    /// </summary>
    public const int UnsupportedCommand = 6;

    /// <summary>
    /// Command blocked by safety constraints (status = 7)
    /// </summary>
    public const int SafetyViolation = 7;

    /// <summary>
    /// Some operations succeeded, some failed (status = 8)
    /// </summary>
    public const int PartialSuccess = 8;

    /// <summary>
    /// Device, output, or data not found (status = 9)
    /// </summary>
    public const int NotFound = 9;

    /// <summary>
    /// Insufficient resources such as out of memory (status = 10)
    /// </summary>
    public const int ResourceExhausted = 10;

    /// <summary>
    /// Gets a human-readable description for a status code.
    /// </summary>
    public static string GetDescription(int code) => code switch
    {
        Success => "Command executed successfully",
        ValidationFailed => "Parameter validation failed",
        DeviceBusy => "Device is busy, cannot execute command",
        Timeout => "Command execution timeout",
        HardwareError => "Hardware rejected command or execution failed",
        PermissionDenied => "Insufficient permissions for command",
        UnsupportedCommand => "Command not supported by device",
        SafetyViolation => "Command blocked by safety constraints",
        PartialSuccess => "Some operations succeeded, some failed",
        NotFound => "Device, output, or data not found",
        ResourceExhausted => "Insufficient resources (OOM)",
        _ => "Unknown status"
    };
}