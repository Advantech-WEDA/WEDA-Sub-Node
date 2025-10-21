namespace Weda.SubNode.Abstractions.Communication;

/// <summary>
/// Common connection settings for all communication types
/// </summary>
public class ConnectionSettings
{
    /// <summary>
    /// Maximum number of retry attempts when connection fails
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay in milliseconds before first retry
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Whether to use exponential backoff for retries
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Read timeout in milliseconds
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// Write timeout in milliseconds
    /// </summary>
    public int WriteTimeoutMs { get; set; } = 3000;
}
