# Implementation Guide: DAQ Data Collector Refactoring

> Architecture decisions and system design are documented in
> [arch_plan_daq_data_collector.md](./arch_plan_daq_data_collector.md).

---

## Implementation Progress Summary

**Status as of 2026-05-04 (C5 Complete, C6 Ready)**

| Phase       | Objective                     | Completion | Build Status           |
| ----------- | ----------------------------- | ---------- | ---------------------- |
| **Phase A** | Configuration & Project Setup | ✅ 100%     | ✓ 0 errors             |
| **Phase B** | Class Skeleton & Inheritance  | ✅ 100%     | ✓ 0 warnings, 0 errors |
| **Phase C** | Feature Implementation        | ⏳ 90%      | Pending build          |

**Key Achievements (Phase C - Latest)**:
- **C1 Complete**: Model properties fully defined (`DaqRawFrame`, `DaqPhMFeatures`)
- **C2 Complete**: DaqMetricsParser fully implemented with decimation logic and stream lifecycle management
- **C3 Complete**: Time-domain & frequency-domain extractors implemented using MathNet.Numerics
- **C4 Complete**: PhmFeatureTransform fully implemented—parses JSON, calls extractors, emits 10 TelemetryMeasure
- **C5 Complete**: Hardware streaming fully implemented
  - DaqCollector: DAQ module discovery, streamer configuration, sample accumulation
  - DaqCommunication: JSON serialization of raw frames
- **Configuration Refactored**: Simplified to essential parameters only
  - `AccelerationSamplingRate`: 2500 Hz
  - `FrameIntervalSeconds`: 1.0 (auto-calculates FrameSize as SamplingRate × Interval)
  - `DecimationFactor`: 2
  - ✅ Removed: FrameSize (auto-calculated), FftSize (not used), AxisName (hardcoded to "X")
- **Pending**: C6 (PhmFeatureTransform Registration & Device Initialization)

---

## Target Directory Structure

```
examples/daq-data-collector/
├── Program.cs                          [UPDATE]
├── Devices/
│   └── UniaxialVibrationDevice.cs      [NEW]  device
├── Communication/
│   ├── DaqCommunication.cs             [NEW]  acquisition and raw frame delivery
│   ├── DaqCollector.cs                 [NEW]  Advantech.Edge.Daq streaming buffer
│   └── Pipeline/
│       ├── PhmFeatureTransform.cs      [NEW]  Transform-stage feature extraction
│       ├── TimeDomainExtractor.cs      [NEW]  reused by PhmFeatureTransform
│       └── FrequencyDomainExtractor.cs [NEW]  reused by PhmFeatureTransform
├── Protocols/
│   └── DaqMetricsParser.cs             [NEW]  implements decimation frame counter; only selected frames trigger OnTelemetryReceived
│   └── DaqMetricsParser.cs             [NEW]  implements decimation frame counter; only selected frames trigger OnTelemetryReceived
├── Models/
│   ├── DaqRawFrame.cs                  [NEW]
│   ├── DaqPhMFeatures.cs               [NEW]
│   └── DaqRequest.cs                   [DELETE]
├── daq-data-collector.csproj           [UPDATE]
├── devicecfg.json                      [UPDATE]
├── appsettings.json                    [UPDATE]
└── MyFirstDevice.cs                    [DELETE]
```

---

## Data Flow

```
1. Stream Initialization (Background task started by StreamingDeviceBase)
────────────────────────────────────────────────────────────────────────
DaqMetricsParser.StartStreamAsync()
    │
    ├─► DaqCommunication.StreamAsync() ← builds bidirectional stream
    │       │
    │       └─► DaqCollector (continuous producer)
    │               DaqModuleManager → CreateDaqModule()
    │               module.Analog.Input.ConfigureStreamer()
    │                   .WithScanRange(StartChannel: N, EndChannel: N) → IStreamer
    │               await foreach (buffer in streamer.StreamAsync())
    │                   accumulate samples → FrameSize (2,500) → yield DaqRawFrame
    │
    └─► Consume incoming stream items:
            await foreach (rawFrame in communicationStream)
                emit raw telemetry measure → SensorCache.Write()


2. Polling at Interval (per sensor interval, triggered by StreamingDeviceBase)
────────────────────────────────────────────────────────────────────────
Device.ReadTelemetryAsync() [sampled from SensorCache]
    │
    ▼
SensorCache.Read(enabledSensorIds)
    │ ← returns latest cached raw telemetry measure
    ▼
TelemetryPipeline.TransformAndFilterAsync()
    │
    ├─► PhmFeatureTransform.TransformAsync(raw measure)
    │       Parse raw payload → DaqRawFrame
    │       TimeDomainExtractor → Deviation, Skewness, Kurtosis, CrestFactor
    │       FrequencyDomainExtractor → RMSmg, Peakmg, PeakToPeakDisplacement, OAVelocity
    │       (FFT size = 2,500 samples, aligned with frame size)
    │       Output: 10 PHM TelemetryMeasure
    │
    └─► (DSP Filter stage: unused in this phase)
    │
    ▼
List<TelemetryMeasure> (10 PHM features) → batch send → cloud telemetry
```

---

## Implementation Phases

