# Architecture Plan: DAQ Data Collector Refactoring

## Overview

Refactor `daq-data-collector` to mirror the layered architecture of `system-agent`. Adopt the
framework Transform Pipeline as the primary feature extraction path, decoupling PHM computation
from communication transport. Adopt **Streaming communication pattern** to align with the
Asynchronous nature of `Advantech.Edge.Daq`.

`DaqCommunication` exposes a bidirectional streaming interface. `DaqCollector` continuously
pushes hardware-acquired `DaqRawFrame` objects into the stream. `DaqMetricsParser` consumes the
stream, buffers raw data in `SensorCache`, and provides telemetry on-demand during polling intervals.
`Transform` stage then expands raw telemetry into PHM feature measures. Introduce
`UniaxialVibrationDevice` inheriting `StreamingDeviceBase` directly. Replace `MyFirstDevice` and
remove `TcpModbusSimulator`.

Raw data collection uses **`Advantech.Edge.Daq`** (not Modbus, no connection settings):

- `DaqModuleManager` → `CreateDaqModule()` → `DaqModule.Analog.Channels` (collection)
- Target channel only (configured index, e.g. channel 0):
  `module.Analog.Input.ConfigureStreamer()` → `WithScanRange(StartChannel: N, EndChannel: N)`
  → `IStreamerBuilder` → `.Build()` → `IStreamer`
- `IStreamer.StreamAsync()` yields buffered samples continuously as `IAsyncEnumerable<float[]>`
  when hardware buffers are full
- `DaqCommunication.StreamAsync()` wraps `IStreamer` into framework's Streaming protocol interface
- `DaqCollector` runs background producer task: accumulates raw samples → complete frame →
  yields into stream

---

## Migration Context

### Previous Architecture (Request-Response Pattern)

- `UniaxialVibrationDevice` extended `RequestResponseDeviceBase`
- `DaqCommunication` extended `RequestResponseCommunicationBase<DaqRequest, object>`
- `DaqMetricsParser` implemented `IRequestResponseProtocolParser`
- Device polling triggered `ReadTelemetryAsync()` which issued `DaqRequest` and blocked until one frame was available

The previous design wrapped push-based hardware in request-response semantics. That introduced an
empty trigger model (`DaqRequest`) and coupled telemetry polling with frame acquisition.

### Previous Request-Response Flow

```
UniaxialVibrationDevice
  │ (extends RequestResponseDeviceBase)
  │
  ├─ OnInitializeAsync()
  │   └─ starts polling tasks via StartBackgroundTasksAsync()
  │
  ├─ At each sensor interval:
  │   └─ Device.ReadTelemetryAsync()
  │       └─ parser.ReadTelemetryAsync()
  │           └─ DaqCommunication.RequestAsync(new DaqRequest())
  │               └─ RequestAsyncCore()
  │                   ├─ await Channel.ReadAsync()
  │                   ├─ serialize → raw payload
  │                   └─ return to parser
  │               └─ parser emits raw telemetry measure
  │                   └─ TelemetryPipeline transform
  │                       └─ enqueue 10 PHM measures
```

---

## Architecture Diagrams

