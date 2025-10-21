namespace Weda.SubNode.Core.Devices.Retry;

/// <summary>
/// Defines retry behavior and backoff strategy.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>
    /// Gets the maximum number of retry attempts.
    /// </summary>
    public required int MaxRetries { get; init; }

    /// <summary>
    /// Gets the backoff strategy.
    /// </summary>
    public required BackoffStrategy BackoffStrategy { get; init; }

    /// <summary>
    /// Gets the initial delay before the first retry.
    /// </summary>
    public required TimeSpan InitialDelay { get; init; }

    /// <summary>
    /// Gets the maximum delay between retries.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets the backoff multiplier for exponential strategies.
    /// Default is 2.0 (doubles each time).
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Gets whether to add jitter to retry delays to avoid thundering herd.
    /// </summary>
    public bool UseJitter { get; init; } = true;

    /// <summary>
    /// Gets the maximum jitter percentage (0-1).
    /// Default is 0.2 (±20% randomness).
    /// </summary>
    public double JitterFactor { get; init; } = 0.2;

    /// <summary>
    /// Predefined retry policy with no retries.
    /// </summary>
    public static RetryPolicy None => new()
    {
        MaxRetries = 0,
        BackoffStrategy = BackoffStrategy.Constant,
        InitialDelay = TimeSpan.Zero
    };

    /// <summary>
    /// Predefined retry policy with immediate retries.
    /// </summary>
    public static RetryPolicy Immediate => new()
    {
        MaxRetries = 3,
        BackoffStrategy = BackoffStrategy.Constant,
        InitialDelay = TimeSpan.FromMilliseconds(100)
    };

    /// <summary>
    /// Predefined retry policy with linear backoff.
    /// </summary>
    public static RetryPolicy Linear => new()
    {
        MaxRetries = 5,
        BackoffStrategy = BackoffStrategy.Linear,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// Predefined retry policy with exponential backoff.
    /// </summary>
    public static RetryPolicy Exponential => new()
    {
        MaxRetries = 7,
        BackoffStrategy = BackoffStrategy.Exponential,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromMinutes(5),
        BackoffMultiplier = 2.0
    };

    /// <summary>
    /// Predefined retry policy with Fibonacci backoff.
    /// </summary>
    public static RetryPolicy Fibonacci => new()
    {
        MaxRetries = 8,
        BackoffStrategy = BackoffStrategy.Fibonacci,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromMinutes(3)
    };
}

/// <summary>
/// Backoff strategy for retry delays.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>
    /// Constant delay between retries.
    /// </summary>
    Constant = 0,

    /// <summary>
    /// Linear increase: delay = initialDelay * attempt.
    /// </summary>
    Linear = 1,

    /// <summary>
    /// Exponential increase: delay = initialDelay * (multiplier ^ attempt).
    /// </summary>
    Exponential = 2,

    /// <summary>
    /// Fibonacci sequence: delay follows Fibonacci numbers.
    /// </summary>
    Fibonacci = 3
}
