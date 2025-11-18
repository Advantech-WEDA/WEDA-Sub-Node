using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Weda.SubNode.Core.Policies;

/// <summary>
/// Polly resilience policies for device and cloud service connections.
/// Provides retry, circuit breaker, and timeout policies with comprehensive logging.
/// </summary>
public static class ConnectionPolicies
{
    /// <summary>
    /// Creates a unified resilience pipeline for all connection types (Device/Cloud/Reconnection):
    /// - Retry: Unlimited retries with exponential backoff (1s, 2s, 4s, ..., max 60s) + jitter
    /// - No Circuit Breaker (persistent connection attempts)
    /// - Timeout: 30 seconds per connection attempt
    ///
    /// This single policy is used for:
    /// - Initial device connections (TCP/MQTT)
    /// - Initial cloud service connections
    /// - Runtime reconnection attempts
    /// </summary>
    public static ResiliencePipeline<bool> CreateConnectionPipeline(
        ILogger logger,
        ConnectionPolicyOptions? options = null)
    {
        options ??= ConnectionPolicyOptions.Default;

        // Use RetryPolicyFactory for unlimited retry logic (AlwaysRetry)
        var retryPipeline = RetryPolicyFactory.CreateAlwaysRetryBool(
            logger: logger,
            initialDelay: options.InitialDelay,
            maxDelay: options.MaxDelay);

        return new ResiliencePipelineBuilder<bool>()
            .AddPipeline(retryPipeline)
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.Timeout,
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "Connection attempt timed out after {Timeout}s",
                        args.Timeout.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Alias for CreateConnectionPipeline. Use for device connections.
    /// </summary>
    public static ResiliencePipeline<bool> CreateDeviceConnectionPipeline(
        ILogger logger,
        ConnectionPolicyOptions? options = null)
        => CreateConnectionPipeline(logger, options);

    /// <summary>
    /// Alias for CreateConnectionPipeline. Use for cloud connections.
    /// </summary>
    public static ResiliencePipeline<bool> CreateCloudConnectionPipeline(
        ILogger logger,
        ConnectionPolicyOptions? options = null)
        => CreateConnectionPipeline(logger, options);

    /// <summary>
    /// Alias for CreateConnectionPipeline. Use for reconnection.
    /// </summary>
    public static ResiliencePipeline<bool> CreateReconnectionPipeline(
        ILogger logger,
        ConnectionPolicyOptions? options = null)
        => CreateConnectionPipeline(logger, options);

    /// <summary>
    /// Creates a general-purpose resilience pipeline for device operations with:
    /// - Retry: 3 attempts with exponential backoff (500ms, 1s, 2s) + jitter
    /// - Circuit Breaker: Opens after 50% failure rate (min 4 calls), breaks for 15s
    /// - Timeout: 10 seconds per operation
    /// Suitable for telemetry sending, command execution, etc.
    /// </summary>
    public static ResiliencePipeline CreateGeneralOperationPipeline(
        ILogger logger,
        ConnectionPolicyOptions? options = null)
    {
        options ??= new ConnectionPolicyOptions
        {
            MaxRetryAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(500),
            MaxDelay = TimeSpan.FromSeconds(5),
            Timeout = TimeSpan.FromSeconds(10),
            CircuitBreakerFailureRatio = 0.5,
            CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30),
            CircuitBreakerMinThroughput = 4,
            CircuitBreakerBreakDuration = TimeSpan.FromSeconds(15)
        };

        // Use RetryPolicyFactory for retry logic
        var retryPipeline = RetryPolicyFactory.CreateNTimeRetry(
            logger: logger,
            maxRetries: options.MaxRetryAttempts,
            initialDelay: options.InitialDelay,
            maxDelay: options.MaxDelay);

        return new ResiliencePipelineBuilder()
            .AddPipeline(retryPipeline)
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = options.CircuitBreakerFailureRatio,
                SamplingDuration = options.CircuitBreakerSamplingDuration,
                MinimumThroughput = options.CircuitBreakerMinThroughput,
                BreakDuration = options.CircuitBreakerBreakDuration,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnOpened = args =>
                {
                    logger.LogError(
                        "Circuit breaker OPENED: Operations suspended for {BreakDuration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("Circuit breaker CLOSED: Operations resumed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    logger.LogInformation("Circuit breaker HALF-OPEN: Testing operation...");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.Timeout,
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "Operation timed out after {Timeout}s",
                        args.Timeout.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}

/// <summary>
/// Configuration options for connection resilience policies.
/// </summary>
public sealed class ConnectionPolicyOptions
{
    /// <summary>
    /// Maximum number of retry attempts (default: 3 for device, 5 for cloud)
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial retry delay before exponential backoff (default: 1 second for device, 500ms for cloud)
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum retry delay cap - delay = min(MaxDelay, exponential_backoff(attempt))
    /// This ensures retry delays have an upper bound (default: 10 seconds)
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Connection timeout per attempt (default: 30 seconds for device, 60 seconds for cloud)
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Circuit breaker failure ratio threshold (default: 0.5 = 50%)
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Circuit breaker sampling window (default: 30 seconds)
    /// </summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Circuit breaker minimum throughput before opening (default: 4 calls)
    /// </summary>
    public int CircuitBreakerMinThroughput { get; set; } = 4;

    /// <summary>
    /// Circuit breaker break duration (default: 20 seconds)
    /// </summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Default policy for all connection types (Device/Cloud/Reconnection):
    /// - AlwaysRetry: Unlimited retries with 1s initial delay, 60s max delay
    /// - Timeout: 30 seconds per attempt
    /// - No Circuit Breaker: Persistent connection attempts
    /// </summary>
    public static ConnectionPolicyOptions Default => new()
    {
        MaxRetryAttempts = int.MaxValue, // Unlimited (AlwaysRetry)
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(60),
        Timeout = TimeSpan.FromSeconds(30),
        CircuitBreakerFailureRatio = 0.5,
        CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30),
        CircuitBreakerMinThroughput = 4,
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(20)
    };

    /// <summary>
    /// Alias for Default. All connection types now use the same policy.
    /// </summary>
    public static ConnectionPolicyOptions CloudDefault => Default;
}
