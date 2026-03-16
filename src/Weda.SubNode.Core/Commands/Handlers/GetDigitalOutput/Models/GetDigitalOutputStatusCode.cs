namespace Weda.SubNode.Core.Commands.Handlers.GetDigitalOutput.Models;

/// <summary>
/// Status codes for do.get command responses.
/// </summary>
public static class GetDigitalOutputStatusCode
{
    /// <summary>All requested outputs were read successfully.</summary>
    public const int Success = 200;

    /// <summary>Some outputs were read successfully, but others failed.</summary>
    public const int PartialSuccess = 206;

    /// <summary>Invalid request parameters.</summary>
    public const int InvalidArguments = 400;

    /// <summary>No device found matching the specified name.</summary>
    public const int DeviceNotFound = 404;

    /// <summary>Device does not support digital output reading.</summary>
    public const int NotSupported = 405;

    /// <summary>Failed to read outputs due to device error.</summary>
    public const int ExecutionFailed = 500;

    /// <summary>Operation timed out.</summary>
    public const int Timeout = 504;
}
