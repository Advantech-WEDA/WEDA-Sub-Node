namespace Weda.SubNode.Core.Devices.Health;

/// <summary>
/// Device health metrics collected over a time window.
/// Contains operation counters, performance metrics, and resource usage statistics.
/// </summary>
public sealed record DeviceHealthMetrics
{
    /// <summary>
    /// Gets the device identifier.
    /// </summary>
    public required string DeviceId { get; init; }

    // ===== Operation Counters =====

    /// <summary>
    /// Gets the total number of operations attempted in the metrics window.
    /// </summary>
    public required int TotalOperations { get; init; }

    /// <summary>
    /// Gets the number of successful operations.
    /// </summary>
    public required int SuccessfulOperations { get; init; }

    /// <summary>
    /// Gets the number of failed operations.
    /// </summary>
    public required int FailedOperations { get; init; }

    /// <summary>
    /// Gets the error rate (0-1).
    /// Calculated as: FailedOperations / TotalOperations.
    /// </summary>
    public required double ErrorRate { get; init; }

    // ===== Connection Metrics =====

    /// <summary>
    /// Gets the number of connection attempts.
    /// </summary>
    public int ConnectionAttempts { get; init; }

    /// <summary>
    /// Gets the number of successful connections.
    /// </summary>
    public int SuccessfulConnections { get; init; }

    /// <summary>
    /// Gets the number of failed connections.
    /// </summary>
    public int FailedConnections { get; init; }

    /// <summary>
    /// Gets the connection success rate (0-1).
    /// </summary>
    public double ConnectionSuccessRate { get; init; }

    // ===== Performance Metrics =====

    /// <summary>
    /// Gets the average telemetry read duration.
    /// </summary>
    public TimeSpan AverageTelemetryReadDuration { get; init; }

    /// <summary>
    /// Gets the maximum telemetry read duration observed.
    /// </summary>
    public TimeSpan MaxTelemetryReadDuration { get; init; }

    /// <summary>
    /// Gets the average cloud send duration.
    /// </summary>
    public TimeSpan AverageCloudSendDuration { get; init; }

    /// <summary>
    /// Gets the maximum cloud send duration observed.
    /// </summary>
    public TimeSpan MaxCloudSendDuration { get; init; }

    // ===== Resource Metrics (Optional) =====

    /// <summary>
    /// Gets the CPU usage percentage (0-100), if available.
    /// </summary>
    public double? CpuUsagePercent { get; init; }

    /// <summary>
    /// Gets the memory usage percentage (0-100), if available.
    /// </summary>
    public double? MemoryUsagePercent { get; init; }

    /// <summary>
    /// Gets the memory usage in bytes, if available.
    /// </summary>
    public long? MemoryUsageBytes { get; init; }

    // ===== Metadata =====

    /// <summary>
    /// Gets the time window over which metrics were collected.
    /// </summary>
    public required TimeSpan MetricsWindow { get; init; }

    /// <summary>
    /// Gets the timestamp when metrics were collected.
    /// </summary>
    public required DateTimeOffset CollectedAt { get; init; }

    /// <summary>
    /// Returns a string representation of the health metrics.
    /// </summary>
    public override string ToString() =>
        $"DeviceHealthMetrics[{DeviceId}]: " +
        $"ErrorRate={ErrorRate:P2}, " +
        $"AvgReadTime={AverageTelemetryReadDuration.TotalMilliseconds:F0}ms, " +
        $"AvgSendTime={AverageCloudSendDuration.TotalMilliseconds:F0}ms, " +
        $"Window={MetricsWindow.TotalMinutes:F0}min";
}
