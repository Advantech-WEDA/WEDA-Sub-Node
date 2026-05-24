# Configuration Parameter Redesign Proposal

> Status: Draft — Updated to align with User Story (AC1–AC4)
>
> Context: This document captures the design discussion on improving the DAQ data collector
> configuration parameters to be more intuitive and physically meaningful for system integrators.

---

## Problem Statement

### Current User Problem

When measuring **low-frequency signals**, the raw sample count for effective FFT computation may
be insufficient. Users need to increase the **sampling time window** (observation duration) to
improve frequency resolution for detecting lower-frequency components.

**Reference**: WISE-2410 is reported to use approximately a 1-minute collection window (exact
semantics — whether sampling window or telemetry interval — to be confirmed).

### Constraints

- **More samples → finer frequency resolution**: The frequency resolution is bounded by the
  reciprocal of the analysis window duration: $\Delta f = 1 / T$
  
  > **Why this formula?** FFT's frequency resolution is $\Delta f = f_s / N$, where $f_s$ is sampling rate and $N$ is sample count. Since $N = f_s \times T$, we get $\Delta f = f_s / (f_s \times T) = 1 / T$. Longer observation windows → finer resolution.

- **Fewer samples → coarser frequency resolution**: Short windows cannot resolve closely spaced
  low-frequency components
- **Low-frequency signal requirement**: To accurately characterize a signal at frequency $f_{\min}$,
  the analysis window must satisfy $T \geq 1 / f_{\min}$

---

## Root Cause: Current Parameter Design Issues

### Current Parameters

```json
"Properties": {
  "AccelerationSamplingRate": 2000,
  "TelemetryInterval": 1000,
  "DecimationFactor": 2
}
```

### The Hidden Calculation

The actual analysis window is not directly configured — it is derived from two unrelated parameters
through a non-obvious formula:

```
AnalysisWindowSeconds = TelemetryInterval / (1000 × DecimationFactor)
                      = 1000 / (1000 × 2)
                      = 0.5 seconds

FrameSize = AccelerationSamplingRate × AnalysisWindowSeconds
          = 2000 × 0.5 = 1000 samples

FrequencyResolution = 1 / AnalysisWindowSeconds = 2 Hz
```

### Identified Issues

| Issue | Description |
|-------|-------------|
| **Hidden key parameter** | Analysis window (0.5 s) is not directly configurable; users must understand the formula |
| **Confusing semantics** | `TelemetryInterval` sounds like a reporting interval, but also controls the analysis window indirectly |
| **Opaque `DecimationFactor`** | Not physically intuitive; users must know what "decimation" means |
| **Low-freq limitation** | Current 0.5 s window gives only 2 Hz frequency resolution — insufficient for low-speed machinery |

### Frequency Resolution vs. Analysis Window (at 2000 Hz sampling rate)

| Target Lowest Frequency | Required Resolution | Analysis Window | Samples Required |
|------------------------|---------------------|-----------------|-----------------|
| ~50 Hz (3000 RPM shaft) | ≤ 5 Hz | 0.2 s | 400 |
| ~10 Hz | ≤ 1 Hz | 1 s | 2,000 |
| ~1 Hz | ≤ 0.1 Hz | 10 s | 20,000 |
| ~0.1 Hz (WISE-2410 scale) | ≤ 0.017 Hz | 60 s | 120,000 |

---

## Selected Design

### Separate Analysis Window and Per-Sensor Reporting Intervals ✅

```json
{
  "Properties": {
    "AcquisitionRateHz": 2000,
    "ObservationWindowSeconds": 1.0
  },
  "Sensors": [
    {
      "Name": "x_axis_rms_mg",
      "Report": {
        "Enabled": true,
        "Interval": 1000
      }
    },
    {
      "Name": "x_axis_kurtosis",
      "Report": {
        "Enabled": true,
        "Interval": 60000
      }
    }
  ]
}
```

