# IIoT Code Review Report — System Agent

**Date**: 2026-03-20
**Reviewer**: Claude Code (IIoT Engineer Pro)
**Standards**: DDD/SOLID/CQRS · SIL2 (IEC 61508) · IEC 62443 SL2

---

## Review Summary

- **Files Reviewed**:
  - `Communication/LocalSystemCommunication.cs`
  - `Communication/LocalSystemResourceCollector.cs`
  - `Communication/Collectors/DiskCollector.cs`
  - `Protocols/SystemMetricsParser.cs`
  - `Devices/SystemAgentDeviceBase.cs`
  - `LocalSystemAgentDevice.cs`
- **Overall Assessment**: **Pass with Comments**

The system agent is well-structured with good safety awareness (health-gated telemetry, graceful hardware degradation, retry pipeline). Four issues require fixes before production deployment: a silent exception swallower that masks cancellation, unguarded `/proc` parsing, incorrect disk capacity math, and a timeout scope bug that defeats the retry policy.

---

## Critical Issues (Must Fix)

### 1. Silent exception swallowing masks cancellation and parse errors

**File**: `Communication/Collectors/DiskCollector.cs:95–98`

```csharp
catch
{
    // Ignore errors reading disk stats
}
```

**Problem**: An untyped `catch` with no logging or propagation:
- Swallows `OperationCanceledException` — if the parent `CancellationToken` is cancelled, the disk collector silently absorbs it instead of propagating, violating the C# cancellation contract.
- Silently swallows I/O failures and parse exceptions, leaving callers with no indication that disk I/O stats are missing.
- Violates the project mandate: *"Never ignore an error. Wrap all errors with technical context."*

**Recommendation**: Propagate cancellation explicitly and log all other failures:
```csharp
catch (OperationCanceledException)
{
    throw; // Always propagate cancellation
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to read /proc/diskstats — I/O stats will be unavailable");
}
```

---

### 2. Unguarded `long.Parse` on untrusted `/proc` data

**File**: `Communication/Collectors/DiskCollector.cs:86–90`

```csharp
ReadsCompleted = long.Parse(parts[3]),
SectorsRead    = long.Parse(parts[5]),
WritesCompleted = long.Parse(parts[7]),
SectorsWritten  = long.Parse(parts[9]),
IoTimeMs        = long.Parse(parts[12])
```

**Problem**: `/proc/diskstats` contains entries for partitions and virtual devices that may have non-numeric or unexpected fields. A malformed line throws `FormatException` or `OverflowException`, which is then swallowed by the empty `catch` above, silently dropping all I/O stats for all disks.

**Recommendation**: Use `long.TryParse` with per-line validation:
```csharp
if (!long.TryParse(parts[3], out var readsCompleted)) continue;
if (!long.TryParse(parts[5], out var sectorsRead)) continue;
// ...
result[deviceName] = new DiskIoStats { ReadsCompleted = readsCompleted, ... };
```

---

### 3. Incorrect disk total capacity calculation

**File**: `Protocols/SystemMetricsParser.cs:274–286`

```csharp
var total = disk.FilesystemAvailBytes + (disk.FilesystemFreeBytes > 0
    ? disk.FilesystemFreeBytes - disk.FilesystemAvailBytes + disk.FilesystemAvailBytes
    : disk.FilesystemAvailBytes);
// ...
"total" => total > 0 ? total : disk.FilesystemAvailBytes * 2, // Rough estimate
```

**Problem**:
- The inline formula simplifies to `avail + free` (not total capacity) because the ternary's true branch is `free - avail + avail = free`.
- The fallback `FilesystemAvailBytes * 2` is arbitrary — it will be wildly wrong for any disk where used space ≠ available space.
- This causes `usage_percent` and `used` metrics sent to the cloud to be incorrect, a data-integrity violation.

**Root cause**: `DiskMetrics` has no `FilesystemTotalBytes` field. `DriveInfo.TotalSize` is available in `DiskCollector` but never stored.

**Recommendation**: In `DiskCollector.cs`, store `drive.TotalSize`:
```csharp
var disk = new DiskMetrics
{
    DeviceName             = deviceName,
    MountPoint             = drive.Name,
    FilesystemTotalBytes   = drive.TotalSize,        // Add this field
    FilesystemAvailBytes   = drive.AvailableFreeSpace,
    FilesystemFreeBytes    = drive.TotalFreeSpace
};
```

