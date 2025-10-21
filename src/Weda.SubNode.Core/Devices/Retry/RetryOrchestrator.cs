using System.Collections.Concurrent;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core.Devices.Retry;

/// <summary>
/// Thread-safe retry orchestrator with circuit breaker pattern.
/// Implements exponential backoff, jitter, and failure tracking.
/// </summary>
public sealed class RetryOrchestrator : IRetryOrchestrator
{
    private readonly string _deviceId;
    private readonly ILogger<RetryOrchestrator> _logger;
    private readonly RetryPolicy _retryPolicy;
    private readonly CircuitBreakerPolicy _circuitBreakerPolicy;
    private readonly Random _random = new();
    private readonly SemaphoreSlim _circuitLock = new(1, 1);

    // Circuit breaker state
    private CircuitState _circuitState = CircuitState.Closed;
    private DateTimeOffset _circuitOpenedAt;
    private int _consecutiveFailures;
    private int _halfOpenSuccesses;
    private readonly ConcurrentQueue<DateTimeOffset> _recentFailures = new();

    // Statistics
    private long _totalAttempts;
    private long _successfulAttempts;
    private long _failedAttempts;
    private long _circuitOpenCount;
    private DateTimeOffset? _lastSuccessTime;
    private DateTimeOffset? _lastFailureTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryOrchestrator"/> class.
    /// </summary>
    public RetryOrchestrator(
        string deviceId,
        ILogger<RetryOrchestrator> logger,
        RetryPolicy? retryPolicy = null,
        CircuitBreakerPolicy? circuitBreakerPolicy = null)
    {
        _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retryPolicy = retryPolicy ?? RetryPolicy.Exponential;
        _circuitBreakerPolicy = circuitBreakerPolicy ?? CircuitBreakerPolicy.Default;
    }

    /// <inheritdoc/>
    public CircuitState CurrentCircuitState => _circuitState;

    /// <inheritdoc/>
    public event EventHandler<RetryAttemptEvent>? RetryAttempting;

    /// <inheritdoc/>
    public event EventHandler<CircuitBreakerStateChangedEvent>? CircuitBreakerStateChanged;

    /// <inheritdoc/>
    public async Task<ErrorOr<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async ct =>
        {
            try
            {
                var result = await operation(ct);
                return ErrorOrFactory.From(result);
            }
            catch (Exception ex)
            {
                return Error.Failure(
                    code: "Operation.Failed",
                    description: $"Operation '{operationName}' failed: {ex.Message}");
            }
        }, operationName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ErrorOr<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<ErrorOr<T>>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        // Check circuit breaker state
        var circuitCheck = await CheckCircuitBreakerAsync(operationName);
        if (circuitCheck.IsError)
        {
            return circuitCheck.Errors;
        }

        Exception? lastException = null;
        ErrorOr<T> lastResult = default;

        for (int attempt = 0; attempt <= _retryPolicy.MaxRetries; attempt++)
        {
            Interlocked.Increment(ref _totalAttempts);

            try
            {
                // Execute the operation
                lastResult = await operation(cancellationToken);

                if (!lastResult.IsError)
                {
                    // Success
                    await HandleSuccessAsync(operationName);
                    return lastResult;
                }

                // Operation returned error
                _logger.LogWarning(
                    "Operation '{OperationName}' returned error on attempt {Attempt}/{MaxAttempts}: {Errors}",
                    operationName, attempt + 1, _retryPolicy.MaxRetries + 1,
                    string.Join(", ", lastResult.Errors.Select(e => e.Description)));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Operation '{OperationName}' was cancelled", operationName);
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex,
                    "Operation '{OperationName}' threw exception on attempt {Attempt}/{MaxAttempts}",
                    operationName, attempt + 1, _retryPolicy.MaxRetries + 1);
            }

