namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Circuit breaker state.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed, requests are allowed through.
    /// </summary>
    Closed = 0,

    /// <summary>
    /// Circuit is open, requests are blocked.
    /// </summary>
    Open = 1,

    /// <summary>
    /// Circuit is half-open, allowing limited requests to test recovery.
    /// </summary>
    HalfOpen = 2
}
