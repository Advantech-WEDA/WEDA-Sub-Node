# Per-Sensor PHM Feature Reporting Interval Design

> Status: Implemented
>
> Context: This document explains why per-sensor `Report.Interval` does not independently control
> PHM feature upload frequency in the current architecture, and describes a solution that requires
> no framework changes.

---

## 1. Problem: All PHM Features Are Sent Together

With the current architecture, all 10 PHM features (`x_axis_rms_mg`, `x_axis_kurtosis`, etc.) are
always sent to the cloud at the **same moment**, driven by the raw sensor's interval group. Setting
different `Report.Interval` values on individual PHM sensors in `devicecfg.json` has **no effect**
on their upload frequency.

### 1.1 Actual Telemetry Flow

```
DaqMetricsParser.OnTelemetryReceived([rawPayload])
  └─ StreamingDeviceBase._sensorCache.Push([rawPayload])
       └─ stores: { "daqraw:vibration:payload" → rawFrame }
            (PHM sensor ResourceIds are NEVER written to SensorCache)

RunIntervalLoopAsync  (interval = ObservationWindowMs, sensors = all 11)
  └─ _sensorCache.Read([raw, rms, peak, kurtosis, ...])
       └─ returns: [rawPayload only]   ← PHM sensors have no cache entry

  └─ EnqueueTelemetryAsync([rawPayload])
       └─ TelemetryPipeline.ExecuteTransformStageAsync
            └─ finds PhmFeatureTransform on raw sensor
            └─ TransformAsync([rawPayload])
                 └─ computes all 10 features at once
                 └─ returns [rms, peak, displacement, velocity, ...]
            └─ ALL 10 features sent immediately ← no per-feature gating
```

### 1.2 Root Cause

`PhmFeatureTransform` outputs directly to the telemetry send queue. The output measures bypass
`SensorCache` entirely, so the per-sensor interval sampling tasks that `GroupSensorsByInterval()`
creates for PHM sensors are permanently empty — they never read anything from the cache.

The standard framework mechanism for per-sensor intervals (SensorCache → per-group sampling task)
only works for sensors that **receive data via the stream**. PHM features are computed, not received.

---

## 2. Solution: Transform-Level Frame Counter

The fix lives entirely within the `daq-data-collector` example project — no changes to `src/` are
required. `PhmFeatureTransform` tracks a **frame counter** per feature and emits only every N-th
frame, where N = `sensorIntervalMs / observationWindowMs` (integer division).

Wall-clock comparison was considered but rejected: because `_lastEmitTime` is recorded *after* FFT
computation, the measured elapsed time is always slightly shorter than `intervalMs`, causing an
alternating skip / emit pattern on every pair of frames (odd-indexed frames are permanently
skipped). Frame counting is immune to FFT latency and produces exact N-th-frame emissions.

### 2.1 Architecture After Fix

```
Every ObservationWindow (e.g., 1 s):
  TransformAsync([rawPayload])
    ├─ Compute all 10 features (always — FFT still runs on every frame)
    └─ Per feature:
         decimation = sensorIntervalMs[feature] / observationWindowMs
         counter[feature]++
         emit only if counter[feature] % decimation == 0

Result in the common case (all sensors at 1 s, observationWindowMs=1000):
  decimation = 1 → emit every frame   (same as before)

Result with mixed intervals (rms=1 s, kurtosis=10 s, observationWindowMs=1000):
  Frame 1  → rms ✓ (1%1=0), kurtosis ✓ (first call always emits)
  Frame 2  → rms ✓, kurtosis ✗ (2%10≠0)
  ...
  Frame 11 → rms ✓, kurtosis ✓ (11%10=1 — wait, 10%10=0 at frame 10)
  Frame 10 → rms ✓, kurtosis ✓ (10%10=0)
```

Key property: **FFT computation always runs** on every frame (so no data is lost for longer-interval
features — they always use the most recent window). Only the cloud upload is gated.

---

## 3. Implementation

### 3.1 `PhmFeatureTransform.cs`

Replace the `_lastEmitTime` field with `_observationWindowMs` and `_frameCounters`:

```csharp
// fields
private readonly Dictionary<string, int> _sensorIntervalMs;  // Per-sensor upload interval (ms)
private readonly int _observationWindowMs;                    // ObservationWindowSeconds × 1000
private readonly Dictionary<string, int> _frameCounters = new();  // Frames-since-last-emit counter per sensor
```

New constructor signature (add `observationWindowMs` after `sensorIntervalMs`):

```csharp
public PhmFeatureTransform(
    int samplingRate,
    int fftSize,
    string rawPayloadResourceId,
    Dictionary<string, string>? phMSensorResourceIds = null,
    Dictionary<string, int>? sensorIntervalMs = null,
    int observationWindowMs = 1000,                             // ← new
    TimeDomainExtractor? timeDomain = null,
    FrequencyDomainExtractor? frequencyDomain = null,
    ILogger<PhmFeatureTransform>? logger = null)
{
    ...
    _sensorIntervalMs = sensorIntervalMs ?? new Dictionary<string, int>();
    _observationWindowMs = observationWindowMs > 0 ? observationWindowMs : 1000;
}
```

Replace the per-feature gate inside `TransformAsync`:

```csharp
foreach (var (sensorName, value) in sensorNameMappings)
{
    var intervalMs = _sensorIntervalMs.GetValueOrDefault(sensorName, 1000);
    var decimation = Math.Max(1, intervalMs / _observationWindowMs);

    if (!_frameCounters.TryGetValue(sensorName, out var counter))
        counter = decimation - 1;  // initialize so first call always emits

    counter++;
    _frameCounters[sensorName] = counter % decimation;

    var isDue = counter % decimation == 0;

    if (!isDue)
    {
        skippedSensors.Add(sensorName);
        continue;
    }
    ...
}
```