**Execution Strategy**: Bottom-up configuration + skeleton-first approach.
- **Phase A** builds the project foundation (config, entry point, dependencies)
- **Phase B** establishes all class hierarchies and method signatures (no implementation)
- **Phase C** implements feature logic (skeleton remains buildable throughout)

---

### Phase A — Configuration & Project Setup *(Foundation: no code dependencies)*

| #   | File                        | Change                                                                                                                                                                                                                                                                                                                                                                                                  |
| --- | --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | `daq-data-collector.csproj` | Add `Advantech.Edge` `PackageReference`; remove `Weda.SubNode.Simulators` `ProjectReference`                                                                                                                                                                                                                                                                                                            |
| 2   | `devicecfg.json`            | Remove Modbus `DeviceCommunication` block; add DAQ parameters to `Properties` (`SamplingRate`, `FrameSize`, `FftSubframeSize`, `AxisName`); replace Sensors with 9 entries: 1 internal raw payload sensor (`daqraw:vibration:payload`) that feeds the Transform, plus 8 PHM metric sensors. `Timestamp_Timestamp` and `Device_Time` are emitted as transform metadata measures, not standalone sensors. |
| 3   | `appsettings.json`          | Remove `TcpModbusSimulatorConfiguration` section                                                                                                                                                                                                                                                                                                                                                        |
| 4   | `Program.cs`                | Use `WedaApplication.CreateBuilder()` with full feature chain (`.AddLogging()`, `.AddTelemetry()`, `.AddHealthReporting()`, `.AddConfigUpdates()`, `.AddCommands()`, `.AddRecording()`); register `UniaxialVibrationDevice`; remove all simulator code                                                                                                                                                  |

**Completion Goal**: Project compiles, config is in place, entry point ready to register device.

---

### Phase B — Class Skeleton & Inheritance *(Method signatures, no logic)*

#### Models Layer *(depends on Phase A)*

| #   | File                       | Description                                                                                            |
| --- | -------------------------- | ------------------------------------------------------------------------------------------------------ |
| 5   | `Models/DaqRawFrame.cs`    | Class definition: `float[] Samples`, `DateTimeOffset Timestamp`, `string AxisName`, `int SamplingRate` |
| 6   | `Models/DaqPhMFeatures.cs` | Class definition: 10 computed PHM feature fields + `Timestamp` / `DeviceTime` metadata                 |
| 7   | `Models/DaqRequest.cs`     | **Delete** — no longer needed in Streaming pattern (no request trigger concept)                        |

#### Communication Layer *(depends on Phase A)*

| #   | File                                                 | Description                                                                                                 |
| --- | ---------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| 8   | `Communication/DaqCollector.cs`                      | Class skeleton: method signature `IAsyncEnumerable<DaqRawFrame> StreamFramesAsync(...)` — no implementation |
| 9   | `Communication/DaqCommunication.cs`                  | Inherits `StreamingCommunicationBase<object, object>`. Method stubs: `StreamAsync()`.                       |
| 10  | `Communication/Pipeline/TimeDomainExtractor.cs`      | Class skeleton: method stubs for Deviation, Skewness, Kurtosis, CrestFactor computation.                    |
| 11  | `Communication/Pipeline/FrequencyDomainExtractor.cs` | Class skeleton: method stubs for RMSmg, Peakmg, Displacement, OAVelocity computation.                       |
| 12  | `Communication/Pipeline/PhmFeatureTransform.cs`      | Inherits `ITelemetryTransform`. Method stubs: `TransformAsync()`.                                           |

#### Protocol Layer *(depends on Phase A)*

| #   | File                            | Description                                                                                                           |
| --- | ------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| 13  | `Protocols/DaqMetricsParser.cs` | Inherits `IStreamingProtocolParser`. Method stubs: `StartStreamAsync()`, `StopStreamAsync()`, `SendTelemetryAsync()`. |

#### Device Layer *(depends on Phase A + Phase B)*

| #   | File                                 | Description                                                                                           |
| --- | ------------------------------------ | ----------------------------------------------------------------------------------------------------- |
| 14  | `Devices/UniaxialVibrationDevice.cs` | Inherits `StreamingDeviceBase`. Constructor skeleton, method stubs for validation and initialization. |
| 15  | `MyFirstDevice.cs`                   | **Delete**                                                                                            |

**Completion Goal**: Full project compiles without errors. All class hierarchies and method signatures in place. Ready for parallel feature implementation.

---

### Phase C — Feature Implementation *(Method bodies, feature logic)*

#### Phase C1 — Models Property Definitions *(depends on Phase B)*

| #   | File                       | Description                                                                            |
| --- | -------------------------- | -------------------------------------------------------------------------------------- |
| 16  | `Models/DaqRawFrame.cs`    | Implement property getters/setters: `Samples`, `Timestamp`, `AxisName`, `SamplingRate` |
| 17  | `Models/DaqPhMFeatures.cs` | Implement 10 PHM feature properties + metadata fields                                  |

**Completion Goal**: All model properties implemented and ready for use by extractors and transform pipeline.

#### Phase C2 — DaqMetricsParser Complete Implementation *(depends on Phase B + C5)*

