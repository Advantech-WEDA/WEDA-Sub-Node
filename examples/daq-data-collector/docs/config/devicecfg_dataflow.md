# devicecfg.json Data Flow in DAQ Data Collector

## Overview

This document details how the `devicecfg.json` configuration file is utilized throughout the entire DAQ data collection pipeline in the UniaxialVibrationDevice. The configuration serves two main purposes:

1. **Properties Section**: Provides DAQ hardware parameters (sampling rate, frame size, FFT configuration)
2. **Sensors Section**: Defines the output telemetry schema and reporting intervals for the transformed PHM features

---

## Architecture Overview

```
devicecfg.json
    ├── Properties (DAQ Parameters)
    │   ├── SamplingRate → DaqCollector initialization
    │   ├── FrameSize → Raw frame accumulation trigger
    │   ├── FftSize → Frequency-domain feature extraction
    │   └── AxisName → Sensor axis identification
    │
    └── Sensors (Output Telemetry Schema)
        ├── daqraw:vibration:payload (internal raw input)
        └── 9 PHM feature sensors (time-domain + frequency-domain + metadata)
```

---

## Phase 1: Configuration Loading

### 1.1 Configuration File Structure

```json
{
  "SubNode": {
    "Name": "UniaxialVibrationDevice",
    "SubNodeType": "VibrationSensor",
    "Manufacturer": "Advantech",
    "Model": "iDAQ-801+B10BG3",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "UniaxialVibrationDeviceConfig": {
      "Enabled": true,
      "Properties": {
        "SamplingRate": 2500,
        "FrameSize": 2500,
        "FftSize": 2500,
        "AxisName": "X"
      },
      "Sensors": [
        {
          "Name": "daqraw_vibration_payload",
          "ResourceId": "daqraw:vibration:payload",
          "SensorGroup": "RAW",
          "Report": {
            "Enabled": true,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "string",
            "DisplayName": "Raw Vibration Payload"
          }
        },
        {
          "Name": "x_axis_rms_mg",
          "SensorGroup": "PHM",
          "Report": {
            "Enabled": true,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis RMSmg"
          }
        },
        // ... 8 additional PHM feature sensors
      ]
    }
  }
}
```

### 1.2 DeviceConfiguration Object

When the application starts, the `devicecfg.json` is loaded into a `DeviceConfiguration` object:

```csharp
public class DeviceConfiguration
{
    public bool Enabled { get; set; }
    public Dictionary<string, object> Properties { get; set; }  // SamplingRate, FrameSize, etc.
    public List<Sensor> Sensors { get; set; }                   // 10 sensor definitions
    public BackgroundTaskPeriods Periods { get; set; }
    // ... other configuration fields
}
```

---

## Phase 2: Device Initialization

### 2.1 UniaxialVibrationDevice Constructor

When `builder.AddDevice<UniaxialVibrationDevice>("UniaxialVibrationDeviceConfig")` is called in `Program.cs`, the device initializes using the configuration:

```csharp
public UniaxialVibrationDevice(
    IWedaApplicationContext context,
    DeviceConfiguration configuration)
    : base(context, configuration, CreateStreamingParser(context, configuration))
{
    EnableDataReceivedTracking = true;
    DataReceived += OnDataReceived;
}
```

### 2.2 Streaming Parser Creation

```csharp
private static DaqMetricsParser CreateStreamingParser(
    IWedaApplicationContext context,
    DeviceConfiguration configuration)
{
    var loggerFactory = context.LoggerFactory;

    // Step A: Read DAQ parameters from Properties section
    var props = configuration.Properties;
    var samplingRate = props.TryGetValue("SamplingRate", out var sr) 
        ? int.Parse(sr!.ToString()!) 
        : 2500;  // Default: 2500 Hz
    
    var frameSize = props.TryGetValue("FrameSize", out var fs) 
        ? int.Parse(fs!.ToString()!) 
        : 2500;  // Default: 2500 samples/frame
    
    var fftSize = props.TryGetValue("FftSize", out var fft) 
        ? int.Parse(fft!.ToString()!) 
        : 2500;
    
    var axisName = props.TryGetValue("AxisName", out var ax) 
        ? ax!.ToString()! 
        : "X";

    // Step B: Initialize DaqCollector (wraps Advantech.Edge.Daq)
    var collector = new DaqCollector(
        frameSize: frameSize,           // 2500 samples
        samplingRate: samplingRate,     // 2500 Hz
        axisName: axisName,             // "X"
        logger: loggerFactory.CreateLogger<DaqCollector>());

    // Step C: Wrap as DaqCommunication (StreamingCommunicationBase)
    var communication = new DaqCommunication(
        collector,
        loggerFactory.CreateLogger<DaqCommunication>());

    // Step D: Create DaqMetricsParser (IStreamingProtocolParser)
    return new DaqMetricsParser(
        communication,
        loggerFactory.CreateLogger<DaqMetricsParser>());
}
```

