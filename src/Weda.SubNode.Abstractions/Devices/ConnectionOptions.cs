namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Options for connection management and retry logic
/// </summary>
public class ConnectionOptions
{
    /// <summary>
    /// Maximum number of retry attempts for connection operations
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial retry delay in milliseconds (exponential backoff)
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Default connection options
    /// </summary>
    public static ConnectionOptions Default => new();
}