```
+--------------------------------------------------------------------------------+
|                         Device Layered Architecture                            |
+--------------------------------------------------------------------------------+
|                                                                                |
|  +---------------------------------------------------------------------------+ |
|  |  UniaxialVibrationDevice                                                  | |
|  |  (Device - Creates DaqCommunication + DaqMetricsParser; handles config    | |
|  |   validation and OnDataReceived)                                          | |
|  +---------------------------------------------------------------------------+ |
|                                    | extends                                   |
|                                    v                                           |
|  +---------------------------------------------------------------------------+ |
|  |  StreamingDeviceBase (SubNode.Core Framework)                             | |
|  |  (Framework Base - Manages stream lifecycle and interval-based sampling)  | |
|  +---------------------------------------------------------------------------+ |
|                                    | extends                                   |
|                                    v                                           |
|  +---------------------------------------------------------------------------+ |
|  |  DeviceBase (SubNode.Core Framework)                                      | |
|  |  (Core Base - Lifecycle and configuration management)                     | |
|  +---------------------------------------------------------------------------+ |
+--------------------------------------------------------------------------------+


+--------------------------------------------------------------------------------+
|          Communication & Processing Layered Architecture                       |
+--------------------------------------------------------------------------------+
|                                                                                |
|  +---------------------------------------------------------------------------+ |
|  |  DaqCommunication                                                         | |
|  |  (Communication - Exposes Streaming interface for hardware frame stream)  | |
|  +---------------------------------------------------------------------------+ |
|                                    | uses                                      |
|                                    v                                           |
|  +---------------------------------------------------------------------------+ |
|  |  DaqCollector                                                             | |
|  |  (Collector - Continuously streams Advantech.Edge.Daq frames)             | |
|  +---------------------------------------------------------------------------+ |
|                                    | pushes frames into                        |
|                                    v                                           |
|  +---------------------------------------------------------------------------+ |
|  |  DaqMetricsParser + SensorCache (Streaming Protocol Parser)               | |
|  |  (Buffer - Holds latest frame data; sampled by Device at polling interval)│ |
|  +---------------------------------------------------------------------------+ |
|                                    | sampled at interval, then                 |
|                                    v                                           |
|  +---------------------------------------------------------------------------+ |
|  |  TelemetryPipeline (Framework)                                            | |
|  |  ┌─────────────────────────────────────────────────────────────────────┐  | │
|  |  │ Transform Stage: PhmFeatureTransform                                │  │ |
|  |  │ - Parses raw payload → DaqRawFrame                                  │  │ |
|  |  │ - Calls extractors (TimeDomain, FrequencyDomain)                    │  │ |
|  |  │ - Emits 10 PHM feature measures                                     │  │ |
|  |  └─────────────────────────────────────────────────────────────────────┘  │ |
|  |  ┌─────────────────────────────────────────────────────────────────────┐  │ |
|  |  │ DSP Stage: (Unused in this phase)                                   │  │ |
|  |  └─────────────────────────────────────────────────────────────────────┘  │ |
|  +---------------------------------------------------------------------------+ |
|                                                                                |
+--------------------------------------------------------------------------------+
```

---

## Implementation Guide

Directory structure, data flow, implementation phases, PHM feature catalog, verification criteria,
and code examples are documented in
**[impl_guide_daq_data_collector.md](./impl_guide_daq_data_collector.md)**.

---

## Layer Mapping: system-agent vs. daq-data-collector

| Layer                          | system-agent                                       | daq-data-collector                                                                                                         |
| ------------------------------ | -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Device                         | `LocalSystemAgentDevice` + `SystemAgentDeviceBase` | `UniaxialVibrationDevice`                                                                                                  |
| Device Base                    | `RequestResponseDeviceBase`                        | `StreamingDeviceBase`                                                                                                      |
| Communication                  | `LocalSystemCommunication`                         | `DaqCommunication`                                                                                                         |
| Communication Base             | `RequestResponseCommunicationBase`                 | `StreamingCommunicationBase`                                                                                               |
| Collector                      | `LocalSystemResourceCollector`                     | `DaqCollector`                                                                                                             |
| Buffering                      | *(none)*                                           | `SensorCache` (via StreamingProtocolParser)                                                                                |
| Transform (Feature Extraction) | *(none)*                                           | `PhmFeatureTransform` (Transform stage)                                                                                    |
| Protocol Parser                | `SystemMetricsParser`                              | `DaqMetricsParser`                                                                                                         |
| Protocol Parser Base           | `IRequestResponseProtocolParser`                   | `IStreamingProtocolParser`                                                                                                 |
| Raw Data Model                 | `SystemMetricsRawData`                             | `DaqRawFrame` (internal to Transform)                                                                                      |
| Output Model                   | *(inline in parser)*                               | `TelemetryMeasure` (10 PHM features)                                                                                       |
| DAQ Configuration              | *(not applicable)*                                 | Driven by `devicecfg.json` `Properties`: `SamplingRate` (2,500 Hz), `FrameSize` (2,500 samples), `FftSize` (2,500 samples) |

---

## Design Decisions