### 2.3 Configuration Validation

Before the device starts, `OnBeforeInitializeAsync()` validates configuration:

```csharp
protected override Task OnBeforeInitializeAsync(CancellationToken ct)
{
    _logger.LogInformation("Validating UniaxialVibrationDevice configuration...");

    var props = Configuration.Properties;

    // Validate SamplingRate > 0
    if (props.TryGetValue("SamplingRate", out var sr) &&
        int.TryParse(sr?.ToString(), out var samplingRate) &&
        samplingRate <= 0)
        throw new InvalidOperationException($"SamplingRate must be positive, got: {samplingRate}");

    // Validate FrameSize > 0
    if (props.TryGetValue("FrameSize", out var fs) &&
        int.TryParse(fs?.ToString(), out var frameSize) &&
        frameSize <= 0)
        throw new InvalidOperationException($"FrameSize must be positive, got: {frameSize}");

    // Validate FftSize > 0
    if (props.TryGetValue("FftSize", out var fft) &&
        int.TryParse(fft?.ToString(), out var fftSize) &&
        fftSize <= 0)
        throw new InvalidOperationException($"FftSize must be positive, got: {fftSize}");

    _logger.LogInformation("Configuration validation passed.");
    return base.OnBeforeInitializeAsync(ct);
}
```

---

## Phase 3: Raw Data Collection

### 3.1 Streaming Pattern

The UniaxialVibrationDevice uses the **streaming communication pattern**. When the device starts:

1. `StreamingDeviceBase.StartBackgroundTasksAsync()` calls `DaqMetricsParser.StartStreamAsync()`
2. `DaqMetricsParser` initializes the `DaqCommunication` stream
3. `DaqCommunication` connects to `DaqCollector`, which begins consuming Advantech.Edge.Daq stream

### 3.2 Raw Frame Accumulation

```
Advantech.Edge.Daq Hardware Stream
    ↓ (continuous samples at 2500 Hz)
DaqCollector.StreamFramesAsync()
    ├─ Accumulates samples into buffer
    ├─ When buffer reaches FrameSize (2500 samples = 1 second)
    └─ Yields DaqRawFrame
        {
            Samples: float[2500],
            Timestamp: 2024-04-29T10:30:45Z,
            AxisName: "X",
            SamplingRate: 2500
        }
```

### 3.3 JSON Serialization

```csharp
// DaqCommunication.StreamAsync()
public override IAsyncEnumerable<object> StreamAsync(
    IAsyncEnumerable<object> requests,
    CancellationToken cancellationToken = default)
{
    return StreamAsyncImpl(requests, cancellationToken);
}

private async IAsyncEnumerable<object> StreamAsyncImpl(
    IAsyncEnumerable<object> requests,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // For each DaqRawFrame from collector:
    await foreach (var frame in _collector.StreamFramesAsync(cancellationToken))
    {
        // Serialize to JSON
        string payload = JsonSerializer.Serialize(frame);
        
        // Yield as object (will be converted to TelemetryMeasure)
        yield return payload;
    }
}
```

---

## Phase 4: Sensor Grouping by Interval

### 4.1 Grouping Logic

`StreamingDeviceBase` inherits from `DeviceBase`, which provides `GroupSensorsByInterval()`:

