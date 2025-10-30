using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event raised when the circuit breaker state changes.
/// </summary>
public sealed record CircuitBreakerStateChangedEvent(
    string DeviceId,
    string OperationName,
    CircuitState PreviousState,
    CircuitState CurrentState,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Gets the reason for the state change.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the time until the circuit can transition (e.g., from Open to HalfOpen).
    /// </summary>
    public TimeSpan? TimeUntilRetry { get; init; }
}