Then in `SystemMetricsParser.cs`, use it directly:
```csharp
"total"         => disk.FilesystemTotalBytes,
"used"          => disk.FilesystemTotalBytes - disk.FilesystemAvailBytes,
"usage_percent" => disk.FilesystemTotalBytes > 0
    ? Math.Round((1 - (double)disk.FilesystemAvailBytes / disk.FilesystemTotalBytes) * 100, 2)
    : 0,
```

---

### 4. Timeout covers entire retry pipeline instead of each attempt

**File**: `Communication/LocalSystemResourceCollector.cs:344–346`

```csharp
using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
cts.CancelAfter(attemptTimeout); // 5 seconds, but covers ALL 3 attempts
return await retryPipeline.ExecuteAsync(async ct => await action(ct), cts.Token);
```

**Problem**: The 5-second `CancelAfter` is started before the retry loop, not per-attempt. If the first attempt takes 4.9 s (just under the timeout), the CTS fires mid-second-attempt and cancels the pipeline entirely. The retry policy becomes unreachable under slow-sensor conditions — exactly when retries are needed most.

**Recommendation**: Apply the timeout inside the pipeline delegate so each attempt gets its own 5-second window:
```csharp
return await retryPipeline.ExecuteAsync(async ct =>
{
    using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    attemptCts.CancelAfter(attemptTimeout);
    return await action(attemptCts.Token);
}, parentToken);
```

---

## Warnings (Should Fix)

### 5. `DateTime.Now` (local time) in log — use UTC

**File**: `LocalSystemAgentDevice.cs:161`

```csharp
_logger.LogInformation("[HOT-RELOAD] Configuration update received at {Timestamp}",
    DateTime.Now.ToString("HH:mm:ss.fff"));
```

The rest of the codebase uses `DateTimeOffset.UtcNow`. Local time in logs breaks correlation across timezones and Serilog's JSON output. Replace with `DateTimeOffset.UtcNow`.

---

### 6. `LogInformation` on every collection cycle produces excessive log volume

**File**: `Communication/LocalSystemCommunication.cs:84`

```csharp
_logger.LogInformation("Collect system metrics for types: {Types}", ...);
```

At the default 5-second interval with ~10 metric types, this emits ~17,000 Info lines per day. Change to `LogDebug`.

---

### 7. `GetVoltageMetric` and `GetFanSpeedMetric` ignore `metricName` parameter

**File**: `Protocols/SystemMetricsParser.cs:410–424`

```csharp
private object? GetVoltageMetric(VoltageMetrics? metrics, string metricName, Sensor sensor)
{
    if (metrics == null || metrics.Voltages == null) return null;
    return metrics.Voltages; // Always returns full dictionary — metricName ignored
}
```

`GetTemperatureMetric` (same file, lines 381–408) correctly implements per-sensor lookup by `metricName`. Voltage and fan speed silently return the full dictionary regardless of sensor configuration. This is inconsistent and will cause issues if the telemetry framework expects a scalar value. Mirror the per-sensor lookup from `GetTemperatureMetric`.

---

### 8. Dead method `GetHealthMetric` is unreachable

**File**: `Protocols/SystemMetricsParser.cs:199–208`

```csharp
private object? GetHealthMetric(HealthStatusMetrics health, string? metricName) { ... }
```

`SupportedDataType.Health` is removed from `metricTypes` at line 117 (`metricTypes.Remove(SupportedDataType.Health)`), and the `GetMetricValue` switch at lines 180–196 has no `Health` case. This method is unreachable dead code. Either wire it into `GetMetricValue` (if health telemetry is intended) or delete it.

---

### 9. Dependency Inversion violated — collectors `new`ed in constructor

**File**: `Communication/LocalSystemResourceCollector.cs:35–41`, `Communication/LocalSystemCommunication.cs:27`

```csharp
_cpuCollector     = new CpuCollector(logger);
_ramCollector     = new RamCollector(logger);
// ...
_collector = new LocalSystemResourceCollector(_logger); // in LocalSystemCommunication
```

All collectors are instantiated with `new`, bypassing the DI container. This makes unit testing impossible without real OS access and prevents swapping implementations. Inject collectors (or a factory) via constructor parameters and register them in the DI container.

---

## Suggestions (Consider)

### 10. Replace 13 typed nullable Task fields with a result map

**File**: `Communication/LocalSystemResourceCollector.cs:115–128`