            // Check if we should retry
            if (attempt < _retryPolicy.MaxRetries)
            {
                var delay = CalculateDelay(attempt);

                // Emit retry event
                RetryAttempting?.Invoke(this, new RetryAttemptEvent(
                    DeviceId: _deviceId,
                    OperationName: operationName,
                    AttemptNumber: attempt + 1,
                    MaxAttempts: _retryPolicy.MaxRetries + 1,
                    Delay: delay,
                    Timestamp: DateTimeOffset.UtcNow)
                {
                    LastException = lastException,
                    Reason = lastResult.IsError
                        ? string.Join(", ", lastResult.Errors.Select(e => e.Description))
                        : lastException?.Message
                });

                _logger.LogDebug(
                    "Retrying operation '{OperationName}' after {Delay}ms (attempt {Attempt}/{MaxAttempts})",
                    operationName, delay.TotalMilliseconds, attempt + 2, _retryPolicy.MaxRetries + 1);

                await Task.Delay(delay, cancellationToken);
            }
        }

        // All retries exhausted
        await HandleFailureAsync(operationName, lastException);

        if (lastException != null)
        {
            return Errors.Device.OperationFailedAfterRetries(
                operationName,
                _retryPolicy.MaxRetries + 1,
                lastException);
        }

        return lastResult.IsError
            ? lastResult.Errors
            : Error.Failure(code: "Operation.Failed", description: "Operation failed after all retries");
    }

    /// <inheritdoc/>
    public void ResetCircuitBreaker()
    {
        _circuitLock.Wait();
        try
        {
            if (_circuitState != CircuitState.Closed)
            {
                var previousState = _circuitState;
                _circuitState = CircuitState.Closed;
                _consecutiveFailures = 0;
                _halfOpenSuccesses = 0;
                _recentFailures.Clear();

                _logger.LogInformation(
                    "Circuit breaker manually reset for device {DeviceId}",
                    _deviceId);

                CircuitBreakerStateChanged?.Invoke(this, new CircuitBreakerStateChangedEvent(
                    DeviceId: _deviceId,
                    OperationName: "Manual Reset",
                    PreviousState: previousState,
                    CurrentState: CircuitState.Closed,
                    Timestamp: DateTimeOffset.UtcNow)
                {
                    Reason = "Manual reset"
                });
            }
        }
        finally
        {
            _circuitLock.Release();
        }
    }

    /// <inheritdoc/>
    public RetryStatistics GetStatistics()
    {
        TimeSpan? timeUntilReset = null;
        if (_circuitState == CircuitState.Open)
        {
            var elapsed = DateTimeOffset.UtcNow - _circuitOpenedAt;
            var remaining = _circuitBreakerPolicy.OpenDuration - elapsed;
            timeUntilReset = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return new RetryStatistics
        {
            DeviceId = _deviceId,
            CurrentCircuitState = _circuitState,
            TotalAttempts = (int)Interlocked.Read(ref _totalAttempts),
            SuccessfulAttempts = (int)Interlocked.Read(ref _successfulAttempts),
            FailedAttempts = (int)Interlocked.Read(ref _failedAttempts),
            CircuitOpenCount = (int)Interlocked.Read(ref _circuitOpenCount),
            LastCircuitOpenTime = _circuitState == CircuitState.Open ? _circuitOpenedAt : null,
            LastSuccessTime = _lastSuccessTime,
            LastFailureTime = _lastFailureTime,
            TimeUntilCircuitReset = timeUntilReset
        };
    }

    private async Task<ErrorOr<Success>> CheckCircuitBreakerAsync(string operationName)
    {
        if (!_circuitBreakerPolicy.Enabled)
        {
            return Result.Success;
        }

        await _circuitLock.WaitAsync();
        try
        {
            switch (_circuitState)
            {
                case CircuitState.Open:
                    var elapsed = DateTimeOffset.UtcNow - _circuitOpenedAt;
                    if (elapsed >= _circuitBreakerPolicy.OpenDuration)
                    {
                        // Transition to half-open
                        await TransitionCircuitStateAsync(
                            operationName,
                            CircuitState.HalfOpen,
                            "Open duration elapsed, testing recovery");
                        return Result.Success;
                    }
                    else
                    {
                        var remaining = _circuitBreakerPolicy.OpenDuration - elapsed;
                        return Errors.Device.CircuitBreakerOpen(operationName, remaining);
                    }

                case CircuitState.HalfOpen:
                case CircuitState.Closed:
                    return Result.Success;

                default:
                    return Result.Success;
            }
        }
        finally
        {
            _circuitLock.Release();
        }
    }

    private async Task HandleSuccessAsync(string operationName)
    {
        Interlocked.Increment(ref _successfulAttempts);
        _lastSuccessTime = DateTimeOffset.UtcNow;

        if (!_circuitBreakerPolicy.Enabled)
        {
            return;
        }

        await _circuitLock.WaitAsync();
        try
        {
            if (_circuitState == CircuitState.HalfOpen)
            {
                _halfOpenSuccesses++;
                if (_halfOpenSuccesses >= _circuitBreakerPolicy.SuccessThreshold)
                {
                    await TransitionCircuitStateAsync(
                        operationName,
                        CircuitState.Closed,
                        $"Success threshold reached ({_halfOpenSuccesses} successes)");
                    _consecutiveFailures = 0;
                    _halfOpenSuccesses = 0;
                }
            }
            else if (_circuitState == CircuitState.Closed)
            {
                _consecutiveFailures = 0;
            }
        }
        finally
        {
            _circuitLock.Release();
        }
    }

    private async Task HandleFailureAsync(string operationName, Exception? exception)
    {
        Interlocked.Increment(ref _failedAttempts);
        _lastFailureTime = DateTimeOffset.UtcNow;

        if (!_circuitBreakerPolicy.Enabled)
        {
            return;
        }

        await _circuitLock.WaitAsync();
        try
        {
            _consecutiveFailures++;
            _recentFailures.Enqueue(DateTimeOffset.UtcNow);

            // Clean up old failures outside sampling window
            var cutoff = DateTimeOffset.UtcNow - _circuitBreakerPolicy.SamplingDuration;
            while (_recentFailures.TryPeek(out var oldest) && oldest < cutoff)
            {
                _recentFailures.TryDequeue(out _);
            }

            if (_circuitState == CircuitState.HalfOpen)
            {
                // Failed in half-open state, reopen circuit
                await TransitionCircuitStateAsync(
                    operationName,
                    CircuitState.Open,
                    $"Failed in half-open state: {exception?.Message ?? "Operation failed"}");
                _halfOpenSuccesses = 0;
                Interlocked.Increment(ref _circuitOpenCount);
                _circuitOpenedAt = DateTimeOffset.UtcNow;
            }
            else if (_circuitState == CircuitState.Closed)
            {
                // Check if we should open the circuit
                if (_recentFailures.Count >= _circuitBreakerPolicy.FailureThreshold)
                {
                    await TransitionCircuitStateAsync(
                        operationName,
                        CircuitState.Open,
                        $"Failure threshold exceeded ({_recentFailures.Count} failures in {_circuitBreakerPolicy.SamplingDuration.TotalSeconds}s)");
                    Interlocked.Increment(ref _circuitOpenCount);
                    _circuitOpenedAt = DateTimeOffset.UtcNow;
                }
            }
        }
        finally
        {
            _circuitLock.Release();
        }
    }

    private async Task TransitionCircuitStateAsync(
        string operationName,
        CircuitState newState,
        string reason)
    {
        var previousState = _circuitState;
        _circuitState = newState;

        _logger.LogWarning(
            "Circuit breaker state changed from {PreviousState} to {NewState} for device {DeviceId}: {Reason}",
            previousState, newState, _deviceId, reason);

        TimeSpan? timeUntilRetry = newState == CircuitState.Open
            ? _circuitBreakerPolicy.OpenDuration
            : null;

        CircuitBreakerStateChanged?.Invoke(this, new CircuitBreakerStateChangedEvent(
            DeviceId: _deviceId,
            OperationName: operationName,
            PreviousState: previousState,
            CurrentState: newState,
            Timestamp: DateTimeOffset.UtcNow)
        {
            Reason = reason,
            TimeUntilRetry = timeUntilRetry
        });

        await Task.CompletedTask;
    }

    private TimeSpan CalculateDelay(int attemptNumber)
    {
        double baseDelay = _retryPolicy.InitialDelay.TotalMilliseconds;
        double calculatedDelay;

        switch (_retryPolicy.BackoffStrategy)
        {
            case BackoffStrategy.Constant:
                calculatedDelay = baseDelay;
                break;

            case BackoffStrategy.Linear:
                calculatedDelay = baseDelay * (attemptNumber + 1);
                break;

            case BackoffStrategy.Exponential:
                calculatedDelay = baseDelay * Math.Pow(_retryPolicy.BackoffMultiplier, attemptNumber);
                break;

            case BackoffStrategy.Fibonacci:
                calculatedDelay = baseDelay * Fibonacci(attemptNumber + 1);
                break;

            default:
                calculatedDelay = baseDelay;
                break;
        }

        // Apply max delay cap
        calculatedDelay = Math.Min(calculatedDelay, _retryPolicy.MaxDelay.TotalMilliseconds);

        // Apply jitter if enabled
        if (_retryPolicy.UseJitter)
        {
            var jitterRange = calculatedDelay * _retryPolicy.JitterFactor;
            var jitter = (_random.NextDouble() * 2 - 1) * jitterRange; // Random value between -jitterRange and +jitterRange
            calculatedDelay = Math.Max(0, calculatedDelay + jitter);
        }

        return TimeSpan.FromMilliseconds(calculatedDelay);
    }

    private static int Fibonacci(int n)
    {
        if (n <= 1) return n;
        int a = 0, b = 1;
        for (int i = 2; i <= n; i++)
        {
            int temp = a + b;
            a = b;
            b = temp;
        }
        return b;
    }
}
