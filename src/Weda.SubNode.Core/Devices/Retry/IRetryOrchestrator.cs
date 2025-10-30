using ErrorOr;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core.Devices.Retry;

/// <summary>
/// Orchestrates retry logic and circuit breaker pattern for device operations.
/// Provides thread-safe retry mechanisms with configurable backoff strategies.
/// </summary>
public interface IRetryOrchestrator
{
    /// <summary>
    /// Gets the current circuit breaker state.
    /// </summary>
    CircuitState CurrentCircuitState { get; }

    /// <summary>
    /// Executes an operation with retry logic and circuit breaker protection.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="operationName">The name of the operation for logging and events.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation or an error.</returns>
    Task<ErrorOr<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation that returns ErrorOr with retry logic.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation or an error.</returns>
    Task<ErrorOr<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<ErrorOr<T>>> operation,
        string operationName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually resets the circuit breaker to closed state.
    /// </summary>
    void ResetCircuitBreaker();

    /// <summary>
    /// Gets statistics about retry attempts and circuit breaker state.
    /// </summary>
    RetryStatistics GetStatistics();

    /// <summary>
    /// Event raised before each retry attempt.
    /// </summary>
    event EventHandler<RetryAttemptEvent>? RetryAttempting;

    /// <summary>
    /// Event raised when the circuit breaker state changes.
    /// </summary>
    event EventHandler<CircuitBreakerStateChangedEvent>? CircuitBreakerStateChanged;
}

/// <summary>
/// Statistics about retry operations.
/// </summary>
public sealed record RetryStatistics
{
    public required string DeviceId { get; init; }
    public required CircuitState CurrentCircuitState { get; init; }
    public required int TotalAttempts { get; init; }
    public required int SuccessfulAttempts { get; init; }
    public required int FailedAttempts { get; init; }
    public required int CircuitOpenCount { get; init; }
    public DateTimeOffset? LastCircuitOpenTime { get; init; }
    public DateTimeOffset? LastSuccessTime { get; init; }
    public DateTimeOffset? LastFailureTime { get; init; }
    public TimeSpan? TimeUntilCircuitReset { get; init; }
}