| Decision                    | Choice                                                                        | Rationale                                                                                                                                           |
| --------------------------- | ----------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| Communication pattern       | Streaming (not Request-Response)                                              | `Advantech.Edge.Daq` is push-based async API; hardware continuously produces frames. Streaming pattern directly aligns with hardware semantics.     |
| Device base class           | `StreamingDeviceBase` (not `RequestResponseDeviceBase`)                       | Handles async stream lifecycle, provides `SensorCache` for buffering, interval-based sampling. Eliminates artificial request-response wrapper.      |
| Raw data collection         | `Advantech.Edge.Daq` streaming (not Modbus)                                   | Hardware interface for idaq-801/934                                                                                                                 |
| Connection settings         | None (local hardware driver)                                                  | `Advantech.Edge.Daq` accesses hardware directly                                                                                                     |
| Streaming model             | `DaqCollector` yields frames; `DaqCommunication.StreamAsync()` exposes        | Direct async/await semantics; no buffering layer in Communication; parser consumes stream and manages `SensorCache`                                 |
| Buffering strategy          | `SensorCache` in streaming protocol parser                                    | Decouples hardware push rate from device polling intervals; enables different sensor intervals to sample from latest cached frame                   |
| Feature extraction location | Framework Transform stage (`TelemetryPipeline.TransformAndFilterAsync`)       | Decouples domain logic from communication transport; aligns with Transform-first pipeline architecture                                              |
| DAQ config location         | `devicecfg.json` `Properties` block                                           | Co-located with existing device parameters (e.g. `SlaveId`)                                                                                         |
| Telemetry mapping           | One raw payload sensor feeds Transform; reportable PHM metrics map to Sensors | Keeps raw acquisition separate from feature reporting while preserving sensor-level interval and enable/disable control                             |
| Concrete device name        | `UniaxialVibrationDevice`                                                     | Reflects single-axis measurement nature (B10BG3 single-axis accelerometer)                                                                          |
| Frame decimation            | Decimation factor configured in `devicecfg.json` Properties                   | Decouples hardware push rate (2 Hz) from device polling interval (1 Hz); reduces telemetry pipeline load; frame counter logic in `DaqMetricsParser` |

---

## Non-Goals

- No generic raw-frame pipeline abstraction is introduced in framework core in this phase.
- No change is made to cloud-facing feature schema or sensor-level contracts.
- No DSP-first redesign is introduced for PHM feature extraction.

---

## Implementation Boundaries

- `DaqCollector` is responsible for hardware-backed frame production only.
- `DaqCommunication` is responsible for exposing the hardware stream through the framework communication contract.
- `DaqMetricsParser` owns stream lifecycle consumption and cache writes.
- `UniaxialVibrationDevice` owns device lifecycle, configuration validation, and interval-based sampling.
- `PhmFeatureTransform` owns feature expansion and one-to-many telemetry fan-out.
- `TimeDomainExtractor` and `FrequencyDomainExtractor` remain reusable computation helpers under the Transform stage.

---

## Behavioral Differences

| Aspect                           | Request-Response                                 | Streaming                                             |
| -------------------------------- | ------------------------------------------------ | ----------------------------------------------------- |
| Stream lifecycle                 | Implicit (Device manages via polling loop)       | Explicit (`StartStreamAsync()` / `StopStreamAsync()`) |
| Frame buffering                  | `Channel<DaqRawFrame>` (unbounded)               | `SensorCache` (last-write-wins per sensor)            |
| Parser model                     | Passive (called by Device.ReadTelemetry)         | Active (owns stream loop, fires events)               |
| `DaqRequest`                     | Required (empty trigger record)                  | Not needed                                            |
| `ReadTelemetryAsync()` semantics | Blocks until frame ready; calls `RequestAsync()` | Non-blocking; reads latest from cache                 |
| Hardware push vs device poll     | Wrapped; artificial coupling                     | Direct alignment                                      |
| Concurrency                      | Producer/consumer timing coupled                 | Producer and polling rate are decoupled by cache      |
| Latency                          | Immediate request response                       | Bounded by polling interval                           |

---

## Buffering And Latency Characteristics

- Hardware push rate is significantly higher than telemetry polling cadence.
- `SensorCache` is the intentional decoupling boundary and stores the latest value per sensor.
- End-to-end latency is bounded by the configured polling interval rather than hardware sample cadence.
- This design optimizes for latest-state reporting, not historical frame preservation.

## Decimation Factor and Frame Rate Matching

- **Hardware sampling rate**: 5,000 Hz (Advantech.Edge.Daq SamplingRate configured in `devicecfg.json`)
- **Hardware report interval**: 0.5 s → 2,500 samples per frame → **frame push rate: 2 Hz**
- **DecimationFactor**: 2 (configured in `devicecfg.json` Properties)
- **Effective device sample rate**: 1 Hz (hardware 2 Hz ÷ decimation factor 2)
- **Device sensor interval**: 1 s (aligns with effective sample rate)

### Decimation Implementation

`DaqMetricsParser` implements frame decimation via a thread-safe frame counter:

