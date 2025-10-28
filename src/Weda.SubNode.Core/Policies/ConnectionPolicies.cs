using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Weda.SubNode.Core.Policies;

/// <summary>
/// Polly resilience policies for device and cloud service connections.
/// Provides retry, circuit breaker, and timeout policies with comprehensive logging.
/// </summary>
public static class ConnectionPolicies
{
    /// <summary>
    /// Creates a resilience pipeline for device connections (TCP/MQTT) with:
    /// - Retry: 3 attempts with exponential backoff (1s, 2s, 4s) + jitter
    /// - Circuit Breaker: Opens after 50% failure rate (min 4 calls), breaks for 20s
    /// - Timeout: 30 seconds per connection attempt
    /// </summary>
    public static ResiliencePipeline<bool> CreateDeviceConnectionPipeline(
        ILogger logger,
        ConnectionPolicyOptions? options = null)
    {
        options ??= ConnectionPolicyOptions.Default;

        return new ResiliencePipelineBuilder<bool>()
            .AddRetry(new RetryStrategyOptions<bool>
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = options.InitialDelay,
                UseJitter = true,
                MaxDelay = options.MaxDelay,
                ShouldHandle = new PredicateBuilder<bool>()
                    .HandleResult(false) // Retry when connection fails (returns false)
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Connection attempt {AttemptNumber}/{MaxAttempts} failed, retrying after {DelayDuration}ms... (Outcome: {Outcome})",
                        args.AttemptNumber,
                        options.MaxRetryAttempts,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result ? "Success" : "Failure");
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<bool>
            {
                FailureRatio = options.CircuitBreakerFailureRatio,
                SamplingDuration = options.CircuitBreakerSamplingDuration,
                MinimumThroughput = options.CircuitBreakerMinThroughput,
                BreakDuration = options.CircuitBreakerBreakDuration,
                ShouldHandle = new PredicateBuilder<bool>()
                    .HandleResult(false)
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnOpened = args =>
                {
                    logger.LogError(
                        "Circuit breaker OPENED: Connection attempts suspended for {BreakDuration}s due to {FailureRate:P0} failure rate",
                        args.BreakDuration.TotalSeconds,
                        options.CircuitBreakerFailureRatio);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("Circuit breaker CLOSED: Connection attempts resumed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    logger.LogInformation("Circuit breaker HALF-OPEN: Testing connection...");
                    return ValueTask.CompletedTask;
                }
            })
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
    /// Creates a resilience pipeline for cloud service connections with more aggressive retry:
    /// - Retry: 5 attempts with exponential backoff (500ms, 1s, 2s, 4s, 8s) + jitter
    /// - Circuit Breaker: Opens after 60% failure rate (min 3 calls), breaks for 30s
    /// - Timeout: 60 seconds per connection attempt
    /// </summary>
    public static ResiliencePipeline<bool> CreateCloudConnectionPipeline(
        ILogger logger,
        ConnectionPolicyOptions? options = null)
    {
        options ??= ConnectionPolicyOptions.CloudDefault;

        return new ResiliencePipelineBuilder<bool>()
            .AddRetry(new RetryStrategyOptions<bool>
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = options.InitialDelay,
                UseJitter = true,
                MaxDelay = options.MaxDelay,
                ShouldHandle = new PredicateBuilder<bool>()
                    .HandleResult(false)
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Cloud connection attempt {AttemptNumber}/{MaxAttempts} failed, retrying after {DelayDuration}ms...",
                        args.AttemptNumber,
                        options.MaxRetryAttempts,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<bool>
            {
                FailureRatio = options.CircuitBreakerFailureRatio,
                SamplingDuration = options.CircuitBreakerSamplingDuration,
                MinimumThroughput = options.CircuitBreakerMinThroughput,
                BreakDuration = options.CircuitBreakerBreakDuration,
                ShouldHandle = new PredicateBuilder<bool>()
                    .HandleResult(false)
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnOpened = args =>
                {
                    logger.LogError(
                        "Cloud circuit breaker OPENED: Suspended for {BreakDuration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation("Cloud circuit breaker CLOSED: Resumed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    logger.LogInformation("Cloud circuit breaker HALF-OPEN: Testing...");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = options.Timeout,
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "Cloud connection timed out after {Timeout}s",
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
    /// Maximum retry delay cap (default: 10 seconds)
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
    /// Default policy for device connections
    /// </summary>
    public static ConnectionPolicyOptions Default => new()
    {
        MaxRetryAttempts = 3,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(10),
        Timeout = TimeSpan.FromSeconds(30),
        CircuitBreakerFailureRatio = 0.5,
        CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30),
        CircuitBreakerMinThroughput = 4,
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(20)
    };

    /// <summary>
    /// Default policy for cloud service connections (more aggressive retry)
    /// </summary>
    public static ConnectionPolicyOptions CloudDefault => new()
    {
        MaxRetryAttempts = 5,
        InitialDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(15),
        Timeout = TimeSpan.FromSeconds(60),
        CircuitBreakerFailureRatio = 0.6,
        CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30),
        CircuitBreakerMinThroughput = 3,
        CircuitBreakerBreakDuration = TimeSpan.FromSeconds(30)
    };
}