- `ObservationWindowSeconds`: How long to observe (controls FFT quality)
- `Sensors[].Report.Interval`: Per-sensor reporting interval (each sensor can have independent interval)
- `DecimationFactor` is auto-derived internally per-sensor and no longer user-facing

**Selected** because it preserves independent control of signal quality and per-sensor reporting cadence.

## User Mental Model

A system integrator configuring this design faces **independent questions per layer**:

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Question 1: Signal Quality Layer (Shared by all sensors)                 │
│   "How long do I need to observe to analyze accurately?"                 │
│   → ObservationWindowSeconds (device-level)                                 │
│                                                                          │
│ Question 2: Per-Sensor System Requirements Layer                         │
│   "How often does my cloud need THIS SPECIFIC feature?"                  │
│   → Sensors[].Report.Interval (per-sensor, independent)                  │
└──────────────────────────────────────────────────────────────────────────┘
```

These two concerns are inherently independent at different levels:
- Device level: All sensors share the same analysis window (signal acquisition)
- Sensor level: Each feature can have its own reporting interval (business logic)

The current design conflates them through an implicit formula, forcing users to understand the internals.

---

### Step-by-Step Configuration Workflow

**Step 1 — Set sampling rate (hardware capability + signal requirement)**

> "What is the highest frequency component I need to detect? My hardware's sampling rate must satisfy the Nyquist condition:"

$$f_s \geq 2 \times f_{\max}$$

The integrator determines the minimum required sampling rate from their highest target frequency, then selects a rate from the hardware specification that meets this requirement.

---

**Step 2 — Set observation window (signal requirement driven)**

> "What is the lowest frequency component I care about?"

$$\text{ObservationWindowSeconds} \geq \frac{1}{\text{Lowest target frequency (Hz)}}$$

Example decisions:

- "My machine runs at 3000 RPM (50 Hz). I want to detect 1X–5X harmonics. 1-second window → 1 Hz resolution — good enough."
- "I have low-speed machinery at ~1 Hz. I need a 10-second window for 0.1 Hz resolution."
- "I want WISE-2410-equivalent analysis. 60-second window → 0.017 Hz resolution."

Internally derived (shown in startup log for verification, not configurable):

```
FrameSize          = AcquisitionRateHz × ObservationWindowSeconds
FrequencyResolution = 1 / ObservationWindowSeconds
NyquistFrequency   = AcquisitionRateHz / 2
```

---

**Step 3 — Set per-sensor reporting interval (system integration driven)**

> "How often does my monitoring system need THIS FEATURE's updates?"

This decision is independent of signal analysis and made **per-sensor**:

| Feature | Typical Interval | Rationale |
|---------|-----------------|----------|
| RMS (real-time state) | = ObservationWindowSeconds | Report every frame |
| Peak (trend monitoring) | 10–60 s | Less frequent |
| Kurtosis (deep analysis) | 60–300 s | Rare, expensive to compute |

Internally derived per-sensor (not user-facing):

```
DecimationFactor[sensor] = Sensor.Report.Interval / (ObservationWindowSeconds × 1000)
```

---

### Example Configuration

```json
{
  "Properties": {
    "AcquisitionRateHz": 2000,
    "ObservationWindowSeconds": 1.0
  },
  "Sensors": [
    {
      "Name": "x_axis_rms_mg",
      "Report": {
        "Enabled": true,
        "Interval": 1000
      }
    },
    {
      "Name": "x_axis_kurtosis",
      "Report": {
        "Enabled": true,
        "Interval": 60000
      }
    }
  ]
}
```

**Startup log output** (for user verification):

```
DAQ Configuration:
  AcquisitionRate     = 2000 Hz
  ObservationWindow      = 1.0 s  →  FrameSize = 2000 samples, FreqResolution = 1.0 Hz
  NyquistFrequency    = 1000 Hz
  
Sensor reporting intervals:
  - x_axis_rms_mg: Interval=1000ms (= 1× ObservationWindow) → DecimationFactor=1
  - x_axis_kurtosis: Interval=60000ms (= 60× ObservationWindow) → DecimationFactor=60