```
Frame Stream from Hardware (2 Hz):
  T=0.0s  → Frame 1  → [counter=1, 1 % 2 ≠ 0] → discard
  T=0.5s  → Frame 2  → [counter=2, 2 % 2 = 0] → PROCESS → emit OnTelemetryReceived()
  T=1.0s  → Frame 3  → [counter=3, 3 % 2 ≠ 0] → discard
  T=1.5s  → Frame 4  → [counter=4, 4 % 2 = 0] → PROCESS → emit OnTelemetryReceived()
```

Only frames where `frameCounter % DecimationFactor == 0` trigger `OnTelemetryReceived()`, resulting in 1 Hz effective update rate to `SensorCache`.

---

## Reference Files

| File                                                                       | Purpose                                                                                                                                  |
| -------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `examples/system-agent/LocalSystemAgentDevice.cs`                          | Primary pattern for `UniaxialVibrationDevice`: constructor-level assembly of communication + parser, config validation, `OnDataReceived` |
| `examples/system-agent/Devices/SystemAgentDeviceBase.cs`                   | Reference for constructor-level assembly of communication + parser; `UniaxialVibrationDevice` inlines this logic directly                |
| `examples/system-agent/Communication/LocalSystemCommunication.cs`          | Communication (no-connection variant)                                                                                                    |
| `examples/system-agent/Communication/LocalSystemResourceCollector.cs`      | Collector pattern                                                                                                                        |
| `examples/system-agent/Protocols/SystemMetricsParser.cs`                   | Protocol parser pattern (Request-Response variant for reference)                                                                         |
| `examples/daq-data-collector/docs/impl_plan_feature_extraction.md`         | Feature catalog & algorithm specs                                                                                                        |
| `src/Weda.SubNode.Core/Communication/Common/StreamingCommunicationBase.cs` | Base communication class for streaming pattern                                                                                           |
| `src/Weda.SubNode.Core/Devices/StreamingDeviceBase.cs`                     | Base device class for streaming pattern; includes `SensorCache` and interval sampling                                                    |

---

## Decision Log: Adopt Transform Pipeline for PHM Feature Extraction

### Decision

Adopt the framework Transform Pipeline as the primary PHM feature extraction path for DAQ data in this refactoring phase.

Feature extraction is no longer owned by `RawDataPipeline` inside `DaqCommunication.StreamAsync()`.
`DaqCommunication` remains acquisition-focused, `DaqMetricsParser` consumes the stream and emits raw telemetry input to `SensorCache`, and Transform stage expands raw payload into PHM feature measures.

### Why This Decision

1. **Better alignment with framework pipeline lifecycle**
  - Transform stage is the intended pre-DSP processing layer and already participates in telemetry flow, validation boundaries, and pipeline events.

2. **Stable telemetry contract to cloud**
  - The 10 PHM feature sensors remain the final output model.
  - Existing sensor-facing and cloud-facing contracts are preserved.

3. **Cleaner separation of concerns**
  - `DaqCommunication` handles transport and frame acquisition.
  - Transform handles feature extraction and one-to-many fan-out.
  - Parser focuses on protocol-to-telemetry input mapping.

4. **Lower migration risk for current scope**
  - Migration can be completed inside `daq-data-collector` example without introducing framework-core abstraction changes.
  - Runtime transform injection avoids assembly-discovery coupling in this phase.

### Scope in This Phase

- Use Transform stage for PHM extraction.
- Keep DSP stage optional and unused for this feature path.
- Limit changes to the example project (`examples/daq-data-collector`).
- Do not introduce framework-core abstraction changes in this phase.

### Alternative Considered (Rejected)

**RawDataPipeline approach**: Keep feature extraction inside communication-layer `RawDataPipeline` within legacy request-response `DaqCommunication.RequestAsyncCore()`.

This option is rejected for the following reasons:
1. **Violates separation of concerns** — mixes domain computation with communication transport.
2. **Deviates from Transform-first architecture** — framework intentionally separates feature extraction from communication.
3. **Coupling concerns** — RawDataPipeline lives in Communication package, forcing tight coupling to protocol parsing lifecycle.
4. **Limited future extensibility** — makes it harder to add DSP-stage post-processing or alternative extraction paths.

This approach is no longer considered viable for this refactoring.

### Future Enhancement Opportunity

Consider adding optional DSP-stage post-processing (smoothing, denoising) on final PHM measures only if a concrete business requirement emerges.
Alternatively, if multiple DAQ examples share similar Transform logic, extract reusable transform package for common use.

### Related Documents

 - [Implementation Guide](./impl_guide_daq_data_collector.md)
 - [Implementation Guide](./impl_guide_daq_data_collector.md#appendix-c-file-by-file-completion-checklist)
