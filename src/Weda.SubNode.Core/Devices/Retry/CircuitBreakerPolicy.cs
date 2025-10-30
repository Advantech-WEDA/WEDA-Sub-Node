using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Core.Devices.Retry;

/// <summary>
/// Defines circuit breaker behavior to prevent cascading failures.
/// </summary>
public sealed class CircuitBreakerPolicy
{
    /// <summary>
    /// Gets the number of consecutive failures before opening the circuit.
    /// </summary>
    public required int FailureThreshold { get; init; }

    /// <summary>
    /// Gets the duration the circuit stays open before attempting to half-open.
    /// </summary>
    public required TimeSpan OpenDuration { get; init; }

    /// <summary>
    /// Gets the number of successful calls required in half-open state to close the circuit.
    /// </summary>
    public int SuccessThreshold { get; init; } = 1;

    /// <summary>
    /// Gets the sampling duration for tracking failures.
    /// Only failures within this window count toward the threshold.
    /// </summary>
    public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets whether the circuit breaker is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Predefined circuit breaker policy that is disabled.
    /// </summary>
    public static CircuitBreakerPolicy Disabled => new()
    {
        FailureThreshold = int.MaxValue,
        OpenDuration = TimeSpan.Zero,
        Enabled = false
    };

    /// <summary>
    /// Predefined circuit breaker policy with conservative settings.
    /// Opens after 5 failures, stays open for 30 seconds.
    /// </summary>
    public static CircuitBreakerPolicy Conservative => new()
    {
        FailureThreshold = 5,
        OpenDuration = TimeSpan.FromSeconds(30),
        SuccessThreshold = 2,
        SamplingDuration = TimeSpan.FromMinutes(1)
    };

    /// <summary>
    /// Predefined circuit breaker policy with aggressive settings.
    /// Opens after 3 failures, stays open for 10 seconds.
    /// </summary>
    public static CircuitBreakerPolicy Aggressive => new()
    {
        FailureThreshold = 3,
        OpenDuration = TimeSpan.FromSeconds(10),
        SuccessThreshold = 1,
        SamplingDuration = TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// Predefined circuit breaker policy with default settings.
    /// Opens after 4 failures, stays open for 20 seconds.
    /// </summary>
    public static CircuitBreakerPolicy Default => new()
    {
        FailureThreshold = 4,
        OpenDuration = TimeSpan.FromSeconds(20),
        SuccessThreshold = 1,
        SamplingDuration = TimeSpan.FromMinutes(1)
    };
}