```

---

## Configuration Validation

### Device-Level Parameter Validation

`AcquisitionRateHz` and `ObservationWindowSeconds` have defaults and are applied if omitted. The service refuses to start only if an explicitly set value is invalid:

| Condition | Behavior |
|---|---|
| `AcquisitionRateHz` non-positive or wrong type | Service refuses to start; error message identifies the field |
| `ObservationWindowSeconds` non-positive or wrong type | Service refuses to start; error message identifies the field |

> These parameters are validated at startup only and cannot be updated at runtime via command.

### Per-Sensor Parameter Constraints

```
Sensor.Report.Interval ≥ (ObservationWindowSeconds × 1000)     (required)
Sensor.Report.Interval should be a multiple of ObservationWindowSeconds × 1000  (recommended, or warn)

DecimationFactor[sensor] = Sensor.Report.Interval / (ObservationWindowSeconds × 1000)   (auto-derived per-sensor)
```

| Relationship | At Startup | At Runtime (command update) |
|---|---|---|
| `Interval = ObservationWindow × 1000` | ✅ OK | ✅ OK |
| `Interval > ObservationWindow × 1000` | ✅ OK | ✅ OK |
| `Interval < ObservationWindow × 1000` | ❌ Service refuses to start; error identifies the offending sensor | ❌ Change rejected; previous configuration retained; error identifies the offending sensor |
| `Interval` not an exact multiple of `ObservationWindow × 1000` | ⚠️ Warning logged; rounds down to `N × ObservationWindow × 1000` ms | ⚠️ Warning logged; rounds down to `N × ObservationWindow × 1000` ms |

> Runtime validation is performed in `ValidateConfigurationUpdate()`. Returning a failure result causes the framework to discard the update and retain the previous configuration automatically.

---

## Behavior When Sensor.Report.Interval > ObservationWindowSeconds

Multiple FFT windows are computed in each telemetry period. The current SensorCache design uses
**last-write-wins semantics**: only the most recently computed frame's features are reported.

| Behavior | Description | Suitability |
|----------|-------------|-------------|
| Report latest frame *(current)* | Most recent PHM feature values | ✅ Real-time state monitoring |
| Report average across frames *(not implemented)* | Feature values smoothed over period | Trend analysis — higher complexity |

The current architecture naturally supports "latest frame" with no additional design required.

---

## Derived Parameters (Internal Only — Not User-Configurable)

| Parameter | Derivation | Exposed to User |
|-----------|-----------|----------------|
| `FrameSize` | `AcquisitionRateHz × ObservationWindowSeconds` | Log only |
| `FftSize` | `= FrameSize` (full-frame FFT) | Log only |
| `FrequencyResolution` | `1 / ObservationWindowSeconds` | Log only |
| `NyquistFrequency` | `AcquisitionRateHz / 2` | Log only |
| `DecimationFactor[sensor]` | `Sensor.Report.Interval / (ObservationWindowSeconds × 1000)` | Log only (per-sensor) |
| `FrameIntervalSeconds` | `= ObservationWindowSeconds` | Internal only |

---

## Multi-Interval Support: Per-Sensor Configuration

**Decision (Resolved)**: Different sensors now support independent reporting intervals via `Sensors[].Report.Interval` (in milliseconds).

### Rationale

The framework's `GroupSensorsByInterval()` already supports per-sensor reporting intervals. This allows:
- High-frequency features (RMS, Peak): Report every analysis window (e.g., 1000 ms)
- Low-frequency features (Kurtosis, trend analysis): Report less frequently (e.g., 60000 ms)
- Fine-grained control for system integrators to optimize cloud bandwidth and feature freshness independently

### Configuration Example

```json
"Sensors": [
  {
    "Name": "x_axis_rms_mg",
    "Report": {
      "Enabled": true,
      "Interval": 1000
    }
  },
  {
    "Name": "x_axis_kurtosis",
    "Report": {
      "Enabled": true,
      "Interval": 60000
    }
  }
]
```

### Constraint and Unit Alignment

**Unit Declaration (Decision: Plan A — Mixed Units)**

- `ObservationWindowSeconds`: Configured in **seconds** (e.g., `1.0`, `60.0`)
- `Sensor.Report.Interval`: Configured in **milliseconds** (e.g., `1000`, `60000`)
- This follows the framework's existing convention for `Report.Interval`

**Validation Rule**

Each sensor's `Report.Interval` (ms) must satisfy:

```
Sensor.Report.Interval ≥ (ObservationWindowSeconds × 1000)
```

**Integer Multiple Handling (Decision: Warn and Round Down)**

The ratio should ideally be an integer:

```
DecimationFactor = Sensor.Report.Interval / (ObservationWindowSeconds × 1000)
```

If the ratio is **not an exact integer**:
1. **Warn** in the startup log with the mismatch details
2. **Round down** to the nearest integer (floor)
3. Continue execution with the rounded value

This is lenient to accommodate configuration flexibility while alerting the operator to potential misalignment.

**Examples**

| ObservationWindowSeconds | Report.Interval | Exact Ratio | DecimationFactor | Action |
|---|---|---|---|---|
| 1.0 s | 1000 ms | 1.0 | 1 | ✅ Exact |
| 1.0 s | 5000 ms | 5.0 | 5 | ✅ Exact |
| 1.0 s | 1500 ms | 1.5 | 1 | ⚠️ Warn, round down |
| 1.0 s | 900 ms | 0.9 | 0 | ❌ Invalid; < 1 |
| 60.0 s | 60000 ms | 1.0 | 1 | ✅ Exact |

---

## Implementation Decisions

### 1️⃣ Migration Strategy

**Decision: Direct Migration (No Backward Compatibility)**

- Migrate directly to the new configuration format
- Old parameters (`TelemetryInterval`, `DecimationFactor`) are removed; no automatic conversion
- Configuration files must be manually updated
- This ensures a clean break and no confusion between old and new semantics

### 2️⃣ Default Values

| Parameter | Default Value | Status |
|-----------|---------------|--------|
| `AcquisitionRateHz` | 1000 Hz | ✅ Decided |
| `ObservationWindowSeconds` | 1.0 s | ✅ Decided |
| `Sensor.Report.Interval` | 1000 ms | ✅ Decided (hard-coded in program) |

**Rationale for defaults**:
- **1000 Hz**: Covers up to 500 Hz (Nyquist), sufficient for typical industrial rotating machinery analysis (shaft harmonics and bearing defect frequencies). Provides a ready-to-use baseline without requiring hardware knowledge upfront.
- **1.0 s**: Provides 1 Hz frequency resolution; covers rotating machinery above ~60 RPM. For low-speed machinery (< 60 RPM), increase proportionally (e.g., 10 s for 6 RPM)
- **1000 ms** (`Sensor.Report.Interval`): Reports every analysis frame; real-time monitoring

### 3️⃣ Multi-Sensor Reporting

**Decision: Independent Per-Sensor Reporting**

Each sensor reports independently according to its own `Report.Interval`. No aggregation or synchronization across sensors.

**Example**:
- Sensor A (`x_axis_rms_mg`): Reports at 1000 ms intervals
- Sensor B (`x_axis_kurtosis`): Reports at 60000 ms intervals
- Messages are sent separately, not combined

This simplifies the architecture and allows fine-grained control per feature.

### 4️⃣ Code Replacement Strategy

**Decision: Complete Replacement, No Legacy Support**

- Use per-sensor decimation logic directly in new code
- Remove global `DecimationFactor` usage
- All code paths transition to the new parameterization
- No conditional logic for old vs. new format

---

## Open Questions

| # | Question | Status |
|---|----------|--------|
| 1 | Should different sensors support different `TelemetryIntervalSeconds`? | ✅ **RESOLVED** — Implemented via `Sensors[].Report.Interval` |
| 2 | Does WISE-2410 use a 1-minute *acquisition window* or a 1-minute *telemetry interval*? | **To confirm** — affects baseline ObservationWindowSeconds recommendation |
| 3 | Should non-multiple `Sensor.Report.Interval / ObservationWindowSeconds` be rejected (error) or warned (round down)? | ✅ **RESOLVED** — **Warn and round down**; flexible but logged |
| 4 | Is `ObservationWindowSeconds` default of 1.0 s reasonable for all use cases? | ✅ **RESOLVED** — **1.0 s**; covers rotating machinery above ~60 RPM; users requiring low-speed analysis set explicitly per Step 2 workflow |

---

## Impact on Implementation

The following implementation changes are required:

### `devicecfg.json`

#### Device-Level Properties

```json
"Properties": {
  "AcquisitionRateHz": 2000,
  "ObservationWindowSeconds": 1.0
}
```

#### Per-Sensor Interval Configuration

```json
"Sensors": [
  {
    "Name": "x_axis_rms_mg",
    "Report": {
      "Enabled": true,
      "Interval": 1000
    }
  },
  {
    "Name": "x_axis_kurtosis",
    "Report": {
      "Enabled": true,
      "Interval": 60000
    }
  }
]
```

**Remove**: 
- `TelemetryInterval` (ms) from Properties
- `TelemetryIntervalSeconds` from Properties (no longer needed)
- `DecimationFactor` from Properties (auto-derived per sensor)

### `UniaxialVibrationDevice.cs` — `CreateStreamingParser()`

#### Read and Validate Device-Level Configuration

```csharp
// Apply defaults if omitted; error only if an explicitly set value is invalid
var acquisitionRate = props.TryGetValue("AcquisitionRateHz", out var ar)
    ? int.Parse(ar!.ToString()!) : 1000;