```csharp
// DeviceBase.GroupSensorsByInterval()
protected List<(int IntervalMs, List<Sensor> Sensors)> GroupSensorsByInterval()
{
    return Configuration.Sensors
        .Where(s => s.IsEffectivelyEnabled)              // Check Report.Enabled
        .GroupBy(s => (int)s.Report.Interval)           // Group by Report.Interval
        .Select(g => (IntervalMs: g.Key, Sensors: g.ToList()))
        .ToList();
}
```

### 4.2 For DAQ Data Collector

Given the `devicecfg.json` configuration, all sensors have `Report.Interval: 1000`:

```csharp
// Result of GroupSensorsByInterval():
var sensorGroups = new List<(int, List<Sensor>)>
{
    (IntervalMs: 1000, Sensors: new List<Sensor>
    {
        // ResourceId: "daqraw:vibration:payload" (RAW)
        Sensor(name: "daqraw_vibration_payload", interval: 1000),
        
        // ResourceId: "x_axis_rms_mg" (PHM feature)
        Sensor(name: "x_axis_rms_mg", interval: 1000),
        
        // ... 8 additional PHM sensors, all at 1000ms
        Sensor(name: "x_axis_peak_mg", interval: 1000),
        Sensor(name: "x_axis_peak_to_peak_displacement", interval: 1000),
        Sensor(name: "x_axis_oa_velocity", interval: 1000),
        Sensor(name: "x_axis_deviation", interval: 1000),
        Sensor(name: "x_axis_skewness", interval: 1000),
        Sensor(name: "x_axis_kurtosis", interval: 1000),
        Sensor(name: "x_axis_crest_factor", interval: 1000),
        Sensor(name: "timestamp_timestamp", interval: 1000),
        Sensor(name: "device_time", interval: 1000)
    })
};
```

### 4.3 Interval Loop Creation

For each sensor group, `StreamingDeviceBase.StartBackgroundTasksAsync()` creates an interval loop:

```csharp
// PubSubDeviceBase.StartBackgroundTasksAsync()
// (StreamingDeviceBase has similar pattern)

foreach (var (intervalMs, sensors) in sensorGroups)
{
    // Create a periodic sampling task
    var samplingTask = RunIntervalLoopAsync(
        sensors,
        intervalMs: 1000,           // From Report.Interval
        initialDelay: true,         // Wait for stream data to arrive
        cancellationToken);
    
    _samplingTasks.Add(samplingTask);
}
```

---

## Phase 5: Periodic Sampling

### 5.1 Interval Loop Execution

The `RunIntervalLoopAsync()` method (from `DeviceBase`) executes the sampling loop:

```csharp
protected async Task RunIntervalLoopAsync(
    List<Sensor> sensors,
    int intervalMs,
    bool initialDelay,
    CancellationToken cancellationToken)
{
    // Optional initial delay to allow stream data to arrive
    if (initialDelay)
    {
        try
        {
            await Task.Delay(intervalMs, cancellationToken);
        }
        catch (OperationCanceledException) { }
    }

    // Use PeriodicTimer for precise interval timing
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));

    try
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            // For each tick, call ProcessIntervalGroupAsync
            await ProcessIntervalGroupAsync(sensors, cancellationToken);
        }
    }
    finally
    {
        await timer.DisposeAsync();
    }
}
```

### 5.2 Data Reading from SensorCache

```csharp
// StreamingDeviceBase.ReadSensorsForIntervalGroupAsync()
// (implementation detail of ProcessIntervalGroupAsync)

protected override async Task<IntervalGroupReadResult> ReadSensorsForIntervalGroupAsync(
    List<string> sensorResourceIds,  // ["daqraw:vibration:payload", "x_axis_rms_mg", ...]
    CancellationToken cancellationToken)
{
    // Read from SensorCache (where streamed data is stored)
    var measures = _sensorCache.Read(sensorResourceIds);
    
    if (measures.Count > 0)
    {
        _logger.LogDebug("Sampled {Count} telemetry measures from cache", measures.Count);
        // Raise DataReceived event (RAW data before transform/filter)
        RaiseDataReceived(measures);
    }

    return Task.FromResult(measures);
}
```

---

## Phase 6: Telemetry Transform Pipeline

