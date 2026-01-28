namespace Weda.SubNode.Abstractions.Commands.Contracts;


/// <summary>
/// Command response status codes.
/// </summary>
/// <remarks>
/// Status codes follow the specification:
/// 0 = SUCCESS: Command executed successfully, all data retrieved
/// 1 = PARTIAL_SUCCESS: Command completed with data gaps or quality issues
/// 2 = INVALID_TIME_RANGE: Time range is invalid or exceeds retention period
/// 3 = NO_DATA_AVAILABLE: No data found for specified time range
/// 4 = STORAGE_ERROR: Local storage unavailable or corrupted
/// 5 = TIMEOUT: Command execution exceeded timeout
/// 6 = PERMISSION_DENIED: Insufficient permissions for data access
/// 7 = RESOURCE_EXHAUSTED: Device resources insufficient for query
/// </remarks>
public static class CommandResponseStatusCode
{
    /// <summary>
    /// Command executed successfully, all data retrieved (status = 0)
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Command completed with data gaps or quality issues (status = 1)
    /// </summary>
    public const int PartialSuccess = 1;

    /// <summary>
    /// Invalid input arguments (status = 2)
    /// </summary>
    public const int InvalidInputArguments = 2;

    /// <summary>
    /// No data found for specified conditions (status = 3)
    /// </summary>
    public const int NoDataAvailable = 3;

    /// <summary>
    /// Local storage unavailable or corrupted (status = 4)
    /// </summary>
    public const int StorageError = 4;

    /// <summary>
    /// Command execution exceeded timeout (status = 5)
    /// </summary>
    public const int Timeout = 5;

    /// <summary>
    /// Insufficient permissions for data access (status = 6)
    /// </summary>
    public const int PermissionDenied = 6;

    /// <summary>
    /// Device resources insufficient for command execution (status = 7)
    /// </summary>
    public const int ResourceExhausted = 7;

    /// <summary>
    /// Unexpected error (status = 500)
    /// </summary>
    public const int UnexptectedError = 500;

    /// <summary>
    /// Gets a human-readable description for a status code.
    /// </summary>
    public static string GetDescription(int code) => code switch
    {
        Success => "Command executed successfully, all data retrieved",
        PartialSuccess => "Command completed with data gaps or quality issues",
        InvalidInputArguments => "Invalid input arguments",
        NoDataAvailable => "No data found for specified conditions",
        StorageError => "Local storage unavailable or corrupted",
        Timeout => "Command execution exceeded timeout",
        PermissionDenied => "Insufficient permissions for data access",
        ResourceExhausted => "Device resources insufficient for command execution",
        UnexptectedError => "Unexpected error",
        _ => "Unknown status"
    };
}