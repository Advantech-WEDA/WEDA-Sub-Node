using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core.Devices.Health;

/// <summary>
/// Thread-safe device health monitor implementation.
/// Collects metrics in memory using circular buffers and computes health status based on thresholds.
/// </summary>
public sealed class DeviceHealthMonitor : IDeviceHealthMonitor
{
    private string _deviceId;
    private readonly ILogger<DeviceHealthMonitor> _logger;
    private readonly HealthThresholds _thresholds;
    private readonly ICommunication? _communication;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Thread-safe counters using Interlocked
    private long _totalOperations;
    private long _successfulOperations;
    private long _failedOperations;
    private long _connectionAttempts;
    private long _successfulConnections;
    private long _failedConnections;

    // Time-windowed metrics storage (circular buffers via ConcurrentQueue)
    private readonly ConcurrentQueue<OperationRecord> _operationHistory = new();
    private readonly ConcurrentQueue<DurationRecord> _telemetryReadDurations = new();
    private readonly ConcurrentQueue<DurationRecord> _cloudSendDurations = new();

    private HealthStatus _currentHealthStatus = HealthStatus.Healthy;

    // CPU usage tracking
    private DateTime _lastCpuCheckTime = DateTime.UtcNow;
    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private double _lastCpuUsagePercent = 0.0;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceHealthMonitor"/> class.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="communication">The communication instance to check connection state (optional).</param>
    /// <param name="thresholds">Health thresholds configuration. If null, uses default thresholds.</param>
    public DeviceHealthMonitor(
        string deviceId,
        ILogger<DeviceHealthMonitor> logger,
        ICommunication? communication = null,
        HealthThresholds? thresholds = null)
    {
        _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _communication = communication;
        _thresholds = thresholds ?? HealthThresholds.Default;
    }

    /// <inheritdoc/>
    public HealthStatus CurrentHealthStatus => _currentHealthStatus;

    /// <inheritdoc/>
    public event EventHandler<DeviceHealthChangedEvent>? HealthStatusChanged;

    /// <summary>
    /// Updates the device ID. Should be called after device registration.
    /// </summary>
    public void SetDeviceId(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        _deviceId = deviceId;
        _logger.LogDebug("DeviceHealthMonitor device ID updated to: {DeviceId}", _deviceId);
    }

    /// <inheritdoc/>
    public void RecordSuccess(string operationType)
    {
        Interlocked.Increment(ref _totalOperations);
        Interlocked.Increment(ref _successfulOperations);

        _operationHistory.Enqueue(new OperationRecord
        {
            Type = operationType,
            Success = true,
            Timestamp = DateTimeOffset.UtcNow
        });

        // Cleanup old records (keep last 1000 records max)
        TrimQueue(_operationHistory, 1000);

        // Check if health improved
        CheckAndUpdateHealthStatus();
    }

    /// <inheritdoc/>
    public void RecordFailure(string operationType, Exception exception)
    {
        Interlocked.Increment(ref _totalOperations);
        Interlocked.Increment(ref _failedOperations);

        _operationHistory.Enqueue(new OperationRecord
        {
            Type = operationType,
            Success = false,
            ErrorMessage = exception?.Message,
            Timestamp = DateTimeOffset.UtcNow
        });

        TrimQueue(_operationHistory, 1000);

        if (exception != null)
        {
            _logger.LogWarning(exception,
                "Operation '{OperationType}' failed for device {DeviceId}",
                operationType, _deviceId);
        }

        // Check if health degraded
        CheckAndUpdateHealthStatus();
    }

    /// <inheritdoc/>
    public void RecordTelemetryReadDuration(TimeSpan duration)
    {
        _telemetryReadDurations.Enqueue(new DurationRecord
        {
            Duration = duration,
            Timestamp = DateTimeOffset.UtcNow
        });

        TrimQueue(_telemetryReadDurations, 500);

        // Check if performance degraded
        if (duration > _thresholds.CriticalReadDuration)
        {
            _logger.LogWarning(
                "Telemetry read took {Duration}ms (critical threshold: {Threshold}ms) for device {DeviceId}",
                duration.TotalMilliseconds,
                _thresholds.CriticalReadDuration.TotalMilliseconds,
                _deviceId);
        }

        // Check if health status changed
        CheckAndUpdateHealthStatus();
    }