### 6.1 PhmFeatureTransform

After sampling, the telemetry pipeline executes transforms. `PhmFeatureTransform` is the key transform:

```csharp
// PhmFeatureTransform.TransformAsync()
public class PhmFeatureTransform : ITelemetryTransform
{
    private readonly TimeDomainExtractor _timeDomain;
    private readonly FrequencyDomainExtractor _frequencyDomain;

    public async Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        var outputMeasures = new List<TelemetryMeasure>();

        // Find the raw payload sensor
        var rawPayloadMeasure = measures.FirstOrDefault(m => 
            m.ResourceId == "daqraw:vibration:payload");

        if (rawPayloadMeasure == null)
            return outputMeasures;  // No raw data to process

        try
        {
            // Step 1: Deserialize JSON payload → DaqRawFrame
            var rawFrame = JsonSerializer.Deserialize<DaqRawFrame>(
                rawPayloadMeasure.Payload?.ToString() ?? "");
            
            if (rawFrame == null)
                return outputMeasures;

            // Step 2: Extract time-domain features
            var timeDomain = _timeDomain.Extract(rawFrame);
            // Returns: Deviation, Skewness, Kurtosis, CrestFactor

            // Step 3: Extract frequency-domain features
            var frequencyDomain = _frequencyDomain.Extract(rawFrame);
            // Returns: RMSmg, Peakmg, PeakToPeakDisplacement, OAVelocity

            // Step 4: Create DaqPhMFeatures object
            var features = new DaqPhMFeatures
            {
                Timestamp = rawFrame.Timestamp,
                DeviceTime = DateTimeOffset.UtcNow,
                XAxisRMSmg = frequencyDomain.RMSmg,
                XAxisPeakmg = frequencyDomain.Peakmg,
                XAxisPeakToPeakDisplacement = frequencyDomain.PeakToPeakDisplacement,
                XAxisOAVelocity = frequencyDomain.OAVelocity,
                XAxisDeviation = timeDomain.Deviation,
                XAxisSkewness = timeDomain.Skewness,
                XAxisKurtosis = timeDomain.Kurtosis,
                XAxisCrestFactor = timeDomain.CrestFactor
            };

            // Step 5: Emit 10 TelemetryMeasure objects
            // (one for each sensor defined in devicecfg.json)
            outputMeasures.Add(new TelemetryMeasure
            {
                ResourceId = "x_axis_rms_mg",
                Value = features.XAxisRMSmg,
                Timestamp = rawFrame.Timestamp
            });

            outputMeasures.Add(new TelemetryMeasure
            {
                ResourceId = "x_axis_peak_mg",
                Value = features.XAxisPeakmg,
                Timestamp = rawFrame.Timestamp
            });

            // ... 8 more measures for the remaining PHM features

            return outputMeasures;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PHM features");
            return outputMeasures;
        }
    }
}
```

### 6.2 Transform Pipeline Integration

The transform is registered in the telemetry pipeline:

```csharp
// In DeviceBase, the transform pipeline processes measures:
await EnqueueTelemetryAsync(measures, cancellationToken);

// EnqueueTelemetryAsync internally:
// 1. Applies all transforms (PhmFeatureTransform is one of them)
// 2. Applies all DSP filters
// 3. Enqueues transformed measures into _telemetryBatch
// 4. Fires DataProcessed event
```

---

## Phase 7: Batch Telemetry Sending

### 7.1 Calculated Send Period

```csharp
// DeviceBase calculates the batch send period
protected int CalculatedSendTelemetryPeriod
{
    get { /* implemented */ }
}

// Calculated as:
private static int CalculateSendTelemetryPeriod(DeviceConfiguration configuration)
{
    var enabledIntervals = configuration.Sensors
        .Where(s => s.IsEffectivelyEnabled && s.Report.Interval > 0)
        .Select(s => (int)s.Report.Interval)
        .ToList();

    return enabledIntervals.Count > 0 ? enabledIntervals.Min() : 1000;
}

// For DAQ collector:
// Enabled sensors all have Report.Interval: 1000
// CalculatedSendTelemetryPeriod = 1000 ms
```