The pattern of 13 individually named `Task<T?>?` variables is fragile — every new metric type requires changes in 4+ separate locations (declaration, switch case, WhenAll, result assignment). A `Dictionary<string, Task>` with a typed result extractor, or a discriminated union result type, would be more maintainable and less error-prone.

### 11. Replace `[HOT-RELOAD]` string prefix with structured log scope

**File**: `LocalSystemAgentDevice.cs:160–165`

Using `[HOT-RELOAD]` as a message prefix is a workaround for missing context. Use `_logger.BeginScope(...)` to add structured properties that integrate with Serilog's enrichers and make log filtering reliable:
```csharp
using (_logger.BeginScope(new { Operation = "HotReload", DeviceId = e.DeviceId }))
{
    _logger.LogInformation("Configuration update received");
    // ...
}
```

### 12. Implement `GetLinuxInodeInfo` or remove stub

**File**: `Communication/Collectors/DiskCollector.cs:103–107`

```csharp
private static (long total, long free) GetLinuxInodeInfo(string _)
{
    // This would require P/Invoke to statvfs, return 0 for now
    return (0, 0);
}
```

The stub always returns `(0, 0)` and the result is stored in `DiskMetrics.FilesystemFiles`. Sensors configured for inode metrics will always report zero without any warning. Either implement the `statvfs` P/Invoke, or log a one-time warning and skip populating the field, so operators know the data is unavailable.

---

## Positive Observations

1. **SIL2 health gate in telemetry pipeline**: `SystemMetricsParser.cs:153–159` correctly skips telemetry for metric types that failed collection, preventing stale or zero values from reaching the cloud. This is the right safety pattern.

2. **Graceful hardware platform degradation**: `LocalSystemResourceCollector.cs:51–70` catches five distinct exception types (`TypeInitializationException`, `TargetInvocationException`, `DllNotFoundException`, `BadImageFormatException`, generic `Exception`) during Advantech SUSI initialization and continues with `null`, correctly isolating hardware failures from software operation.

3. **Immutable request type**: `SystemMetricsRequest` defined as a `record` ensures request objects cannot be mutated after creation, eliminating a class of concurrency bugs.

4. **Parallel metric collection with isolated failure**: `Task.WhenAll` over independently executed per-type tasks means one failing collector does not block others — a correct application of the bulkhead pattern.

5. **Retry with exponential backoff**: `RetryPolicyFactory.CreateNTimeRetry` with 100 ms → 1 s backoff is appropriate for transient OS API failures.

6. **Two-stage configuration validation**: Startup (`OnBeforeInitializeAsync`) and hot-reload (`OnBeforeConfigUpdateAsync`) validation prevents invalid configurations from reaching runtime in both the initial load and dynamic update paths.

7. **Consistent `CancellationToken` threading**: Tokens are propagated through all async call chains, including linked token sources for compound cancellation — correct for a SIL2 environment.

8. **Platform guard clarity**: `OperatingSystem.IsLinux()` / `IsMacOS()` / `IsWindows()` guards clearly delineate platform-specific paths and are checked per-block rather than cached, avoiding stale state.

---

## Checklist Summary

| Category | Status | Notes |
|---|---|---|
| SRP / Cohesion | ✅ Pass | Collectors are well-scoped per metric type |
| OCP / Extension | ⚠️ Warn | New metric types require multi-site changes (issue #10) |
| DIP | ❌ Fail | Collectors `new`ed directly, no DI (issue #9) |
| SIL2 Pre-condition Validation | ✅ Pass | Two-stage config validation in place |
| SIL2 Fail-safe | ✅ Pass | Health-gated telemetry correctly suppresses bad data |
| SIL2 Timeout Protection | ❌ Fail | Timeout scope covers retry pipeline, not per-attempt (issue #4) |
| Error Propagation | ❌ Fail | Silent catch in DiskCollector (issues #1, #2) |
| Resource Cleanup | ✅ Pass | `using` on CTS, proper `IDisposable` patterns |
| Async / CancellationToken | ⚠️ Warn | Cancellation swallowed in DiskCollector (issue #1) |
| Structured Logging | ⚠️ Warn | Info-level noise (issue #6), prefix convention (issue #11) |
| Data Integrity | ❌ Fail | Disk total calculation is incorrect (issue #3) |
| Dead Code | ⚠️ Warn | `GetHealthMetric` unreachable (issue #8) |
| Security | ✅ Pass | No hardcoded credentials, no injection vectors |