    /// <inheritdoc/>
    public void RecordCloudSendDuration(TimeSpan duration)
    {
        _cloudSendDurations.Enqueue(new DurationRecord
        {
            Duration = duration,
            Timestamp = DateTimeOffset.UtcNow
        });

        TrimQueue(_cloudSendDurations, 500);

        if (duration > _thresholds.CriticalSendDuration)
        {
            _logger.LogWarning(
                "Cloud send took {Duration}ms (critical threshold: {Threshold}ms) for device {DeviceId}",
                duration.TotalMilliseconds,
                _thresholds.CriticalSendDuration.TotalMilliseconds,
                _deviceId);
        }
    }

    /// <inheritdoc/>
    public void RecordConnectionAttempt(bool success)
    {
        Interlocked.Increment(ref _connectionAttempts);

        if (success)
        {
            Interlocked.Increment(ref _successfulConnections);
        }
        else
        {
            Interlocked.Increment(ref _failedConnections);
        }
    }

    /// <inheritdoc/>
    public Task<DeviceHealth> GetCurrentHealthAsync(CancellationToken cancellationToken = default)
    {
        var metrics = GetMetrics();
        var healthStatus = ComputeHealthStatus(metrics);

        // Get system resource usage
        var process = Process.GetCurrentProcess();
        var cpuUsage = GetCpuUsage(process);
        var memoryUsage = GetMemoryUsage(process);

        // Get last error message from recent failed operations
        var lastError = _operationHistory
            .Where(o => !o.Success && !string.IsNullOrEmpty(o.ErrorMessage))
            .OrderByDescending(o => o.Timestamp)
            .FirstOrDefault()?.ErrorMessage;

        var health = new DeviceHealth
        {
            DeviceId = _deviceId,
            Status = healthStatus,
            ErrorRate = metrics.ErrorRate,
            AverageResponseTime = metrics.AverageTelemetryReadDuration,
            ErrorCount = metrics.FailedOperations,
            CpuUsage = cpuUsage,
            MemoryUsage = memoryUsage,
            LastError = lastError,
            LastChecked = DateTimeOffset.UtcNow,
            Details = new Dictionary<string, object>
            {
                ["TotalOperations"] = metrics.TotalOperations,
                ["SuccessfulOperations"] = metrics.SuccessfulOperations,
                ["FailedOperations"] = metrics.FailedOperations,
                ["ConnectionSuccessRate"] = metrics.ConnectionSuccessRate,
                ["AverageTelemetryReadDuration"] = metrics.AverageTelemetryReadDuration.TotalMilliseconds,
                ["MaxTelemetryReadDuration"] = metrics.MaxTelemetryReadDuration.TotalMilliseconds,
                ["AverageCloudSendDuration"] = metrics.AverageCloudSendDuration.TotalMilliseconds,
                ["MaxCloudSendDuration"] = metrics.MaxCloudSendDuration.TotalMilliseconds,
                ["ProcessId"] = process.Id,
                ["WorkingSetMB"] = process.WorkingSet64 / 1024.0 / 1024.0,
                ["PrivateMemoryMB"] = process.PrivateMemorySize64 / 1024.0 / 1024.0
            }
        };

        return Task.FromResult(health);
    }

    /// <inheritdoc/>
    public DeviceHealthMetrics GetMetrics(TimeSpan? window = null)
    {
        var metricsWindow = window ?? _thresholds.MetricsWindow;
        var cutoffTime = DateTimeOffset.UtcNow - metricsWindow;

        // Filter operations within window
        var recentOperations = _operationHistory
            .Where(o => o.Timestamp >= cutoffTime)
            .ToList();

        var totalOps = recentOperations.Count;
        var successfulOps = recentOperations.Count(o => o.Success);
        var failedOps = recentOperations.Count(o => !o.Success);
        var errorRate = totalOps > 0 ? (double)failedOps / totalOps : 0.0;

        // Filter durations within window
        var recentReads = _telemetryReadDurations
            .Where(d => d.Timestamp >= cutoffTime)
            .ToList();

        var recentSends = _cloudSendDurations
            .Where(d => d.Timestamp >= cutoffTime)
            .ToList();

        // Compute averages
        var avgReadDuration = recentReads.Any()
            ? TimeSpan.FromMilliseconds(recentReads.Average(r => r.Duration.TotalMilliseconds))
            : TimeSpan.Zero;

        var maxReadDuration = recentReads.Any()
            ? recentReads.Max(r => r.Duration)
            : TimeSpan.Zero;

        var avgSendDuration = recentSends.Any()
            ? TimeSpan.FromMilliseconds(recentSends.Average(s => s.Duration.TotalMilliseconds))
            : TimeSpan.Zero;

        var maxSendDuration = recentSends.Any()
            ? recentSends.Max(s => s.Duration)
            : TimeSpan.Zero;

        // Connection metrics (lifetime counters, not windowed)
        var connAttempts = Interlocked.Read(ref _connectionAttempts);
        var connSuccess = Interlocked.Read(ref _successfulConnections);
        var connFailed = Interlocked.Read(ref _failedConnections);
        var connSuccessRate = connAttempts > 0 ? (double)connSuccess / connAttempts : 1.0;

        return new DeviceHealthMetrics
        {
            DeviceId = _deviceId,
            TotalOperations = totalOps,
            SuccessfulOperations = successfulOps,
            FailedOperations = failedOps,
            ErrorRate = errorRate,
            ConnectionAttempts = (int)connAttempts,
            SuccessfulConnections = (int)connSuccess,
            FailedConnections = (int)connFailed,
            ConnectionSuccessRate = connSuccessRate,
            AverageTelemetryReadDuration = avgReadDuration,
            MaxTelemetryReadDuration = maxReadDuration,
            AverageCloudSendDuration = avgSendDuration,
            MaxCloudSendDuration = maxSendDuration,
            MetricsWindow = metricsWindow,
            CollectedAt = DateTimeOffset.UtcNow
        };
    }