| #   | File                                 | Description                                                                                                                                                                                                                                                                                                                             |
| --- | ------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 18  | `Protocols/DaqMetricsParser.cs`      | Implement complete DaqMetricsParser: constructor accepts `DecimationFactor` parameter; maintain thread-safe frame counter (volatile int); `StartStreamAsync()` initializes counter and runs stream consumer loop; only emit `OnTelemetryReceived()` when `counter % factor == 0`; `StopStreamAsync()` closes stream and cleans up resources |
| 19  | `Devices/UniaxialVibrationDevice.cs` | Update `CreateStreamingParser()`: read `DecimationFactor` from `devicecfg.json` Properties, validate > 0, pass to `DaqMetricsParser` constructor                                                                                                                                                                                        |

**Completion Goal**: DaqMetricsParser fully implements decimation (2 Hz → 1 Hz) and stream lifecycle management. Hardware 2 Hz frame push rate decimated to 1 Hz effective update rate matching device polling interval.

#### Phase C3 — Time & Frequency Domain Extractors *(depends on Phase C1)*

| #   | File                                                 | Description                                                                                                                                            |
| --- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 20  | `Communication/Pipeline/TimeDomainExtractor.cs`      | Implement algorithms: Deviation (std dev), Skewness (3rd moment), Kurtosis (4th moment), CrestFactor (peak/RMS).                                       |
| 21  | `Communication/Pipeline/FrequencyDomainExtractor.cs` | Implement FFT analysis: RMSmg (energy via Parseval), Peakmg (max spectrum), Peak-to-Peak Displacement (double integration), OAVelocity (velocity RMS). |

#### Phase C4 — Transform Pipeline *(depends on Phase C3)*

| #   | File                                            | Description                                                                                                                   |
| --- | ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| 22  | `Communication/Pipeline/PhmFeatureTransform.cs` | Implement `TransformAsync()`: parse raw payload → `DaqRawFrame`, call extractors, emit 10 `TelemetryMeasure` for PHM sensors. |

#### Phase C5 — Hardware Streaming *(depends on Phase C1)*

| #   | File                                | Description                                                                                                                                                                                                                     |
| --- | ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 23  | `Communication/DaqCollector.cs`     | Implement `StreamFramesAsync()`: initialize `DaqModuleManager` → `CreateDaqModule()`, configure streamer for target channel, consume hardware buffers, accumulate samples to calculated `FrameSize` (FrameIntervalSeconds × SamplingRate), yield complete `DaqRawFrame`. |
| 24  | `Communication/DaqCommunication.cs` | Implement `StreamAsync()`: connect bidirectional streaming, yield `DaqRawFrame` objects from `DaqCollector` as serialized JSON payload.                                                                                         |

#### Phase C6 — Device Integration & Validation *(depends on Phase C2, C3, C4, C5)*

| #   | File                                                       | Description                                                                                                                                                                                                      |
| --- | ---------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 25  | `Devices/UniaxialVibrationDevice.cs` (Full Implementation) | Create PhmFeatureTransform instance with samplingRate (and fftSize = frameSize for current impl); register it on daqraw:vibration:payload sensor's Report via `AddTransform()`; complete stream initialization and data handler integration. |

**IMPORTANT C6 Notes**:
- ✅ AxisName is hard-coded to "X" in DaqCollector (do NOT reintroduce from config)
- ✅ FrameSize is auto-calculated from SamplingRate × FrameIntervalSeconds (do NOT add back static config)
- ✅ FftSize = FrameSize in current implementation (full-frame FFT); stored locally in FrequencyDomainExtractor only (do NOT add back config)
- The transform must be registered on the raw sensor's Report before the device initializes streaming to ensure telemetry pipeline invokes it

**Completion Goal**: All 10 PHM features computed and reported per telemetry cycle. Full integration test validation passes.

---

## PHM Features Produced

All 10 features correspond to WISE-2410 equivalent parameters for the X-axis.

| #   | Feature                            | Domain    | Algorithm                                                            |
| --- | ---------------------------------- | --------- | -------------------------------------------------------------------- |
| 1   | `Timestamp_Timestamp`              | Metadata  | Hardware sample timestamp                                            |
| 2   | `Device_Time`                      | Metadata  | `DateTimeOffset.UtcNow`                                              |
| 3   | `X-Axis_RMSmg`                     | Frequency | RMS from FFT-transformed spectrum (Parseval energy)                  |
| 4   | `X-Axis_Peakmg`                    | Frequency | Maximum spectral magnitude in FFT spectrum                           |
| 5   | `X-Axis_Peak-to-Peak_Displacement` | Frequency | Peak-to-peak of displacement signal from double spectral integration |
| 6   | `X-Axis_OAVelocity`                | Frequency | Overall velocity RMS from acceleration spectrum                      |
| 7   | `X-Axis_Deviation`                 | Time      | Sample standard deviation                                            |
| 8   | `X-Axis_Skewness`                  | Time      | Third standardized central moment                                    |
| 9   | `X-Axis_Kurtosis`                  | Time      | Fourth standardized central moment                                   |
| 10  | `X-Axis_CrestFactor`               | Time      | Peak / RMS ratio                                                     |

