using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Weda.SubNode.Core.Policies;

/// <summary>
/// Factory for creating common retry policies using Polly.
/// Provides predefined retry strategies with sensible defaults.
/// </summary>
public static class RetryPolicyFactory
{
    /// <summary>
    /// Creates an "Always Retry" policy that retries indefinitely with exponential backoff and upper bound.
    /// Delay formula: min(MaxDelay, InitialDelay * 2^attempt) + jitter
    ///
    /// Example delays with InitialDelay=1s, MaxDelay=60s:
    /// - Attempt 1: ~1s
    /// - Attempt 2: ~2s
    /// - Attempt 3: ~4s
    /// - Attempt 4: ~8s
    /// - Attempt 5: ~16s
    /// - Attempt 6: ~32s
    /// - Attempt 7+: ~60s (capped at MaxDelay)
    /// </summary>
    /// <param name="logger">Logger for retry events</param>
    /// <param name="initialDelay">Initial retry delay (default: 1 second)</param>
    /// <param name="maxDelay">Maximum retry delay upper bound (default: 60 seconds)</param>
    /// <returns>Resilience pipeline that always retries with bounded exponential backoff</returns>
    public static ResiliencePipeline CreateAlwaysRetry(
        ILogger logger,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null)
    {
        var initDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        var maxDel = maxDelay ?? TimeSpan.FromSeconds(60);

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = int.MaxValue, // Always retry
                BackoffType = DelayBackoffType.Exponential,
                Delay = initDelay,
                MaxDelay = maxDel, // Upper bound to prevent infinite growth
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Retry attempt {AttemptNumber}, retrying after {DelayDuration}ms (MaxDelay={MaxDelay}s)",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        maxDel.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates an "Always Retry" policy for operations returning bool.
    /// Retries when result is false or exception is thrown.
    /// </summary>
    public static ResiliencePipeline<bool> CreateAlwaysRetryBool(
        ILogger logger,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null)
    {
        var initDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        var maxDel = maxDelay ?? TimeSpan.FromSeconds(60);

        return new ResiliencePipelineBuilder<bool>()
            .AddRetry(new RetryStrategyOptions<bool>
            {
                MaxRetryAttempts = int.MaxValue,
                BackoffType = DelayBackoffType.Exponential,
                Delay = initDelay,
                MaxDelay = maxDel,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<bool>()
                    .HandleResult(false) // Retry on failure
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Retry attempt {AttemptNumber}, retrying after {DelayDuration}ms (Result={Result}, MaxDelay={MaxDelay}s)",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result,
                        maxDel.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates an "N-Time Retry" policy with exponential backoff.
    /// Retries exactly N times before giving up.
    /// </summary>
    /// <param name="logger">Logger for retry events</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="initialDelay">Initial retry delay (default: 500ms)</param>
    /// <param name="maxDelay">Maximum retry delay (default: 30s)</param>
    public static ResiliencePipeline CreateNTimeRetry(
        ILogger logger,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null)
    {
        var initDelay = initialDelay ?? TimeSpan.FromMilliseconds(500);
        var maxDel = maxDelay ?? TimeSpan.FromSeconds(30);

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = initDelay,
                MaxDelay = maxDel,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Retry attempt {AttemptNumber}/{MaxAttempts}, retrying after {DelayDuration}ms",
                        args.AttemptNumber,
                        maxRetries,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates an "N-Time Retry" policy for operations returning bool.
    /// </summary>
    public static ResiliencePipeline<bool> CreateNTimeRetryBool(
        ILogger logger,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null)
    {
        var initDelay = initialDelay ?? TimeSpan.FromMilliseconds(500);
        var maxDel = maxDelay ?? TimeSpan.FromSeconds(30);

        return new ResiliencePipelineBuilder<bool>()
            .AddRetry(new RetryStrategyOptions<bool>
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = initDelay,
                MaxDelay = maxDel,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<bool>()
                    .HandleResult(false)
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Retry attempt {AttemptNumber}/{MaxAttempts}, retrying after {DelayDuration}ms (Result={Result})",
                        args.AttemptNumber,
                        maxRetries,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Result);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a "Fast Retry" policy with minimal delays.
    /// Suitable for quick operations that usually succeed.
    /// </summary>
    public static ResiliencePipeline CreateFastRetry(
        ILogger logger,
        int maxRetries = 5)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromMilliseconds(100),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Fast retry attempt {AttemptNumber}/{MaxAttempts}",
                        args.AttemptNumber,
                        maxRetries);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a "Linear Backoff Retry" policy.
    /// Delay increases linearly: initialDelay, 2*initialDelay, 3*initialDelay, ...
    /// </summary>
    public static ResiliencePipeline CreateLinearRetry(
        ILogger logger,
        int maxRetries = 5,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null)
    {
        var initDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        var maxDel = maxDelay ?? TimeSpan.FromSeconds(30);

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Linear,
                Delay = initDelay,
                MaxDelay = maxDel,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Linear retry attempt {AttemptNumber}/{MaxAttempts}, delay={DelayDuration}ms",
                        args.AttemptNumber,
                        maxRetries,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a "No Retry" policy.
    /// Useful for operations that should not be retried.
    /// </summary>
    public static ResiliencePipeline CreateNoRetry()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 0
            })
            .Build();
    }

    /// <summary>
    /// Creates a custom retry policy with full control over all parameters.
    /// </summary>
    public static ResiliencePipeline CreateCustomRetry(
        ILogger logger,
        int maxRetries,
        DelayBackoffType backoffType,
        TimeSpan initialDelay,
        TimeSpan maxDelay,
        bool useJitter = true)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = backoffType,
                Delay = initialDelay,
                MaxDelay = maxDelay,
                UseJitter = useJitter,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Custom retry attempt {AttemptNumber}/{MaxAttempts}, delay={DelayDuration}ms",
                        args.AttemptNumber,
                        maxRetries,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}

/// <summary>
/// Predefined retry policy presets for common scenarios.
/// </summary>
public static class RetryPolicyPresets
{
    /// <summary>
    /// Quick retry for operations that usually succeed (5 retries, 100ms constant delay).
    /// </summary>
    public static RetryPolicyOptions Quick => new()
    {
        MaxRetries = 5,
        BackoffType = DelayBackoffType.Constant,
        InitialDelay = TimeSpan.FromMilliseconds(100),
        MaxDelay = TimeSpan.FromMilliseconds(100)
    };

    /// <summary>
    /// Standard retry for normal operations (3 retries, exponential backoff).
    /// </summary>
    public static RetryPolicyOptions Standard => new()
    {
        MaxRetries = 3,
        BackoffType = DelayBackoffType.Exponential,
        InitialDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// Aggressive retry for critical operations (10 retries, exponential backoff).
    /// </summary>
    public static RetryPolicyOptions Aggressive => new()
    {
        MaxRetries = 10,
        BackoffType = DelayBackoffType.Exponential,
        InitialDelay = TimeSpan.FromMilliseconds(100),
        MaxDelay = TimeSpan.FromSeconds(60)
    };

    /// <summary>
    /// Patient retry for long-running operations (5 retries, linear backoff).
    /// </summary>
    public static RetryPolicyOptions Patient => new()
    {
        MaxRetries = 5,
        BackoffType = DelayBackoffType.Linear,
        InitialDelay = TimeSpan.FromSeconds(2),
        MaxDelay = TimeSpan.FromMinutes(1)
    };

    /// <summary>
    /// Infinite retry for operations that must eventually succeed (unbounded, exponential with cap).
    /// </summary>
    public static RetryPolicyOptions Infinite => new()
    {
        MaxRetries = int.MaxValue,
        BackoffType = DelayBackoffType.Exponential,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(60)
    };
}

/// <summary>
/// Configuration options for retry policies.
/// </summary>
public sealed class RetryPolicyOptions
{
    public int MaxRetries { get; set; }
    public DelayBackoffType BackoffType { get; set; }
    public TimeSpan InitialDelay { get; set; }
    public TimeSpan MaxDelay { get; set; }
    public bool UseJitter { get; set; } = true;
}