    /// <inheritdoc/>
    public void ResetMetrics()
    {
        _lock.Wait();
        try
        {
            Interlocked.Exchange(ref _totalOperations, 0);
            Interlocked.Exchange(ref _successfulOperations, 0);
            Interlocked.Exchange(ref _failedOperations, 0);
            Interlocked.Exchange(ref _connectionAttempts, 0);
            Interlocked.Exchange(ref _successfulConnections, 0);
            Interlocked.Exchange(ref _failedConnections, 0);

            _operationHistory.Clear();
            _telemetryReadDurations.Clear();
            _cloudSendDurations.Clear();

            _currentHealthStatus = HealthStatus.Healthy;

            _logger.LogInformation("Health metrics reset for device {DeviceId}", _deviceId);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ===== Private Helper Methods =====

    private double? GetCpuUsage(Process process)
    {
        try
        {
            var currentTime = DateTime.UtcNow;
            var currentCpuTime = process.TotalProcessorTime;

            // Calculate time elapsed since last check
            var timeDelta = (currentTime - _lastCpuCheckTime).TotalMilliseconds;

            // Need at least 100ms between samples for meaningful measurement
            if (timeDelta < 100)
            {
                return _lastCpuUsagePercent;
            }

            // Calculate CPU time delta
            var cpuTimeDelta = (currentCpuTime - _lastCpuTime).TotalMilliseconds;

            // Calculate CPU usage percentage
            // CPU usage = (CPU time used / elapsed time) / number of cores * 100
            var cpuUsagePercent = (cpuTimeDelta / (timeDelta * Environment.ProcessorCount)) * 100;

            // Update last values
            _lastCpuCheckTime = currentTime;
            _lastCpuTime = currentCpuTime;
            _lastCpuUsagePercent = Math.Max(0, Math.Min(cpuUsagePercent, 100.0));

            return _lastCpuUsagePercent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get CPU usage for device {DeviceId}", _deviceId);
            return null;
        }
    }

    private double? GetMemoryUsage(Process process)
    {
        try
        {
            // Get working set memory in bytes
            var workingSetBytes = process.WorkingSet64;

            // Get total physical memory (platform-specific)
            var totalMemoryBytes = GetTotalPhysicalMemory();

            if (totalMemoryBytes > 0)
            {
                var memoryUsagePercent = (workingSetBytes / (double)totalMemoryBytes) * 100;
                return Math.Min(memoryUsagePercent, 100.0);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get memory usage for device {DeviceId}", _deviceId);
            return null;
        }
    }

    private long GetTotalPhysicalMemory()
    {
        try
        {
            // Use GC.GetGCMemoryInfo() which is cross-platform
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            return gcMemoryInfo.TotalAvailableMemoryBytes;
        }
        catch
        {
            // Fallback: use a default value (8GB) if we can't get total memory
            return 8L * 1024 * 1024 * 1024;
        }
    }

    private void CheckAndUpdateHealthStatus()
    {
        var metrics = GetMetrics();
        var newStatus = ComputeHealthStatus(metrics);

        if (newStatus != _currentHealthStatus)
        {
            var previousStatus = _currentHealthStatus;
            _currentHealthStatus = newStatus;

            var reason = GenerateHealthChangeReason(metrics, previousStatus, newStatus);

            _logger.LogWarning(
                "Device {DeviceId} health changed: {Previous} → {Current} ({Reason})",
                _deviceId, previousStatus, newStatus, reason);

            var @event = new DeviceHealthChangedEvent(
                DeviceId: _deviceId,
                PreviousStatus: previousStatus,
                CurrentStatus: newStatus,
                Reason: reason,
                Timestamp: DateTimeOffset.UtcNow);

            HealthStatusChanged?.Invoke(this, @event);
        }
    }

    private HealthStatus ComputeHealthStatus(DeviceHealthMetrics metrics)
    {
        // First check: Communication state (highest priority)
        if (_communication != null)
        {
            var state = _communication.State;
            if (state == CommunicationState.Disconnected || state == CommunicationState.Error)
            {
                return HealthStatus.Unhealthy;
            }

            if (state == CommunicationState.Connecting)
            {
                return HealthStatus.Degraded;
            }
        }

        // Check error rate thresholds
        if (metrics.ErrorRate >= _thresholds.CriticalErrorRate)
        {
            return HealthStatus.Unhealthy;
        }

        if (metrics.ErrorRate >= _thresholds.WarningErrorRate)
        {
            return HealthStatus.Degraded;
        }

        // Check performance thresholds
        if (metrics.AverageTelemetryReadDuration > _thresholds.CriticalReadDuration ||
            metrics.AverageCloudSendDuration > _thresholds.CriticalSendDuration)
        {
            return HealthStatus.Unhealthy;
        }

        if (metrics.AverageTelemetryReadDuration > _thresholds.WarningReadDuration ||
            metrics.AverageCloudSendDuration > _thresholds.WarningSendDuration)
        {
            return HealthStatus.Degraded;
        }

        // Check connection success rate
        if (metrics.ConnectionSuccessRate < 0.5) // Less than 50% connection success
        {
            return HealthStatus.Unhealthy;
        }

        if (metrics.ConnectionSuccessRate < 0.8) // Less than 80% connection success
        {
            return HealthStatus.Degraded;
        }

        return HealthStatus.Healthy;
    }

    private string GenerateHealthChangeReason(
        DeviceHealthMetrics metrics,
        HealthStatus previousStatus,
        HealthStatus currentStatus)
    {
        var reasons = new List<string>();

        if (metrics.ErrorRate >= _thresholds.CriticalErrorRate)
        {
            reasons.Add($"Critical error rate: {metrics.ErrorRate:P1}");
        }
        else if (metrics.ErrorRate >= _thresholds.WarningErrorRate)
        {
            reasons.Add($"Warning error rate: {metrics.ErrorRate:P1}");
        }

        if (metrics.AverageTelemetryReadDuration > _thresholds.CriticalReadDuration)
        {
            reasons.Add($"Critical read duration: {metrics.AverageTelemetryReadDuration.TotalSeconds:F1}s");
        }
        else if (metrics.AverageTelemetryReadDuration > _thresholds.WarningReadDuration)
        {
            reasons.Add($"Warning read duration: {metrics.AverageTelemetryReadDuration.TotalSeconds:F1}s");
        }

        if (metrics.AverageCloudSendDuration > _thresholds.CriticalSendDuration)
        {
            reasons.Add($"Critical send duration: {metrics.AverageCloudSendDuration.TotalSeconds:F1}s");
        }
        else if (metrics.AverageCloudSendDuration > _thresholds.WarningSendDuration)
        {
            reasons.Add($"Warning send duration: {metrics.AverageCloudSendDuration.TotalSeconds:F1}s");
        }

        if (metrics.ConnectionSuccessRate < 0.8)
        {
            reasons.Add($"Low connection success rate: {metrics.ConnectionSuccessRate:P0}");
        }

        if (currentStatus < previousStatus) // Improved
        {
            return reasons.Any()
                ? $"Improved, but: {string.Join(", ", reasons)}"
                : "Metrics improved";
        }

        return reasons.Any()
            ? string.Join(", ", reasons)
            : "Threshold exceeded";
    }

    private static void TrimQueue<T>(ConcurrentQueue<T> queue, int maxSize)
    {
        while (queue.Count > maxSize)
        {
            queue.TryDequeue(out _);
        }
    }

    // ===== Internal Record Types =====

    private sealed record OperationRecord
    {
        public required string Type { get; init; }
        public required bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
    }

    private sealed record DurationRecord
    {
        public required TimeSpan Duration { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
    }
}