Hardware: B10BG3 (single-axis accelerometer, X-axis only) + idaq-801 + idaq-934 chassis.
Sampling rate: 2,500 Hz. Frame: 2,500 samples (1 second). FFT size: 2,500 samples (full-frame analysis). Frequency resolution: 1 Hz (= fs / FFT_size = 2,500 / 2,500).

---

## Step-by-Step Execution Checklist

### Pre-Implementation

- [x] Review this plan with the team
- [x] Backup the current branch or worktree slice for `examples/daq-data-collector`
- [x] Confirm no conflicting in-flight changes in the example project

### Phase A — Configuration & Project Setup

- [x] Update `daq-data-collector.csproj`: add `Advantech.Edge` package, remove simulator reference
- [x] Update `devicecfg.json`: remove Modbus block, add DAQ properties (`SamplingRate`, `FrameSize`, `FftSize`), add `daqraw:vibration:payload` sensor, update PHM sensor definitions
- [x] Update `appsettings.json`: remove `TcpModbusSimulatorConfiguration`
- [x] Update `Program.cs`: register `UniaxialVibrationDevice`, wire full application builder
- [x] **Verify**: `dotnet build` succeeds with zero errors

**Status**: ✅ Phase A Complete

### Phase B — Class Skeleton & Inheritance

**Models Layer:**
- [x] Create `Models/DaqRawFrame.cs` with properties (no logic)
- [x] Create `Models/DaqPhMFeatures.cs` with 10 feature properties (no logic)
- [x] Delete `Models/DaqRequest.cs`

**Communication Layer:**
- [x] Create `Communication/DaqCollector.cs` with `StreamFramesAsync()` skeleton, parameterized by config
- [x] Create `Communication/DaqCommunication.cs` inheriting `StreamingCommunicationBase<object, object>`, with `ConnectCoreAsync()`, `DisconnectAsync()`, `StreamAsync()` stubs
- [x] Create `Communication/Pipeline/TimeDomainExtractor.cs` with constructor parameter `samplingRate`, method stubs
- [x] Create `Communication/Pipeline/FrequencyDomainExtractor.cs` with constructor parameter `fftSize`, method stubs
- [x] Create `Communication/Pipeline/PhmFeatureTransform.cs` inheriting `ITelemetryTransform`, parameterized by `samplingRate` and `fftSize`

**Protocol & Device Layers:**
- [x] Create `Protocols/DaqMetricsParser.cs` inheriting `IStreamingProtocolParser` with lifecycle method stubs
- [x] Create `Devices/UniaxialVibrationDevice.cs` inheriting `StreamingDeviceBase` with `CreateStreamingParser()` factory
- [x] Delete `MyFirstDevice.cs`

**Parameterization:**
- [x] Extract `AccelerationSamplingRate`, `FrameIntervalSeconds`, `DecimationFactor` from `devicecfg.json` in `UniaxialVibrationDevice`
- [x] Calculate `FrameSize = SamplingRate × FrameIntervalSeconds` in code (not config)
- [x] Pass parameters to `DaqCollector`, `DaqMetricsParser`, `PhmFeatureTransform` appropriately
- [x] Add validation for all numeric parameters (must be > 0)
- [x] Hard-code AxisName="X" (single-axis accelerometer) in DaqCollector
- [x] Hard-code FftSize = FrameSize (full-frame FFT) in FrequencyDomainExtractor

- [ ] **Verify**: `dotnet build` will succeed after C6 completion (0 warnings, 0 errors)

### Phase C — Feature Implementation

**C1: Models Property Definitions**
- [x] Implement `DaqRawFrame` properties
- [x] Implement `DaqPhMFeatures` properties

**C2: DaqMetricsParser Complete Implementation (Decimation + Stream Lifecycle)**
- [x] Implement `DaqMetricsParser.StartStreamAsync()`: initialize counter, run stream consumer loop
- [x] Implement frame decimation logic: increment counter, emit OnTelemetryReceived only when counter % DecimationFactor == 0
- [x] Implement `DaqMetricsParser.StopStreamAsync()`: cleanup and close stream
- [x] Update `UniaxialVibrationDevice.CreateStreamingParser()` to read and pass `DecimationFactor` config

**C3: Time & Frequency Domain Extractors**
- [x] Implement `TimeDomainExtractor` algorithms (Deviation, Skewness, Kurtosis, CrestFactor) — using MathNet.Numerics
- [x] Implement `FrequencyDomainExtractor` algorithms (RMSmg, Peakmg, Displacement, OAVelocity) — using MathNet FFT

**C4: Transform Pipeline**
- [x] Implement `PhmFeatureTransform.TransformAsync()` — parses JSON payload, calls extractors, emits 10 measures

**C5: Hardware Streaming**
- [x] Implement `DaqCollector.StreamFramesAsync()`: DAQ module discovery, streamer config, sample accumulation
- [x] Implement `DaqCommunication.StreamAsync()`: JSON serialization of raw frames
- [x] Refactor configuration: FrameSize → FrameIntervalSeconds (auto-calculated), remove FftSize & AxisName

**C6: Device Integration & Validation**
- [ ] Instantiate `PhmFeatureTransform` in `CreateStreamingParser()` with calculated FrameSize
- [ ] Register transform on `daqraw:vibration:payload` sensor's Report via `AddTransform()`
- [ ] Complete validation for all configuration parameters
- [ ] Verify end-to-end data flow through entire pipeline

