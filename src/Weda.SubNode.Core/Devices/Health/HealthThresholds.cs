namespace Weda.SubNode.Core.Devices.Health;

/// <summary>
/// Health threshold configuration for device monitoring.
/// Defines warning and critical thresholds for various health metrics.
/// </summary>
public sealed class HealthThresholds
{
    /// <summary>
    /// Gets the error rate threshold for warning status (0-1).
    /// Default: 0.1 (10% error rate triggers warning).
    /// </summary>
    public double WarningErrorRate { get; init; } = 0.1;

    /// <summary>
    /// Gets the error rate threshold for critical/unhealthy status (0-1).
    /// Default: 0.3 (30% error rate triggers unhealthy).
    /// </summary>
    public double CriticalErrorRate { get; init; } = 0.3;

    /// <summary>
    /// Gets the telemetry read duration threshold for warning.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan WarningReadDuration { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the telemetry read duration threshold for critical.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan CriticalReadDuration { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the cloud send duration threshold for warning.
    /// Default: 3 seconds.
    /// </summary>
    public TimeSpan WarningSendDuration { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gets the cloud send duration threshold for critical.
    /// Default: 8 seconds.
    /// </summary>
    public TimeSpan CriticalSendDuration { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Gets the time window for metrics aggregation.
    /// Default: 5 minutes (metrics are computed over this rolling window).
    /// </summary>
    public TimeSpan MetricsWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Creates default health thresholds.
    /// </summary>
    public static HealthThresholds Default => new();

    /// <summary>
    /// Creates strict health thresholds (lower tolerance for errors).
    /// </summary>
    public static HealthThresholds Strict => new()
    {
        WarningErrorRate = 0.05,
        CriticalErrorRate = 0.15,
        WarningReadDuration = TimeSpan.FromSeconds(3),
        CriticalReadDuration = TimeSpan.FromSeconds(6),
        WarningSendDuration = TimeSpan.FromSeconds(2),
        CriticalSendDuration = TimeSpan.FromSeconds(5)
    };

    /// <summary>
    /// Creates relaxed health thresholds (higher tolerance for errors).
    /// </summary>
    public static HealthThresholds Relaxed => new()
    {
        WarningErrorRate = 0.2,
        CriticalErrorRate = 0.5,
        WarningReadDuration = TimeSpan.FromSeconds(10),
        CriticalReadDuration = TimeSpan.FromSeconds(20),
        WarningSendDuration = TimeSpan.FromSeconds(5),
        CriticalSendDuration = TimeSpan.FromSeconds(15)
    };
}