### 7.2 Batch Send Task

```csharp
// DeviceBase.StartBatchSendTask()
private void StartBatchSendTask(CancellationToken ct)
{
    var sendPeriod = CalculatedSendTelemetryPeriod;
    _batchSendTask = Task.Run(async () =>
    {
        _logger.LogDebug("Starting batch send task with period {Period}ms for device {SubNodeId}", sendPeriod, SubNodeId);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Wait for the send period
                await Task.Delay(sendPeriod, ct);

                // Collect all enqueued measures
                var batch = new List<TelemetryMeasure>();
                while (_telemetryBatch.TryDequeue(out var measure))
                {
                    batch.Add(measure);
                }

                if (batch.Count > 0)
                {
                    _logger.LogDebug(
                        "Sending telemetry batch: {Count} measures for device {SubNodeId}",
                        batch.Count, SubNodeId);
                    // Send to WedaCore cloud
                    await SendTelemetryAsync(batch, ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Send remaining data before exit
            var remaining = new List<TelemetryMeasure>();
            while (_telemetryBatch.TryDequeue(out var measure))
            {
                remaining.Add(measure);
            }
            if (remaining.Count > 0)
            {
                _logger.LogDebug("Sending remaining {Count} measures before shutdown for device {SubNodeId}", remaining.Count, SubNodeId);
                await SendTelemetryAsync(remaining, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch send task for device {SubNodeId}", SubNodeId);
        }
    }, ct);
}
```

---

## Configuration to Runtime Mapping

| devicecfg.json Field           | Runtime Usage                                      | Example Value                |
| ------------------------------ | -------------------------------------------------- | ---------------------------- |
| `Properties.SamplingRate`      | Passed to `DaqCollector` and feature extractors    | `2500` (Hz)                  |
| `Properties.FrameSize`         | Accumulation trigger in `DaqCollector`             | `2500` (samples)             |
| `Properties.FftSize`           | FFT window size in `FrequencyDomainExtractor`      | `2500`                       |
| `Properties.AxisName`          | Axis identifier in raw frame                       | `"X"`                        |
| `Sensors[0].ResourceId`        | Raw payload sensor identifier in SensorCache       | `"daqraw:vibration:payload"` |
| `Sensors[1-9].ResourceId`      | PHM feature sensor identifiers for output          | `"x_axis_rms_mg"`, etc.      |
| `Sensors[*].Report.Enabled`    | Determines if sensor participates in sampling loop | `true`                       |
| `Sensors[*].Report.Interval`   | Sampling/reporting interval; used to group sensors | `1000` (ms)                  |
| `Sensors[*].Name`              | Human-readable sensor name                         | `"x_axis_rms_mg"`            |
| `Sensors[*].SensorInfo.Schema` | DTDL data type for telemetry                       | `"double"` or `"string"`     |

---

## Complete Data Flow Summary