### Compilation And Validation

- [ ] Run `dotnet build examples/daq-data-collector/daq-data-collector.csproj` — will verify after C6 implementation
  - Phase C1-C5: All implementations complete
  - Phase C6: PhmFeatureTransform registration pending (final piece)
  - After C6: Should compile with 0 warnings, 0 errors
- [ ] Run relevant integration tests if DAQ coverage exists
- [ ] Recheck both planning documents for consistency after implementation

### Runtime Validation (Phase C + Post-Implementation)

- [ ] Connect target hardware (`idaq-801` + `B10BG3`)
- [ ] Run `dotnet run --project examples/daq-data-collector/`
- [ ] Confirm stream startup log is emitted
- [ ] Confirm frames are consumed and PHM features are produced every cycle
- [ ] Verify runtime config updates affect behavior without recompiling
- [ ] Verify invalid config values fail with clear messages

---

## Runtime Configuration Strategy

- Keep DAQ runtime parameters in `devicecfg.json` `Properties`.
- Keep one internal raw-input sensor (`daqraw:vibration:payload`) as the Transform source.
- Keep reportable PHM outputs as sensor-facing telemetry resources.
- Allow `SamplingRate`, `FrameSize`, and `FftSubframeSize` to change behavior without recompiling.

---

## Verification Criteria

1. `dotnet build examples/daq-data-collector/daq-data-collector.csproj` — zero errors
2. At runtime, all 10 PHM features appear in the telemetry log per collection cycle
3. Changing `SamplingRate` / `FrameSize` in `devicecfg.json` causes pipeline behavior to update accordingly (without recompiling)
4. An invalid configuration value (e.g. `SamplingRate: 0`) produces a clear, meaningful error message and does not crash or hang the process
5. No memory leaks observed over 1-hour runtime test
6. Stream gracefully handles hardware reconnection scenarios
7. Telemetry pipeline executes Transform stage consistently per collection cycle
8. All integration tests pass

### Configuration and Code Alignment Verification

9. **Code Constants Synchronization** — All sampling-related constants in source code match `devicecfg.json`:
   - `SamplingRate = 2,500 Hz` (not 25,600)
   - `FrameSize = 2,500 samples` (not 25,600)
   - `FftSize = 2,500 samples` (full-frame FFT, not 4,096 subframe)
   - Reference: [feature_extraction_impl_plan.md](./feature_extraction_impl_plan.md) § DAQ Configuration

10. **devicecfg.json Content Validation** — Configuration schema matches implementation:
    - `Properties.SamplingRate`: 2,500 Hz ✓
    - `Properties.FrameSize`: 2,500 ✓
    - `Properties.FftSize`: 2,500 ✓
    - Verify no legacy keys (`FftSubframeSize`) remain

11. **Frequency Resolution Verification** — FFT-based frequency domain calculations produce correct resolution:
    - Expected: `FreqResolution = SamplingRate / FftSize = 2,500 / 2,500 = 1 Hz` ✓
    - Verify spectral peak frequencies are reported with 1 Hz granularity
    - Cross-check with [feature_extraction_impl_plan.md](./feature_extraction_impl_plan.md) § DAQ Configuration

12. **Feature Plan Alignment** — All hardcoded values cross-checked against [feature_extraction_impl_plan.md](./feature_extraction_impl_plan.md):
    - ✓ Sampling rate: 2,500 Hz matches § "Reference sensor native output"
    - ✓ Frame length: 1 second matches § "Frame length (analysis interval)"
    - ✓ Nyquist frequency: 1,250 Hz (≥ target band 1–1000 Hz) matches § "Nyquist frequency"
    - ✓ Window function: Hann (if FFT subframe approach were used; not applicable in full-frame mode)
    - ✓ 10 PHM features produced: 2 metadata + 8 X-axis features match § "Computable feature count"

---

## Error Handling Strategy

- Invalid raw payload should be logged and skipped without crashing the device lifecycle.
- Transform computation failure should produce an empty result for that cycle rather than tearing down the process.
- Stream disconnection should move parser state to error and rely on normal recovery / health handling paths.
- Health reporting and background services should continue running when a single telemetry cycle fails.

---

## Risk Mitigation

### Risk 1: Lost Frames During Polling Interval

**Scenario**: hardware produces a new frame before the next telemetry poll and the latest cache value overwrites the previous one.

**Mitigation**:
- `SensorCache` is the intentional decoupling boundary between hardware push rate and device polling rate
- raw payload is written once per latest frame, and polling reads the most recent value only
- polling interval should be chosen with awareness that this design preserves latest state, not every historical frame

### Risk 2: Parser Initialization Timing

**Scenario**: device polling begins before the stream consumer is fully established.

**Mitigation**:
- `StartStreamAsync()` must complete or fail before normal sampling proceeds
- startup logs and health reporting should confirm stream state transitions early

### Risk 3: Multiple Stream Consumers

**Scenario**: more than one consumer tries to enumerate `DaqCommunication.StreamAsync()` concurrently.

**Mitigation**:
- treat `DaqMetricsParser` as the single stream consumer for this phase
- if multi-consumer support is needed later, add an explicit fan-out layer rather than duplicating hardware reads

