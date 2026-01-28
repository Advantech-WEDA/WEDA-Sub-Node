# System Agent Fail-Safe Implementation Report

## Overview
This document details the enhancements made to the System Agent (`examples/system-agent`) to meet **SIL2 (Safety Integrity Level 2)** compliance requirements. The primary goal was to eliminate "fail-silent" behaviors where the system would report incorrect data (zeros) during collection failures, and instead implement a **Fail-Safe** architecture that prioritizes data integrity and self-diagnosis.

## Key Features

### 1. Robust Retry Mechanism
**Objective:** Handle transient failures (glitches) without data loss.
- **Implementation:** `ExecuteWithRetryAsync` helper method in `LocalSystemResourceCollector`.
- **Logic:** 
  - Attempts each metric collection up to **3 times** (1 initial + 2 retries).
  - Uses linear backoff (100ms * retry count) to allow systems to recover.
  - Failures in one collector (e.g., Network) do not block others (e.g., CPU).

### 2. Strict Timeouts
**Objective:** Prevent system hangs due to stalled hardware drivers or network interfaces.
- **Implementation:** Enforced at the task execution level using `CancellationTokenSource`.
- **Policy:** **5-second timeout** per collection attempt.
- **Behavior:** If a collector does not return within 5 seconds, it is cancelled, logged as a warning, and a retry is attempted.

### 3. Internal Health Monitoring
**Objective:** Self-diagnose the agent's ability to collect data.
- **Implementation:** `HealthStatusMetrics` model.
- **Metrics Tracked:**
  - `IsHealthy`: Boolean flag indicating if *all* requested metrics were successfully collected.
  - `ActiveErrors`: Dictionary mapping failed metric types to error messages.

### 4. Fail-Safe Telemetry Reporting
**Objective:** Prevent reporting of invalid data.
- **Previous Behavior:** Failed collections returned empty objects, leading to "0" values for CPU, Memory, etc.
- **New Behavior:** 
  - The `SystemMetricsParser` checks `ActiveErrors` before processing data.
  - If a metric type failed collection, its sensors are **skipped** entirely.
  - This ensures the cloud platform receives *no data* rather than *false data*, triggering data-missing alerts upstream instead of processing valid-looking zeros.

### 5. Health Telemetry
**Objective:** Expose internal health status to the cloud.
- **New Metric Type:** `health`
- **Supported Metrics:**
  - `is_healthy`: 1 (Healthy) / 0 (Unhealthy)
  - `error_count`: Number of active errors
  - `errors`: Semicolon-delimited string of error details

## Architecture Details

### `LocalSystemResourceCollector.cs`
- **`ExecuteWithRetryAsync`**: Wraps all 13 collectors. Handles exceptions, timeouts, and logging.
- **`CollectMetricsAsync`**: Aggregates results. If a task fails (returns null), it populates `SystemMetricsRawData.Health` with the error details.

### `SystemMetricsParser.cs`
- **`ConvertToTelemetryMeasures`**: Added a SIL2 check.
  ```csharp
  if (rawData.Health.ActiveErrors.ContainsKey(metricType.ToLowerInvariant())) {
      continue; // Skip faulty sensor
  }
  ```
- **`GetHealthMetric`**: Added handler for the new `health` metric type.

## Failure Scenarios

| Scenario | System Behavior | Outcome |
| :--- | :--- | :--- |
| **Transient Glitch** | Retry logic catches the exception and retries after 100ms. | **Success**: Data collected, no error reported. |
| **Stalled Driver** | Timeout logic cancels task after 5s. Retries. | **Fail-Safe**: Metric skipped, `IsHealthy=false`, Error logged. |
| **Hardware Failure** | All retries fail. | **Fail-Safe**: Metric skipped, `IsHealthy=false`, Error logged. |
| **Network Down** | Network collector fails. CPU/RAM collectors succeed. | **Partial Success**: Network data omitted, CPU/RAM reported normally. `IsHealthy=false`. |

## Configuration Example

To enable monitoring of the agent's fail-safe status, add the following sensor to `devicecfg.json`:

```json
{
  "Name": "Agent Health Status",
  "ResourceId": "system_health_status",
  "Parameters": {
    "MetricType": "health",
    "MetricName": "is_healthy"
  },
  "Report": {
    "Enabled": true,
    "Interval": 10000
  }
}
```
