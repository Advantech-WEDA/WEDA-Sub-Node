namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Device health status.
/// Contains health status level, metrics, and diagnostic information.
/// </summary>
public class DeviceHealth
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Gets or sets the health status level (Healthy, Degraded, Unhealthy).
    /// </summary>
    public required HealthStatus Status { get; init; }

    /// <summary>
    /// Gets or sets whether the device is healthy (backwards compatibility).
    /// True if Status == Healthy, false otherwise.
    /// </summary>
    public bool IsHealthy => Status == HealthStatus.Healthy;

    /// <summary>
    /// Gets or sets the error rate (0-1).
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// Gets or sets the average response time.
    /// </summary>
    public TimeSpan AverageResponseTime { get; init; }

    /// <summary>
    /// Gets or sets the CPU usage percentage (0-100), if available.
    /// </summary>
    public double? CpuUsage { get; init; }

    /// <summary>
    /// Gets or sets the memory usage percentage (0-100), if available.
    /// </summary>
    public double? MemoryUsage { get; init; }

    /// <summary>
    /// Gets or sets the error count (for backwards compatibility).
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Gets or sets the last error message.
    /// </summary>
    public string? LastError { get; init; }

    /// <summary>
    /// Gets or sets additional health details and metrics.
    /// </summary>
    public Dictionary<string, object>? Details { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when health was last checked.
    /// </summary>
    public DateTimeOffset LastChecked { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets additional health metrics (backwards compatibility).
    /// </summary>
    [Obsolete("Use Details property instead")]
    public Dictionary<string, object>? Metrics
    {
        get => Details;
        init => Details = value;
    }

    /// <summary>
    /// Gets or sets the timestamp (backwards compatibility).
    /// </summary>
    [Obsolete("Use LastChecked property instead")]
    public DateTimeOffset Timestamp
    {
        get => LastChecked;
        init => LastChecked = value;
    }
}