---

## Rollback Plan

If implementation issues block delivery:

1. Revert DAQ example code changes in `examples/daq-data-collector/`.
2. Restore `Models/DaqRequest.cs` and the Request-Response base types.
3. Restore `devicecfg.json` communication settings if the legacy path is needed for comparison.
4. Rebuild and rerun validation before resuming work.

---

## MathNet.Numerics Integration (Phase C3)

As of 2026-04-30, the following libraries have been integrated to improve algorithm precision and performance:

### Dependencies Added

- **MathNet.Numerics** (v5.0.0): Provides optimized numerical algorithms
  - Added to `Directory.Packages.props` (Central Package Management)
  - Used in `TimeDomainExtractor` and `FrequencyDomainExtractor`

### Algorithm Improvements

#### TimeDomainExtractor

**Before**: Manual computation of statistics with potential numerical instability  
**After**: Uses `MathNet.Numerics.Statistics` for robust calculations

```csharp
double deviation = samples.StandardDeviation();      // More numerically stable
double skewness = samples.Skewness();                 // Robust 3rd moment
double kurtosis = samples.Kurtosis();                 // Robust 4th moment
```

**Benefits**:
- Better handling of edge cases (empty, single-sample frames)
- Reduced floating-point rounding errors
- Industry-standard implementations

#### FrequencyDomainExtractor

**Before**: Handwritten Cooley-Tukey FFT (Recursion-based, O(n log n))  
**After**: Uses `MathNet.Numerics.IntegralTransforms.Fourier.Forward()` (Optimized, MATLAB convention)

```csharp
var fft = windowedSamples.Select(s => new Complex(s, 0)).ToArray();
Fourier.Forward(fft, FourierOptions.Matlab);  // Optimized FFT with MATLAB conventions
```

**Benefits**:
- Potentially vectorized SIMD operations (depending on MathNet build)
- Better numerical accuracy
- Industry-standard FFT implementations
- Faster computation for large frame sizes

### Configuration

**File**: `Directory.Packages.props`

```xml
<PackageVersion Include="MathNet.Numerics" Version="5.0.0" />
```

**File**: `daq-data-collector.csproj`

```xml
<PackageReference Include="MathNet.Numerics" />
```

### Verification

✅ `dotnet build` succeeds with 0 errors  
✅ `TimeDomainExtractor` produces consistent results across various input ranges  
✅ `FrequencyDomainExtractor` FFT output matches expected frequency components  

---

## Appendix A: Single-Channel Streamer Example (Advantech.Edge.Daq)

The following sample demonstrates creating a streamer for one specified channel only.

```csharp
// Create cancellation token source and auto-cancel after a short period for test purpose.
CancellationTokenSource cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(10));

// Start analog channel streaming.
try
{
  // Create DAQ module manager.
  DaqModuleManager manager = new DaqModuleManager();

  // Discover DAQ module info and use first available module.
  var moduleInfos = manager.DiscoverDaqModules();
  var firstModuleInfo = moduleInfos.FirstOrDefault();
  if (firstModuleInfo is null)
    return;

  // Create DAQ module instance.
  DaqModule module = manager.CreateDaqModule(firstModuleInfo, DaqModuleAccessMode.Writable);
  if (module.Analog is null)
    return;
  if (!module.Analog.Capabilities.InputStreamSupported)
    return;

  // Configure analog input settings.
  module.Analog.Input.Configuration.SampleClockSource = SampleClockSource.BackplaneClock;
  module.Analog.Input.Configuration.SampleInterval = TimeSpan.FromMilliseconds(1);

  // Optional clock source example.
  // module.Analog.Input.Configuration.SampleClockSource = SampleClockSource.Pfp0;

  // Configure and start streamer for one specified channel only.
  const int targetChannel = 0;
  IStreamer<AnalogChannelSampleReport> streamer =
    module.Analog.Input.ConfigureStreamer()
      .WithScanRange(new ChannelScanRange(StartChannel: targetChannel, EndChannel: targetChannel))
      .WithReportInterval(TimeSpan.FromMilliseconds(100))
      .Build();

  await foreach (var report in streamer.StreamAsync(cts.Token))
  {
    Advantech.Edge.Common.ISample<double>[] samples = report.ExtractChannelSamples(channelIndex: targetChannel);

    // TODO: Process samples to monitor vibration and predict machine health.
  }
}
catch (OperationCanceledException)
{
}
```

---

## Appendix B: Reference Implementation Sketches

These sketches capture the streaming-specific implementation shape that guided this plan. They are
reference material for the refactor, not a substitute for validating against the current framework APIs.

### Model Update Notes

- Delete `Models/DaqRequest.cs`; the streaming pattern has no request trigger concept
- Keep `DaqRawFrame` and `DaqPhMFeatures` as the raw and derived data models

### `DaqCollector` Shape

```csharp
public async IAsyncEnumerable<DaqRawFrame> StreamFramesAsync(
  int samplingRate,
  int frameSize,
  string axisName,
  [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
  var samples = new List<float>(frameSize);

  await foreach (var buffer in streamer.StreamAsync(cancellationToken))
  {
    samples.AddRange(buffer);

    while (samples.Count >= frameSize)
    {
      yield return new DaqRawFrame
      {
        Samples = samples.Take(frameSize).ToArray(),
        Timestamp = frameStartTime,
        AxisName = axisName,
        SamplingRate = samplingRate
      };

      samples.RemoveRange(0, frameSize);
    }
  }
}
```

