namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Options for connection management and retry logic (Communication Layer).
/// This is the simplified configuration for developers. For advanced Polly configuration,
/// use ConnectionPolicyOptions in Weda.SubNode.Core.Policies.
/// </summary>
public class ConnectionOptions
{
    /// <summary>
    /// Maximum number of retry attempts for connection operations.
    /// Default -1 representing always retry policy (unlimited retries).
    /// Set to a positive number to limit retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = -1;

    /// <summary>
    /// Initial retry delay in milliseconds (exponential backoff).
    /// The delay doubles after each attempt up to MaxRetryDelayMs.
    /// Default: 1000ms (1 second)
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum retry delay in milliseconds.
    /// The exponential backoff will not exceed this value.
    /// Default: 60000ms (60 seconds)
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 60000;

    /// <summary>
    /// Connection timeout in milliseconds per attempt.
    /// Default: 30000ms (30 seconds)
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Default connection options with unlimited retries.
    /// </summary>
    public static ConnectionOptions Default => new();

    /// <summary>
    /// Creates connection options with limited retry attempts.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts</param>
    /// <param name="initialDelayMs">Initial delay in milliseconds</param>
    /// <param name="timeoutMs">Timeout per attempt in milliseconds</param>
    public static ConnectionOptions WithRetries(int maxRetries, int initialDelayMs = 1000, int timeoutMs = 30000) => new()
    {
        MaxRetryAttempts = maxRetries,
        RetryDelayMs = initialDelayMs,
        ConnectionTimeoutMs = timeoutMs
    };
}
