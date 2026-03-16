namespace Weda.SubNode.Core.Commands.Handlers.GetAnalogInput.Models;

/// <summary>
/// Status codes for ai.get command responses.
/// </summary>
public static class GetAnalogInputStatusCode
{
    /// <summary>All requested inputs were read successfully.</summary>
    public const int Success = 200;

    /// <summary>Some inputs were read successfully, but others failed.</summary>
    public const int PartialSuccess = 206;

    /// <summary>Invalid request parameters.</summary>
    public const int InvalidArguments = 400;

    /// <summary>No device found matching the specified name.</summary>
    public const int DeviceNotFound = 404;

    /// <summary>Device does not support analog input reading.</summary>
    public const int NotSupported = 405;

    /// <summary>Failed to read inputs due to device error.</summary>
    public const int ExecutionFailed = 500;

    /// <summary>Operation timed out.</summary>
    public const int Timeout = 504;
}
