namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Device health status levels.
/// Indicates the overall health state of a device based on collected metrics.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Device is operating normally. All metrics within acceptable ranges.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Device is operating but some metrics are approaching warning thresholds.
    /// May require attention soon.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Device has critical issues. One or more metrics exceed critical thresholds.
    /// Immediate attention required.
    /// </summary>
    Unhealthy = 2
}