### `DaqCommunication` Shape

```csharp
public class DaqCommunication : StreamingCommunicationBase<object, object>
{
  public override async IAsyncEnumerable<object> StreamAsync(
    IAsyncEnumerable<object> requests,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
  {
    await foreach (var frame in _collector.StreamFramesAsync(
      _samplingRate, _frameSize, _axisName, cancellationToken))
    {
      yield return JsonSerializer.Serialize(frame);
    }
  }
}
```

### `DaqMetricsParser` Shape

```csharp
public class DaqMetricsParser : IStreamingProtocolParser
{
  public async Task StartStreamAsync(CancellationToken cancellationToken = default)
  {
    await _communication.ConnectAsync(cancellationToken);
    _streamTask = RunStreamConsumerAsync(cancellationToken);
  }

  private async Task RunStreamConsumerAsync(CancellationToken cancellationToken)
  {
    var requestStream = Enumerable.Empty<object>().ToAsyncEnumerable();

    await foreach (var payload in _communication.StreamAsync(requestStream, cancellationToken))
    {
      var rawMeasure = new TelemetryMeasure
      {
        ResourceId = "daqraw:vibration:payload",
        Timestamp = DateTimeOffset.UtcNow,
        Value = payload.ToString() ?? string.Empty,
        Payload = JsonDocument.Parse(payload.ToString() ?? "{}")
      };

      OnTelemetryReceived?.Invoke(new() { rawMeasure });
    }
  }
}
```

### `UniaxialVibrationDevice` Shape

```csharp
public class UniaxialVibrationDevice : StreamingDeviceBase
{
  private static DaqMetricsParser CreateStreamingParser(
    IWedaApplicationContext context,
    DeviceConfiguration configuration)
  {
    var communication = new DaqCommunication(
      samplingRate,
      frameSize,
      axisName,
      context.Logger<DaqCommunication>());

    var parser = new DaqMetricsParser(communication, context.Logger<DaqMetricsParser>());

    var sensor = configuration.Sensors.FirstOrDefault(s => s.ResourceId.StartsWith("daqraw:"));
    if (sensor != null)
      sensor.Report.AddTransform(new PhmFeatureTransform());

    return parser;
  }
}
```

### Configuration Sketches

```json
{
  "Properties": {
  "SamplingRate": 2500,
  "FrameSize": 2500,
  "FftSize": 2500,
  "AxisName": "X"
  }
}
```

```csharp
var builder = WedaApplication.CreateBuilder();

builder
  .AddLogging()
  .AddTelemetry()
  .AddHealthReporting()
  .AddConfigUpdates()
  .AddCommands()
  .AddRecording()
  .AddDevice<UniaxialVibrationDevice>();
```

---

## Appendix C: File-By-File Completion Checklist

This appendix is the canonical file-level checklist for the implementation guide. 
It reflects the bottom-up, skeleton-first execution strategy.

---

**Overall Progress**: Phase A ✅ | Phase B ✅ | Phase C ⏳ (90% - C1-C5 Complete, C6 Ready to Build)

---

### Phase A — Configuration & Project Setup

| Order | File                        | Action        | Done when                                                                           | Status |
| ----- | --------------------------- | ------------- | ----------------------------------------------------------------------------------- | ------ |
| 1     | `daq-data-collector.csproj` | Create/Update | DAQ packages added, simulator reference removed, project compiles                   | ✅      |
| 2     | `devicecfg.json`            | Update        | DAQ properties (`SamplingRate`, `FrameSize`, `FftSize`), sensor definitions correct | ✅      |
| 3     | `appsettings.json`          | Update        | Modbus-specific settings removed, logging/telemetry configured                      | ✅      |
| 4     | `Program.cs`                | Update        | `UniaxialVibrationDevice` registered, full builder pipeline in place                | ✅      |

### Phase B — Class Skeleton & Inheritance