if (acquisitionRate <= 0)
    throw new InvalidOperationException(
        $"AcquisitionRateHz must be a positive integer, got: {acquisitionRate}");

var observationWindowSeconds = props.TryGetValue("ObservationWindowSeconds", out var ow)
    ? double.Parse(ow!.ToString()!) : 1.0;
if (observationWindowSeconds <= 0)
    throw new InvalidOperationException(
        $"ObservationWindowSeconds must be a positive number, got: {observationWindowSeconds}");

var observationWindowMs = (int)(observationWindowSeconds * 1000);
var frameSize = (int)(acquisitionRate * observationWindowSeconds);
```

#### Read Per-Sensor Intervals and Validate

```csharp
foreach (var sensor in sensorConfigs)
{
    var reportInterval = sensor.Report?.Interval ?? observationWindowMs;
    
    // Validate: Report.Interval >= ObservationWindowSeconds × 1000
    if (reportInterval < observationWindowMs)
        throw new InvalidOperationException(
            $"Sensor '{sensor.Name}' Report.Interval ({reportInterval}ms) must be >= " +
            $"ObservationWindowSeconds ({observationWindowSeconds}s = {observationWindowMs}ms)");
    
    // Calculate decimation factor and check for non-integer multiple
    double exactRatio = (double)reportInterval / observationWindowMs;
    int decimationFactor = (int)Math.Floor(exactRatio);
    
    if (Math.Abs(exactRatio - decimationFactor) > 0.001)
    {
        _logger.LogWarning(
            "Sensor {sensorName}: Report.Interval ({interval}ms) is not an exact multiple of " +
            "ObservationWindowSeconds ({observationWindow}s = {observationWindowMs}ms). " +
            "Exact ratio: {exactRatio}; rounding down to DecimationFactor={decimationFactor}",
            sensor.Name, reportInterval, observationWindowSeconds, observationWindowMs, 
            exactRatio, decimationFactor);
    }
    
    _logger.LogInformation(
        "Sensor {sensorName}: Interval={interval}ms (= {ratio}× ObservationWindow) → DecimationFactor={decimationFactor}",
        sensor.Name, reportInterval, exactRatio, decimationFactor);
}
```

#### Startup Log Output

```csharp
_logger.LogInformation(
    "DAQ Configuration: AcquisitionRate={fs} Hz, ObservationWindow={T}s " +
    "→ FrameSize={N}, FreqResolution={df} Hz, Nyquist={fn} Hz",
    acquisitionRate, observationWindowSeconds,
    frameSize, 1.0 / observationWindowSeconds, acquisitionRate / 2.0);

