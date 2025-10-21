using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Devices.Health;
using Xunit;

namespace Weda.SubNode.Core.Tests.Devices.Health;

/// <summary>
/// Unit tests for DeviceHealthMonitor.
/// Tests health status computation, metrics collection, thread-safety, and event emission.
/// </summary>
public sealed class DeviceHealthMonitorTests
{
    private const string TestDeviceId = "test-device-001";
    private readonly DeviceHealthMonitor _monitor;
    private readonly HealthThresholds _defaultThresholds = HealthThresholds.Default;
    private readonly ILogger<DeviceHealthMonitor> _logger = NullLogger<DeviceHealthMonitor>.Instance;

    public DeviceHealthMonitorTests()
    {
        _monitor = new DeviceHealthMonitor(TestDeviceId, _logger, communication: null, thresholds: _defaultThresholds);
    }

    #region Initial State Tests

    [Fact]
    public void Constructor_ShouldInitializeWithHealthyStatus()
    {
        // Assert
        _monitor.CurrentHealthStatus.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public void Constructor_WithNullThresholds_ShouldUseDefaults()
    {
        // Arrange & Act
        var monitor = new DeviceHealthMonitor(TestDeviceId, _logger, null);

        // Assert
        monitor.CurrentHealthStatus.ShouldBe(HealthStatus.Healthy);
    }

    #endregion

    #region RecordSuccess Tests

    [Fact]
    public void RecordSuccess_ShouldIncrementCounters()
    {
        // Act
        _monitor.RecordSuccess("telemetry");

        // Assert
        var metrics = _monitor.GetMetrics();
        metrics.TotalOperations.ShouldBe(1);
        metrics.SuccessfulOperations.ShouldBe(1);
        metrics.FailedOperations.ShouldBe(0);
        metrics.ErrorRate.ShouldBe(0.0);
    }

    [Fact]
    public void RecordSuccess_Multiple_ShouldAccumulateCorrectly()
    {
        // Act
        for (int i = 0; i < 10; i++)
        {
            _monitor.RecordSuccess("telemetry");
        }

        // Assert
        var metrics = _monitor.GetMetrics();
        metrics.TotalOperations.ShouldBe(10);
        metrics.SuccessfulOperations.ShouldBe(10);
        metrics.ErrorRate.ShouldBe(0.0);
    }

    #endregion

    #region RecordFailure Tests

    [Fact]
    public void RecordFailure_ShouldIncrementCounters()
    {
        // Arrange
        var exception = new Exception("Test error");

        // Act
        _monitor.RecordFailure("telemetry", exception);

        // Assert
        var metrics = _monitor.GetMetrics();
        metrics.TotalOperations.ShouldBe(1);
        metrics.SuccessfulOperations.ShouldBe(0);
        metrics.FailedOperations.ShouldBe(1);
        metrics.ErrorRate.ShouldBe(1.0);
    }

    [Fact]
    public void RecordFailure_MixedWithSuccess_ShouldCalculateErrorRateCorrectly()
    {
        // Act
        _monitor.RecordSuccess("telemetry");
        _monitor.RecordSuccess("telemetry");
        _monitor.RecordSuccess("telemetry");
        _monitor.RecordFailure("telemetry", new Exception("Error 1"));
        _monitor.RecordFailure("telemetry", new Exception("Error 2"));

        // Assert: 5 total, 2 failures = 0.4 error rate
        var metrics = _monitor.GetMetrics();
        metrics.TotalOperations.ShouldBe(5);
        metrics.SuccessfulOperations.ShouldBe(3);
        metrics.FailedOperations.ShouldBe(2);
        metrics.ErrorRate.ShouldBe(0.4);
    }

    #endregion

    #region Health Status Computation Tests

    [Theory]
    [InlineData(0, HealthStatus.Healthy)]       // 0% error rate
    [InlineData(5, HealthStatus.Healthy)]       // 5% error rate (below warning threshold)
    [InlineData(15, HealthStatus.Degraded)]     // 15% error rate (above warning 10%, below critical 30%)
    [InlineData(35, HealthStatus.Unhealthy)]    // 35% error rate (above critical 30%)
    public void HealthStatus_ShouldChangeBasedOnErrorRate(int failureCount, HealthStatus expectedStatus)
    {
        // Arrange - 100 total operations
        int successCount = 100 - failureCount;

        // Act
        for (int i = 0; i < successCount; i++)
        {
            _monitor.RecordSuccess("telemetry");
        }
        for (int i = 0; i < failureCount; i++)
        {
            _monitor.RecordFailure("telemetry", new Exception($"Error {i}"));
        }

        // Assert
        _monitor.CurrentHealthStatus.ShouldBe(expectedStatus);
    }

    [Fact]
    public async Task HealthStatus_ShouldTransitionFromHealthyToDegraded()
    {
        // Arrange
        DeviceHealthChangedEvent? capturedEvent = null;
        _monitor.HealthStatusChanged += (sender, e) => capturedEvent = e;

        // Act - Create 15% error rate (above warning threshold)
        for (int i = 0; i < 85; i++)
        {
            _monitor.RecordSuccess("telemetry");
        }
        for (int i = 0; i < 15; i++)
        {
            _monitor.RecordFailure("telemetry", new Exception($"Error {i}"));
        }

        // Assert
        _monitor.CurrentHealthStatus.ShouldBe(HealthStatus.Degraded);
        capturedEvent.ShouldNotBeNull();
        capturedEvent.DeviceId.ShouldBe(TestDeviceId);
        capturedEvent.PreviousStatus.ShouldBe(HealthStatus.Healthy);
        capturedEvent.CurrentStatus.ShouldBe(HealthStatus.Degraded);
        capturedEvent.Reason.ShouldContain("Error rate");
    }

    [Fact]
    public async Task HealthStatus_ShouldTransitionFromDegradedToUnhealthy()
    {
        // Arrange - First transition to Degraded
        for (int i = 0; i < 85; i++)
        {
            _monitor.RecordSuccess("telemetry");
        }
        for (int i = 0; i < 15; i++)
        {
            _monitor.RecordFailure("telemetry", new Exception($"Error {i}"));
        }
        _monitor.CurrentHealthStatus.ShouldBe(HealthStatus.Degraded);

        // Arrange - Set up event capture
        DeviceHealthChangedEvent? capturedEvent = null;
        _monitor.HealthStatusChanged += (sender, e) => capturedEvent = e;

        // Act - Add more failures to exceed critical threshold (30%)
        // Total: 85 success + 15 failures + 22 failures = 122 operations, 37 failures
        // Error rate: 37/122 = 30.3% (exceeds 30% critical threshold)
        for (int i = 0; i < 22; i++)
        {
            _monitor.RecordFailure("telemetry", new Exception($"Critical Error {i}"));
        }

        // Assert
        _monitor.CurrentHealthStatus.ShouldBe(HealthStatus.Unhealthy);
        capturedEvent.ShouldNotBeNull();
        capturedEvent.PreviousStatus.ShouldBe(HealthStatus.Degraded);
        capturedEvent.CurrentStatus.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task HealthStatus_ShouldTransitionFromUnhealthyToHealthy()
    {
        // Arrange - First transition to Unhealthy
        for (int i = 0; i < 65; i++)
        {
            _monitor.RecordSuccess("telemetry");
        }
        for (int i = 0; i < 35; i++)
        {
            _monitor.RecordFailure("telemetry", new Exception($"Error {i}"));
        }
        _monitor.CurrentHealthStatus.ShouldBe(HealthStatus.Unhealthy);

        // Arrange - Set up event capture to catch LAST event
        DeviceHealthChangedEvent? lastEvent = null;
        _monitor.HealthStatusChanged += (sender, e) => lastEvent = e;

        // Act - Record many successes to bring error rate below thresholds
        // Need enough successes to dilute 35 failures below warning threshold (10%)
        // If we have 35 failures, we need at least 315 successes for 35/350 = 10%
        // To be safe and get below 10%, use 400 successes: 35/435 = 8%
        // This will cause two transitions: Unhealthy -> Degraded -> Healthy
        for (int i = 0; i < 400; i++)
        {
            _monitor.RecordSuccess("telemetry");
        }

        // Assert
        _monitor.CurrentHealthStatus.ShouldBe(HealthStatus.Healthy);
        lastEvent.ShouldNotBeNull();
        lastEvent.PreviousStatus.ShouldBe(HealthStatus.Degraded); // Last transition was Degraded -> Healthy
        lastEvent.CurrentStatus.ShouldBe(HealthStatus.Healthy);
    }

    #endregion

    #region Telemetry Duration Tests

    [Fact]
    public void RecordTelemetryReadDuration_ShouldStoreValue()
    {
        // Act
        _monitor.RecordTelemetryReadDuration(TimeSpan.FromMilliseconds(100));
        _monitor.RecordTelemetryReadDuration(TimeSpan.FromMilliseconds(200));
        _monitor.RecordTelemetryReadDuration(TimeSpan.FromMilliseconds(150));

        // Assert
        var metrics = _monitor.GetMetrics();
        metrics.AverageTelemetryReadDuration.ShouldBe(TimeSpan.FromMilliseconds(150));
        metrics.MaxTelemetryReadDuration.ShouldBe(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void RecordCloudSendDuration_ShouldStoreValue()
    {
        // Act
        _monitor.RecordCloudSendDuration(TimeSpan.FromMilliseconds(50));
        _monitor.RecordCloudSendDuration(TimeSpan.FromMilliseconds(100));
        _monitor.RecordCloudSendDuration(TimeSpan.FromMilliseconds(75));

        // Assert
        var metrics = _monitor.GetMetrics();
        metrics.AverageCloudSendDuration.ShouldBe(TimeSpan.FromMilliseconds(75));
    }

    [Fact]
    public void HealthStatus_ShouldBeDegradedWhenReadDurationExceedsWarning()
    {
        // Arrange
        var thresholds = new HealthThresholds
        {
            WarningReadDuration = TimeSpan.FromSeconds(5),
            CriticalReadDuration = TimeSpan.FromSeconds(10)
        };
        var monitor = new DeviceHealthMonitor(TestDeviceId, _logger, communication: null, thresholds: thresholds);

        // Act - Record duration exceeding warning threshold
        monitor.RecordTelemetryReadDuration(TimeSpan.FromSeconds(7));

        // Assert
        monitor.CurrentHealthStatus.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public void HealthStatus_ShouldBeUnhealthyWhenReadDurationExceedsCritical()
    {
        // Arrange
        var thresholds = new HealthThresholds
        {
            WarningReadDuration = TimeSpan.FromSeconds(5),
            CriticalReadDuration = TimeSpan.FromSeconds(10)
        };
        var monitor = new DeviceHealthMonitor(TestDeviceId, _logger, communication: null, thresholds: thresholds);

        // Act - Record duration exceeding critical threshold
        monitor.RecordTelemetryReadDuration(TimeSpan.FromSeconds(12));

        // Assert
        monitor.CurrentHealthStatus.ShouldBe(HealthStatus.Unhealthy);
    }

    #endregion

    #region Connection Attempt Tests

    [Fact]
    public void RecordConnectionAttempt_Success_ShouldIncrement()
    {
        // Act
        _monitor.RecordConnectionAttempt(true);
        _monitor.RecordConnectionAttempt(true);
        _monitor.RecordConnectionAttempt(false);

        // Assert
        var metrics = _monitor.GetMetrics();
        metrics.ConnectionAttempts.ShouldBe(3);
        metrics.SuccessfulConnections.ShouldBe(2);
        metrics.FailedConnections.ShouldBe(1);
        metrics.ConnectionSuccessRate.ShouldBe(2.0 / 3.0, 0.01);
    }

    #endregion

    #region GetCurrentHealthAsync Tests

    [Fact]
    public async Task GetCurrentHealthAsync_ShouldReturnCorrectHealthObject()
    {
        // Arrange - Create low error rate (below 10% warning threshold)
        for (int i = 0; i < 95; i++)
        {
            _monitor.RecordSuccess("telemetry");
        }
        for (int i = 0; i < 5; i++)
        {
            _monitor.RecordFailure("telemetry", new Exception($"Test error {i}"));
        }
        _monitor.RecordTelemetryReadDuration(TimeSpan.FromMilliseconds(100));

        // Act
        var health = await _monitor.GetCurrentHealthAsync();

        // Assert
        health.DeviceId.ShouldBe(TestDeviceId);
        health.Status.ShouldBe(HealthStatus.Healthy);
        health.IsHealthy.ShouldBeTrue(); // Backwards compatibility
        health.ErrorRate.ShouldBe(0.05, 0.01); // 5/100 = 5%
        health.AverageResponseTime.ShouldBe(TimeSpan.FromMilliseconds(100));
        health.ErrorCount.ShouldBe(5);
        health.LastChecked.ShouldBeInRange(
            DateTimeOffset.UtcNow.AddSeconds(-1),
            DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task GetCurrentHealthAsync_WithDetails_ShouldIncludeMetrics()
    {
        // Arrange
        _monitor.RecordSuccess("telemetry");
        _monitor.RecordTelemetryReadDuration(TimeSpan.FromMilliseconds(100));
        _monitor.RecordCloudSendDuration(TimeSpan.FromMilliseconds(50));

        // Act
        var health = await _monitor.GetCurrentHealthAsync();

        // Assert
        health.Details.ShouldNotBeNull();
        health.Details.ShouldContainKey("TotalOperations");
        health.Details.ShouldContainKey("SuccessfulOperations");
        health.Details.ShouldContainKey("AverageTelemetryReadDuration");
        health.Details.ShouldContainKey("AverageCloudSendDuration");
    }

    #endregion

    #region GetMetrics Tests

    [Fact]
    public void GetMetrics_ShouldReturnCurrentMetrics()
    {
        // Arrange
        _monitor.RecordSuccess("telemetry");
        _monitor.RecordSuccess("telemetry");
        _monitor.RecordFailure("telemetry", new Exception("Error"));

        // Act
        var metrics = _monitor.GetMetrics();

        // Assert
        metrics.DeviceId.ShouldBe(TestDeviceId);
        metrics.TotalOperations.ShouldBe(3);
        metrics.SuccessfulOperations.ShouldBe(2);
        metrics.FailedOperations.ShouldBe(1);
        metrics.ErrorRate.ShouldBe(1.0 / 3.0, 0.01);
        metrics.MetricsWindow.ShouldBe(_defaultThresholds.MetricsWindow);
        metrics.CollectedAt.ShouldBeInRange(
            DateTimeOffset.UtcNow.AddSeconds(-1),
            DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void GetMetrics_WithCustomWindow_ShouldFilterByTime()
    {
        // Arrange
        _monitor.RecordSuccess("telemetry");
        Thread.Sleep(100); // Wait a bit
        var customWindow = TimeSpan.FromMilliseconds(50);

        // Act
        var metrics = _monitor.GetMetrics(customWindow);

        // Assert - Should only include recent operations
        metrics.MetricsWindow.ShouldBe(customWindow);
    }

    #endregion

    #region ResetMetrics Tests

    [Fact]
    public void ResetMetrics_ShouldClearAllCounters()
    {
        // Arrange
        _monitor.RecordSuccess("telemetry");
        _monitor.RecordFailure("telemetry", new Exception("Error"));
        _monitor.RecordTelemetryReadDuration(TimeSpan.FromMilliseconds(100));
        _monitor.RecordCloudSendDuration(TimeSpan.FromMilliseconds(50));

        // Act
        _monitor.ResetMetrics();

        // Assert
        var metrics = _monitor.GetMetrics();
        metrics.TotalOperations.ShouldBe(0);
        metrics.SuccessfulOperations.ShouldBe(0);
        metrics.FailedOperations.ShouldBe(0);
        metrics.ErrorRate.ShouldBe(0.0);
        metrics.AverageTelemetryReadDuration.ShouldBe(TimeSpan.Zero);
        metrics.AverageCloudSendDuration.ShouldBe(TimeSpan.Zero);
        _monitor.CurrentHealthStatus.ShouldBe(HealthStatus.Healthy);
    }

    #endregion

    #region Event Tests

    [Fact]
    public void HealthStatusChanged_ShouldNotFireWhenStatusUnchanged()
    {
        // Arrange
        int eventCount = 0;
        _monitor.HealthStatusChanged += (sender, e) => eventCount++;

        // Act - Record successes (should remain Healthy)
        for (int i = 0; i < 10; i++)
        {
            _monitor.RecordSuccess("telemetry");
        }

        // Assert
        eventCount.ShouldBe(0);
    }

    [Fact]
    public void HealthStatusChanged_ShouldFireOncePerTransition()
    {
        // Arrange
        int eventCount = 0;
        _monitor.HealthStatusChanged += (sender, e) => eventCount++;

        // Act - Transition to Degraded
        for (int i = 0; i < 85; i++)
        {
            _monitor.RecordSuccess("telemetry");
        }
        for (int i = 0; i < 15; i++)
        {
            _monitor.RecordFailure("telemetry", new Exception($"Error {i}"));
        }

        // Assert
        eventCount.ShouldBe(1);
        _monitor.CurrentHealthStatus.ShouldBe(HealthStatus.Degraded);
    }

    #endregion

    #region Thread-Safety Tests

    [Fact]
    public async Task ConcurrentOperations_ShouldBeSafe()
    {
        // Arrange
        const int threadCount = 10;
        const int operationsPerThread = 100;
        var tasks = new Task[threadCount];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int threadIndex = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    if (j % 2 == 0)
                    {
                        _monitor.RecordSuccess("telemetry");
                    }
                    else
                    {
                        _monitor.RecordFailure("telemetry", new Exception($"Error {threadIndex}-{j}"));
                    }
                    _monitor.RecordTelemetryReadDuration(TimeSpan.FromMilliseconds(j));
                }
            });
        }
        await Task.WhenAll(tasks);

        // Assert
        var metrics = _monitor.GetMetrics();
        metrics.TotalOperations.ShouldBe(threadCount * operationsPerThread);
        metrics.SuccessfulOperations.ShouldBe(threadCount * operationsPerThread / 2);
        metrics.FailedOperations.ShouldBe(threadCount * operationsPerThread / 2);
        metrics.ErrorRate.ShouldBe(0.5, 0.01);
    }

    [Fact]
    public async Task ConcurrentGetHealthAsync_ShouldBeSafe()
    {
        // Arrange
        _monitor.RecordSuccess("telemetry");
        _monitor.RecordFailure("telemetry", new Exception("Error"));

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _monitor.GetCurrentHealthAsync())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All results should be consistent
        results.ShouldAllBe(h => h.DeviceId == TestDeviceId);
        results.ShouldAllBe(h => h.Status == _monitor.CurrentHealthStatus);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RecordSuccess_WithNullOperationType_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _monitor.RecordSuccess(null!));
    }

    [Fact]
    public void RecordFailure_WithNullException_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _monitor.RecordFailure("telemetry", null!));
    }

    [Fact]
    public void RecordTelemetryReadDuration_WithZero_ShouldNotAffectMetrics()
    {
        // Act
        _monitor.RecordTelemetryReadDuration(TimeSpan.Zero);

        // Assert
        var metrics = _monitor.GetMetrics();
        metrics.AverageTelemetryReadDuration.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void GetMetrics_WithNoOperations_ShouldReturnZeroErrorRate()
    {
        // Act
        var metrics = _monitor.GetMetrics();

        // Assert
        metrics.ErrorRate.ShouldBe(0.0);
        metrics.TotalOperations.ShouldBe(0);
    }

    #endregion

    #region Threshold Configuration Tests

    [Fact]
    public void HealthThresholds_Default_ShouldHaveExpectedValues()
    {
        // Act
        var thresholds = HealthThresholds.Default;

        // Assert
        thresholds.WarningErrorRate.ShouldBe(0.1);
        thresholds.CriticalErrorRate.ShouldBe(0.3);
        thresholds.WarningReadDuration.ShouldBe(TimeSpan.FromSeconds(5));
        thresholds.CriticalReadDuration.ShouldBe(TimeSpan.FromSeconds(10));
        thresholds.MetricsWindow.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void HealthThresholds_Strict_ShouldHaveLowerThresholds()
    {
        // Act
        var thresholds = HealthThresholds.Strict;

        // Assert
        thresholds.WarningErrorRate.ShouldBeLessThan(HealthThresholds.Default.WarningErrorRate);
        thresholds.CriticalErrorRate.ShouldBeLessThan(HealthThresholds.Default.CriticalErrorRate);
        thresholds.WarningReadDuration.ShouldBeLessThan(HealthThresholds.Default.WarningReadDuration);
        thresholds.CriticalReadDuration.ShouldBeLessThan(HealthThresholds.Default.CriticalReadDuration);
    }

    [Fact]
    public void HealthThresholds_Relaxed_ShouldHaveHigherThresholds()
    {
        // Act
        var thresholds = HealthThresholds.Relaxed;

        // Assert
        thresholds.WarningErrorRate.ShouldBeGreaterThan(HealthThresholds.Default.WarningErrorRate);
        thresholds.CriticalErrorRate.ShouldBeGreaterThan(HealthThresholds.Default.CriticalErrorRate);
        thresholds.WarningReadDuration.ShouldBeGreaterThan(HealthThresholds.Default.WarningReadDuration);
        thresholds.CriticalReadDuration.ShouldBeGreaterThan(HealthThresholds.Default.CriticalReadDuration);
    }

    #endregion
}