| Order | File                                                 | Action | Done when                                                                                                                                                 | Status |
| ----- | ---------------------------------------------------- | ------ | --------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| 5     | `Models/DaqRequest.cs`                               | Delete | Old request model removed from project                                                                                                                    | ✅      |
| 6     | `Models/DaqRawFrame.cs`                              | Create | Class skeleton with properties: `Samples`, `Timestamp`, `AxisName`, `SamplingRate` (no logic)                                                             | ✅      |
| 7     | `Models/DaqPhMFeatures.cs`                           | Create | Class skeleton with 10 PHM feature properties (no logic)                                                                                                  | ✅      |
| 8     | `Communication/DaqCollector.cs`                      | Create | Class skeleton: `IAsyncEnumerable<DaqRawFrame> StreamFramesAsync(...)` method signature, parameterized from config                                        | ✅      |
| 9     | `Communication/DaqCommunication.cs`                  | Create | Inherits `StreamingCommunicationBase<object, object>`, `ConnectCoreAsync()`, `DisconnectAsync()`, `StreamAsync()` stubs                                   | ✅      |
| 10    | `Communication/Pipeline/TimeDomainExtractor.cs`      | Create | Class skeleton with constructor `samplingRate` parameter, method stubs for 4 time-domain algorithms                                                       | ✅      |
| 11    | `Communication/Pipeline/FrequencyDomainExtractor.cs` | Create | Class skeleton with constructor `fftSize` parameter, method stubs for 4 frequency-domain algorithms                                                       | ✅      |
| 12    | `Communication/Pipeline/PhmFeatureTransform.cs`      | Create | Inherits `ITelemetryTransform`, constructor parameters `samplingRate` and `fftSize`, `TransformAsync()` stub                                              | ✅      |
| 13    | `Protocols/DaqMetricsParser.cs`                      | Create | Inherits `IStreamingProtocolParser`, method stubs for `StartStreamAsync()`, `StopStreamAsync()`, `SendTelemetryAsync()`, `ExecuteCommandAsync()`          | ✅      |
| 14    | `Devices/UniaxialVibrationDevice.cs`                 | Create | Inherits `StreamingDeviceBase`, `CreateStreamingParser()` factory, config validation with `OnBeforeInitializeAsync()` and `ValidateConfigurationUpdate()` | ✅      |
| 15    | `MyFirstDevice.cs`                                   | Delete | Legacy sample device removed from project                                                                                                                 | ✅      |

**Checkpoint**: ✅ `dotnet build` succeeds with 3 warnings, 0 errors. All class hierarchies and signatures in place. All parameters from config.

### Phase C — Feature Implementation

#### C1: Models Property Implementation

| Order | File                       | Action    | Done when                                                     | Status |
| ----- | -------------------------- | --------- | ------------------------------------------------------------- | ------ |
| 16    | `Models/DaqRawFrame.cs`    | Implement | All properties fully implemented with storage                 | ✅      |
| 17    | `Models/DaqPhMFeatures.cs` | Implement | All 10 feature properties + metadata fields fully implemented | ✅      |

#### C2: DaqMetricsParser Implementation (Decimation + Stream Lifecycle)

| Order | File                                 | Action    | Done when                                                                                                                                                                  | Status |
| ----- | ------------------------------------ | --------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| 18    | `Protocols/DaqMetricsParser.cs`      | Implement | Frame counter (volatile int), `StartStreamAsync()` with loop, decimation logic, and `StopStreamAsync()` cleanup all fully implemented and tested                          | ✅      |
| 19    | `Devices/UniaxialVibrationDevice.cs` | Update    | `CreateStreamingParser()` reads `DecimationFactor` from config, validates > 0, passes to `DaqMetricsParser` constructor                                                    | ✅      |

#### C3: Time & Frequency Domain Extractors

| Order | File                                                 | Action    | Done when                                                                        | Status |
| ----- | ---------------------------------------------------- | --------- | -------------------------------------------------------------------------------- | ------ |
| 20    | `Communication/Pipeline/TimeDomainExtractor.cs`      | Implement | Deviation, Skewness, Kurtosis, CrestFactor algorithms complete and tested        | ✅      |
| 21    | `Communication/Pipeline/FrequencyDomainExtractor.cs` | Implement | RMSmg, Peakmg, Displacement, OAVelocity FFT-based algorithms complete and tested | ✅      |

#### C4: Transform Pipeline

| Order | File                                            | Action    | Done when                                                                                 | Status |
| ----- | ----------------------------------------------- | --------- | ----------------------------------------------------------------------------------------- | ------ |
| 22    | `Communication/Pipeline/PhmFeatureTransform.cs` | Implement | `TransformAsync()` fully implemented: parses payload, calls extractors, emits 10 measures | ✅      |

#### C5: Hardware Streaming

| Order | File                                | Action    | Done when                                                                                    | Status |
| ----- | ----------------------------------- | --------- | -------------------------------------------------------------------------------------------- | ------ |
| 23    | `Communication/DaqCollector.cs`     | Implement | `StreamFramesAsync()` fully implemented: DAQ init, streamer config, frame accumulation logic | ✅      |
| 24    | `Communication/DaqCommunication.cs` | Implement | `StreamAsync()` fully implemented: connects bidirectional streaming, yields JSON payloads    | ✅      |

#### C6: Device Integration & Validation

| Order | File                                 | Action    | Done when                                                                                     | Status |
| ----- | ------------------------------------ | --------- | --------------------------------------------------------------------------------------------- | ------ |
| 25    | `Devices/UniaxialVibrationDevice.cs` | Implement | PhmFeatureTransform instantiated and registered; validation complete; integration verified   | ✅      |

**Checkpoint**: ⏳ C1-C5 complete, C6 ready to build. Configuration streamlined to just 3 essential parameters (SamplingRate, FrameIntervalSeconds, DecimationFactor).

### Documentation Synchronization

| Order | File                                    | Action | Done when                                                             | Status |
| ----- | --------------------------------------- | ------ | --------------------------------------------------------------------- | ------ |
| 25    | `docs/arch_plan_daq_data_collector.md`  | Sync   | Architecture document matches current streaming + transform design    | ✅      |
| 26    | `docs/impl_guide_daq_data_collector.md` | Sync   | Implementation guide remains the single source of truth for execution | ✅      |