### 3.2 `UniaxialVibrationDevice.cs` — `RegisterPhmTransform()`

Read each PHM sensor's `Report.Interval` and pass the resulting dictionary to the transform
constructor:

```csharp
// Build per-sensor interval map from devicecfg.json Report.Interval
var sensorIntervals = sensorMapping.ToDictionary(
    kvp => kvp.Key,
    kvp => (int)kvp.Value.Report.Interval);

var transform = new PhmFeatureTransform(
    samplingRate: acquisitionRate,
    fftSize: frameSize,
    rawPayloadResourceId: resourceMapping.RawSensorResourceId,
    phMSensorResourceIds: resourceMapping.PhMSensorResourceIds,
    sensorIntervalMs: sensorIntervals,
    observationWindowMs: (int)(observationWindowSeconds * 1000),  // ← new
    logger: loggerFactory.CreateLogger<PhmFeatureTransform>());
```

Update the log line to include the interval map:

```csharp
_logger.LogInformation(
    "PhmFeatureTransform registered: AcquisitionRate={rate} Hz, FrameSize={frameSize}, " +
    "PHM sensors={phMCount}, Intervals=[{intervals}]",
    acquisitionRate, frameSize, resourceMapping.PhMSensorResourceIds.Count,
    string.Join(", ", sensorIntervals.Select(kv => $"{kv.Key}:{kv.Value}ms")));
```

### 3.3 `devicecfg.json`

`Report.Interval` on each PHM sensor now controls its cloud-upload frequency. Every value must be
a positive integer multiple of `ObservationWindowSeconds × 1000` (validated at startup by
`OnBeforeInitializeAsync`).

Example with two reporting tiers:

```json
{ "Name": "x_axis_rms_mg",                ..., "Report": { "Enabled": true, "Interval": 1000  } },
{ "Name": "x_axis_peak_mg",               ..., "Report": { "Enabled": true, "Interval": 1000  } },
{ "Name": "x_axis_peak_to_peak_displacement",...,"Report": { "Enabled": true, "Interval": 1000  } },
{ "Name": "x_axis_oa_velocity",           ..., "Report": { "Enabled": true, "Interval": 1000  } },
{ "Name": "x_axis_deviation",             ..., "Report": { "Enabled": true, "Interval": 1000  } },
{ "Name": "x_axis_skewness",              ..., "Report": { "Enabled": true, "Interval": 10000 } },
{ "Name": "x_axis_kurtosis",              ..., "Report": { "Enabled": true, "Interval": 10000 } },
{ "Name": "x_axis_crest_factor",          ..., "Report": { "Enabled": true, "Interval": 10000 } },
{ "Name": "timestamp_timestamp",          ..., "Report": { "Enabled": true, "Interval": 1000  } },
{ "Name": "device_time",                  ..., "Report": { "Enabled": true, "Interval": 1000  } }
```

---

## 4. Side Effects and Constraints

### 4.1 `GroupSensorsByInterval()` Creates Harmless Extra Tasks

The framework's `GroupSensorsByInterval()` will create separate sampling tasks for sensors with
different intervals. Since PHM sensors are never in `SensorCache`, those tasks run on schedule but
always read zero measures — a negligible no-op. The actual upload gating is handled solely by the
transform's time gate.

### 4.2 First Emission Always Sends All Features

On first call, `_frameCounters` has no entry for the sensor. The counter is initialized to
`decimation - 1`, so after the first increment it equals `decimation`, and `decimation % decimation
== 0` → emit. This is true for any decimation factor, so every feature emits on the very first
frame regardless of its configured interval — providing an immediate complete snapshot at startup.

### 4.3 Interval Must Be an Integer Multiple of `ObservationWindowSeconds`

`decimation = intervalMs / _observationWindowMs` uses **integer division**. If `Report.Interval =
10000` ms and `ObservationWindowSeconds = 1.0` s (`observationWindowMs = 1000`), decimation = 10
and kurtosis emits exactly every 10th frame — no timing jitter because there is no wall-clock
comparison. Values that are not integer multiples of `ObservationWindowSeconds × 1000` produce a
non-integer decimation factor (truncated), which causes the effective upload period to differ from
the configured value. Such values are rejected at startup by `OnBeforeInitializeAsync`.

### 4.4 No Effect on FFT Computation

The time gate only filters the **output** of `TransformAsync`. The FFT and time-domain extractors
always run on every frame. For long-interval features (e.g., 60 s), this means 59 out of 60
computed values are discarded — a deliberate trade-off that ensures the emitted value always
reflects the **most recent** observation window rather than an old cached result.

### 4.5 ValidateConfigurationUpdate Is Unchanged

Cloud configuration updates validate `Report.Interval` against the current `ObservationWindowSeconds`
using integer-multiple rules (same as startup validation). The transform picks up new intervals
automatically on the next frame after a config update because `RegisterPhmTransform()` is called
again during `OnAfterInitializeAsync` following a config reload.

---

## 5. Comparison with Alternative Approaches

| Approach | Framework changes | Per-feature control | Notes |
|---|---|---|---|
| **Transform time gate** (this doc) | None | ✓ | Implemented in example project only |
| Write transform output to SensorCache | Yes (framework) | ✓ | Cleaner architecture, larger scope |
| Single global interval (current state) | None | ✗ | All features at same rate |
| Multiple PhmFeatureTransform instances | None | Partial | Complex wiring, same root problem |