```
┌─────────────────────────────────────────────────────────────────────┐
│ 1. Configuration Loading (devicecfg.json)                           │
│    Properties → DaqCollector init                                   │
│    Sensors → DeviceConfiguration.Sensors                            │
└─────────────┬───────────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────────┐
│ 2. Device Initialization                                            │
│    UniaxialVibrationDevice → CreateStreamingParser()                │
│    → DaqMetricsParser + DaqCommunication + DaqCollector             │
└─────────────┬───────────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────────┐
│ 3. Stream Start & Raw Data Collection                               │
│    Advantech.Edge.Daq → DaqCollector.StreamFramesAsync()            │
│    → DaqRawFrame (2500 samples @ 1 sec)                             │
└─────────────┬───────────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────────┐
│ 4. Serialization to TelemetryMeasure                                │
│    DaqRawFrame → JSON payload                                       │
│    → TelemetryMeasure(ResourceId: "daqraw:vibration:payload")       │
│    → SensorCache                                                    │
└─────────────┬───────────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────────┐
│ 5. Sensor Grouping & Interval Loop (every 1000ms)                   │
│    GroupSensorsByInterval() → all sensors at 1000ms interval        │
│    RunIntervalLoopAsync() → sample all 10 sensors from SensorCache  │
└─────────────┬───────────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────────┐
│ 6. Transform Pipeline                                               │
│    PhmFeatureTransform.TransformAsync()                             │
│    Input: daqraw:vibration:payload (1 measure)                      │
│    Process: Extract time/freq features                              │
│    Output: 9 PHM feature measures + metadata                        │
└─────────────┬───────────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────────┐
│ 7. Batch Enqueue                                                    │
│    EnqueueTelemetryAsync() → _telemetryBatch                        │
│    (holds transformed measures until batch send time)               │
└─────────────┬───────────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────────┐
│ 8. Batch Send to Cloud (every 1000ms)                               │
│    StartBatchSendTask() → runs async loop                           │
│    → _telemetryBatch.TryDequeue() → SendTelemetryAsync()            │
│    → _cloudService.SendTelemetryAsync(batch)                        │
│    → WedaCore Cloud                                                 │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Key Design Decisions

### Why Split into RAW + PHM Sensors?

1. **RAW Sensor** (`daqraw:vibration:payload`):
   - Internal transit format for raw vibration data
   - Not directly reported to cloud (consumed by transform)
   - Enables feature extraction pipeline isolation

2. **PHM Sensors** (9 computed features):
   - Final output of transformation
   - Each can be independently enabled/disabled via `Report.Enabled`
   - Each has configurable reporting interval via `Report.Interval`
   - Mapped to DTDL interface for cloud digital twin

### Why Interval Grouping?

- **Efficiency**: Sensors with the same interval are sampled together in one loop
- **Scalability**: Support devices with multiple sensors at different intervals
- **Configurability**: Adjust intervals remotely without code changes

### Why SensorCache?

- **Decoupling**: Streaming data arrives asynchronously; cache buffers latest values
- **Interval Flexibility**: Sample cached data at any interval independent of stream arrival rate
- **Multi-sensor Support**: Cache holds latest measurements for all sensors

---

## Configuration Best Practices

### 1. Set Appropriate SamplingRate & FrameSize

```json
{
  "SamplingRate": 2500,    // Must match hardware capability
  "FrameSize": 2500        // Duration = FrameSize / SamplingRate = 1 second
}
```

- Typical vibration monitoring: 2500-5000 Hz
- Ensure FrameSize is large enough for meaningful FFT features
- Frame duration should be short enough for responsive monitoring (< 5 seconds)

### 2. Enable Only Required Sensors

```json
{
  "Sensors": [
    {
      "Name": "x_axis_rms_mg",
      "Report": {
        "Enabled": true,     // Only enable needed metrics
        "Interval": 1000     // All sensors at same interval for efficiency
      }
    }
  ]
}
```

### 3. Configure Consistent Intervals

```json
{
  "Report": {
    "Interval": 1000   // All enabled sensors should use same interval
  }
}
```

- Simplifies scheduling (single periodic timer)
- Reduces CPU overhead
- Can be changed remotely via cloud configuration update

### 4. Use Thresholds for Anomaly Detection

```json
{
  "Report": {
    "Thresholds": {
      "LowerWarning": 10,
      "LowerCritical": 5,
      "UpperWarning": 35,
      "UpperCritical": 40
    }
  }
}
```

---

## Troubleshooting Guide

| Issue                      | Cause                                  | Solution                                          |
| -------------------------- | -------------------------------------- | ------------------------------------------------- |
| No telemetry sent          | `Report.Enabled: false` on all sensors | Enable at least one sensor in `devicecfg.json`    |
| Features are NaN           | Invalid SamplingRate or FrameSize      | Verify `Properties.SamplingRate` matches hardware |
| High latency               | `Report.Interval` too large            | Reduce `Report.Interval` in `devicecfg.json`      |
| High CPU usage             | Too many enabled sensors or intervals  | Disable unused sensors, consolidate intervals     |
| Configuration update fails | Schema mismatch                        | Verify `Sensors[].ResourceId` uniqueness          |

---

## References

- [Implementation Guide](impl_guide_daq_data_collector.md)
- [Architecture Plan](arch_plan_daq_data_collector.md)
- [Feature Extraction Plan](feature_extraction_impl_plan.md)