_logger.LogInformation(
    "Sensor reporting intervals: {sensorIntervals}",
    string.Join(", ", sensorConfigs.Select(s => $"{s.Name}={s.Report?.Interval ?? observationWindowMs}ms")));
```

#### Startup Validation — `OnBeforeInitializeAsync()`

```csharp
protected override Task OnBeforeInitializeAsync(CancellationToken ct)
{
    var props = Configuration.Properties;

    // Apply defaults if omitted; error only on invalid values
    var rate = props.TryGetValue("AcquisitionRateHz", out var ar) &&
               int.TryParse(ar?.ToString(), out var r) ? r : 1000;
    if (rate <= 0)
        throw new InvalidOperationException(
            $"AcquisitionRateHz must be a positive integer, got: {rate}");

    var window = props.TryGetValue("ObservationWindowSeconds", out var ow) &&
                 double.TryParse(ow?.ToString(), out var w) ? w : 1.0;
    if (window <= 0)
        throw new InvalidOperationException(
            $"ObservationWindowSeconds must be a positive number, got: {window}");

    // Validate per-sensor Report.Interval
    var observationWindowMs = (int)(window * 1000);
    foreach (var sensor in Configuration.Sensors ?? [])
    {
        var interval = sensor.Report?.Interval ?? observationWindowMs;
        if (interval < observationWindowMs)
            throw new InvalidOperationException(
                $"Sensor '{sensor.Name}' Report.Interval ({interval}ms) must be >= " +
                $"ObservationWindowSeconds ({window}s = {observationWindowMs}ms).");
    }

    return base.OnBeforeInitializeAsync(ct);
}
```

#### Runtime Configuration Validation — `ValidateConfigurationUpdate()`

```csharp
// Called when a configuration update arrives at runtime via WEDA node command.
// Returning failure causes the framework to discard the update and retain the previous configuration.
protected override ConfigurationValidationResult ValidateConfigurationUpdate(
    SubNodeConfigUpdateMessage message)
{
    var baseResult = base.ValidateConfigurationUpdate(message);
    if (!baseResult.IsValid)
        return baseResult;

    var props = Configuration.Properties;
    if (!double.TryParse(props["ObservationWindowSeconds"]?.ToString(), out var window) || window <= 0)
        return ConfigurationValidationResult.Failure("ObservationWindowSeconds is invalid.");

    var observationWindowMs = (int)(window * 1000);
    foreach (var sensor in message.Sensors ?? [])
    {
        var interval = sensor.Report?.Interval ?? observationWindowMs;
        if (interval < observationWindowMs)
            return ConfigurationValidationResult.Failure(
                $"Sensor '{sensor.Name}' Report.Interval ({interval}ms) must be >= " +
                $"ObservationWindowSeconds ({window}s = {observationWindowMs}ms). Update rejected.");
    }

    return ConfigurationValidationResult.Success;
}
```

---

## Related Documents

- [arch_plan_daq_data_collector.md](./arch_plan_daq_data_collector.md) — System architecture and design decisions
- [impl_guide_daq_data_collector.md](./impl_guide_daq_data_collector.md) — Implementation phases and checklist
- [feature_extraction_impl_plan.md](./feature_extraction_impl_plan.md) — PHM feature algorithms and DAQ configuration baseline
- [config/devicecfg_dataflow.md](./config/devicecfg_dataflow.md) — How devicecfg.json is consumed by the pipeline
