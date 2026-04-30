# devicecfg.json Data Flow in DAQ Data Collector

## Overview

This document details how the `devicecfg.json` configuration file is utilized throughout the entire DAQ data collection pipeline in the UniaxialVibrationDevice. The configuration serves two main purposes:

1. **Properties Section**: Provides DAQ hardware parameters (acceleration sampling rate and decimation factor)
2. **Sensors Section**: Defines the output telemetry schema and reporting intervals for the transformed PHM features (11 sensors total: 1 raw payload + 9 PHM features + 1 timestamp + 1 device time)

---

## Architecture Overview

```
devicecfg.json
    ├── Dtdl (DTDL Auto-generation settings)
    ├── DeviceCommunication (DAQ module hardware configuration)
    ├── Properties (DAQ Parameters)
    │   ├── AccelerationSamplingRate → DaqCollector hardware sampling rate
    │   └── DecimationFactor → Sample rate reduction factor
    │
    └── Sensors (Output Telemetry Schema - 11 sensors)
        ├── daqraw_vibration_payload (internal raw input, AI group, disabled)
        ├── 9 AI sensors (PHM features: time-domain + frequency-domain)
        └── 2 SYS sensors (metadata: timestamp + device time)
```

---

## Phase 1: Configuration Loading

### 1.1 Configuration File Structure

```json
{
  "SubNode": {
    "Name": "UniaxialVibrationDevice",
    "SubNodeType": "DaqDevice",
    "Manufacturer": "Advantech",
    "Model": "iDAQ-801+B10BG3",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "UniaxialVibrationDeviceConfig": {
      "Enabled": true,
      "Dtdl": {
        "AutoGenEnabled": true
      },
      "DeviceCommunication": {
        "DaqModuleDeviceNumber": 0
      },
      "Properties": {
        "AccelerationSamplingRate": 5000,
        "DecimationFactor": 2
      },
      "Sensors": [
        {
          "Name": "daqraw_vibration_payload",
          "SensorGroup": "AI",
          "Report": {
            "Enabled": false,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "application/json",
            "DisplayName": "Raw Vibration Payload",
            "Description": "Raw DAQ frame JSON payload for feature extraction pipeline"
          }
        },
        {
          "Name": "x_axis_rms_mg",
          "SensorGroup": "AI",
          "Parameters": {
            "FeatureName": "XAxisRMSmg"
          },
          "Report": {
            "Enabled": true,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis RMSmg",
            "Description": "RMS from FFT-transformed spectrum (Parseval energy)"
          }
        },
        {
          "Name": "x_axis_peak_mg",
          "SensorGroup": "AI",
          "Parameters": {
            "FeatureName": "XAxisPeakmg"
          },
          "Report": {
            "Enabled": true,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis Peakmg",
            "Description": "Maximum spectral magnitude in FFT spectrum"
          }
        },
        {
          "Name": "x_axis_peak_to_peak_displacement",
          "SensorGroup": "AI",
          "Parameters": {
            "FeatureName": "XAxisPeakToPeakDisplacement"
          },
          "Report": {
            "Enabled": true,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis Peak-to-Peak Displacement",
            "Description": "Peak-to-peak of displacement signal from double spectral integration"
          }
        },
        {
          "Name": "x_axis_oa_velocity",
          "SensorGroup": "AI",
          "Parameters": {
            "FeatureName": "XAxisOAVelocity"
          },
          "Report": {
            "Enabled": true,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis OA Velocity",
            "Description": "Overall velocity RMS from acceleration spectrum"
          }
        },
        {
          "Name": "x_axis_deviation",
          "SensorGroup": "AI",
          "Parameters": {
            "FeatureName": "XAxisDeviation"
          },
          "Report": {
            "Enabled": true,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis Deviation",
            "Description": "Sample standard deviation"
          }
        },
        {
          "Name": "x_axis_skewness",
          "SensorGroup": "AI",
          "Parameters": {
            "FeatureName": "XAxisSkewness"
          },
          "Report": {
            "Enabled": true,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis Skewness",
            "Description": "Third standardized central moment"
          }
        },
        {
          "Name": "x_axis_kurtosis",
          "SensorGroup": "AI",
          "Parameters": {
            "FeatureName": "XAxisKurtosis"
          },
          "Report": {
            "Enabled": true,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis Kurtosis",
            "Description": "Fourth standardized central moment"
          }
        },
        {
          "Name": "x_axis_crest_factor",
          "SensorGroup": "AI",
          "Parameters": {
            "FeatureName": "XAxisCrestFactor"
          },
          "Report": {
            "Enabled": true,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis Crest Factor",
            "Description": "Ratio of peak to RMS"
          }
        },
        {
          "Name": "timestamp_timestamp",
          "SensorGroup": "SYS",
          "Parameters": {
            "FeatureName": "Timestamp"
          },
          "Report": {
            "Enabled": true,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "dateTime",
            "DisplayName": "Timestamp",
            "Description": "Hardware sample timestamp"
          }
        },
        {
          "Name": "device_time",
          "SensorGroup": "SYS",
          "Parameters": {
            "FeatureName": "DeviceTime"
          },
          "Report": {
            "Enabled": true,
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "dateTime",
            "DisplayName": "Device Time",
            "Description": "Edge device system clock at feature computation time"
          }
        }
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
    public Dictionary<string, object> Properties { get; set; }  // AccelerationSamplingRate, DecimationFactor, etc.
    public List<Sensor> Sensors { get; set; }                   // 11 sensor definitions
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
    var accelerationSamplingRate = props.TryGetValue("AccelerationSamplingRate", out var asr) 
        ? int.Parse(asr!.ToString()!) 
        : 5000;  // Default: 5000 Hz
    
    var decimationFactor = props.TryGetValue("DecimationFactor", out var df) 
        ? int.Parse(df!.ToString()!) 
        : 2;  // Default: decimation factor 2
    
    // Effective sampling rate after decimation
    var effectiveSamplingRate = accelerationSamplingRate / decimationFactor;  // 2500 Hz

    // Step B: Initialize DaqCollector (wraps Advantech.Edge.Daq)
    var collector = new DaqCollector(
        accelerationSamplingRate: accelerationSamplingRate,  // 5000 Hz (hardware rate)
        decimationFactor: decimationFactor,                  // 2 (reduction factor)
        effectiveSamplingRate: effectiveSamplingRate,        // 2500 Hz (after decimation)
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

    // Validate AccelerationSamplingRate > 0
    if (props.TryGetValue("AccelerationSamplingRate", out var asr) &&
        int.TryParse(asr?.ToString(), out var accelerationSamplingRate) &&
        accelerationSamplingRate <= 0)
        throw new InvalidOperationException($"AccelerationSamplingRate must be positive, got: {accelerationSamplingRate}");

    // Validate DecimationFactor >= 1
    if (props.TryGetValue("DecimationFactor", out var df) &&
        int.TryParse(df?.ToString(), out var decimationFactor) &&
        decimationFactor < 1)
        throw new InvalidOperationException($"DecimationFactor must be >= 1, got: {decimationFactor}");

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

Given the `devicecfg.json` configuration, all enabled sensors have `Report.Interval: 1000`:

```csharp
// Result of GroupSensorsByInterval():
// Note: daqraw_vibration_payload is filtered out because Report.Enabled: false
var sensorGroups = new List<(int, List<Sensor>)>
{
    (IntervalMs: 1000, Sensors: new List<Sensor>
    {
        // AI sensors (9 PHM feature sensors)
        Sensor(name: "x_axis_rms_mg", sensorGroup: "AI", interval: 1000),
        Sensor(name: "x_axis_peak_mg", sensorGroup: "AI", interval: 1000),
        Sensor(name: "x_axis_peak_to_peak_displacement", sensorGroup: "AI", interval: 1000),
        Sensor(name: "x_axis_oa_velocity", sensorGroup: "AI", interval: 1000),
        Sensor(name: "x_axis_deviation", sensorGroup: "AI", interval: 1000),
        Sensor(name: "x_axis_skewness", sensorGroup: "AI", interval: 1000),
        Sensor(name: "x_axis_kurtosis", sensorGroup: "AI", interval: 1000),
        Sensor(name: "x_axis_crest_factor", sensorGroup: "AI", interval: 1000),
        
        // SYS sensors (2 metadata sensors)
        Sensor(name: "timestamp_timestamp", sensorGroup: "SYS", interval: 1000),
        Sensor(name: "device_time", sensorGroup: "SYS", interval: 1000)
    })
};
```

**Note**: The `daqraw_vibration_payload` sensor is defined in `devicecfg.json` but has `Report.Enabled: false`, so it is filtered out by `GroupSensorsByInterval()`. It is only used internally by the transform pipeline to generate PHM features.

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
// (called by ProcessIntervalGroupAsync in DeviceBase)

protected override Task<IntervalGroupReadResult> ReadSensorsForIntervalGroupAsync(
    List<string> sensorResourceIds,  // ["x_axis_rms_mg", "x_axis_peak_mg", ...]
    CancellationToken cancellationToken)
{
    // Read from SensorCache (where streamed data is stored)
    // SensorCache buffers latest telemetry values pushed from the stream
    var measures = _sensorCache.Read(sensorResourceIds);
    
    // Return wrapped result with read duration (TimeSpan.Zero for streaming - cache read is instant)
    return Task.FromResult(new IntervalGroupReadResult(measures, TimeSpan.Zero));
}
```

**Data Flow Note**: The `RaiseDataReceived()` event is called by `ProcessIntervalGroupAsync()` in `DeviceBase` after this method returns, not within this method. The complete flow is:

```
ProcessIntervalGroupAsync()
  1. Call ReadSensorsForIntervalGroupAsync() → read from cache
  2. RaiseDataReceived(measures)              → fire raw data event
  3. EnqueueTelemetryAsync(measures)          → apply transforms/filters
     └─ RaiseDataProcessed()                  → fire processed data event
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

        // Find the raw payload sensor by name
        // Note: ResourceId is auto-generated by the WEDA SubNode framework and maps to the sensor Name
        var rawPayloadMeasure = measures.FirstOrDefault(m => 
            m.SensorName == "daqraw_vibration_payload");

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
            // (one for each enabled sensor defined in devicecfg.json)
            outputMeasures.Add(new TelemetryMeasure
            {
                SensorName = "x_axis_rms_mg",
                Value = features.XAxisRMSmg,
                Timestamp = rawFrame.Timestamp
            });

            outputMeasures.Add(new TelemetryMeasure
            {
                SensorName = "x_axis_peak_mg",
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

| devicecfg.json Field                    | Runtime Usage                                           | Example Value                |
| --------------------------------------- | ------------------------------------------------------- | ---------------------------- |
| `Dtdl.AutoGenEnabled`                   | Enable automatic DTDL interface generation              | `true`                       |
| `DeviceCommunication.DaqModuleDeviceNumber` | DAQ hardware module device ID                       | `0`                          |
| `Properties.AccelerationSamplingRate`   | Hardware sampling rate passed to DAQ collector          | `5000` (Hz)                  |
| `Properties.DecimationFactor`           | Sample rate reduction factor                            | `2`                          |
| `Sensors[0].Name`                       | Raw payload sensor name (AI group, disabled)            | `"daqraw_vibration_payload"` |
| `Sensors[0].Report.Enabled`             | Raw payload disabled (internal transform use only)      | `false`                      |
| `Sensors[1-9].Name`                     | PHM feature sensor names (AI group, enabled)            | `"x_axis_rms_mg"`, etc.      |
| `Sensors[10-11].Name`                   | Metadata sensor names (SYS group, enabled)              | `"timestamp_timestamp"`, `"device_time"` |
| `Sensors[*].SensorGroup`                | Sensor classification category                          | `"AI"` or `"SYS"`            |
| `Sensors[*].Report.Enabled`             | Determines if sensor participates in sampling loop      | `true` or `false`            |
| `Sensors[*].Report.Interval`            | Sampling/reporting interval in milliseconds             | `1000` (ms)                  |
| `Sensors[*].Parameters.FeatureName`     | Internal feature computation identifier                 | `"XAxisRMSmg"`, etc.         |
| `Sensors[*].SensorInfo.Schema`          | DTDL data type for telemetry                            | `"double"`, `"dateTime"`, etc. |
| `Sensors[*].SensorInfo.Description`     | Human-readable feature description                      | Describes the extracted feature |

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
│    → TelemetryMeasure(SensorName: "daqraw_vibration_payload")       │
│    → SensorCache                                                    │
└─────────────┬───────────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────────┐
│ 5. Sensor Grouping & Interval Loop (every 1000ms)                   │
│    GroupSensorsByInterval() → all enabled sensors at 1000ms interval│
│    RunIntervalLoopAsync() → sample all 10 enabled sensors from Cache│
└─────────────┬───────────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────────┐
│ 6. Transform Pipeline                                               │
│    PhmFeatureTransform.TransformAsync()                             │
│    Input: daqraw_vibration_payload (1 measure)                      │
│    Process: Extract time-domain & frequency-domain features         │
│    Output: 9 PHM feature measures (AI) + 1 timestamp (SYS)          │
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

1. **RAW Sensor** (`daqraw_vibration_payload`, AI group):
   - Internal transit format for raw vibration data
   - Disabled in configuration (`Report.Enabled: false`)
   - Not reported to cloud (consumed internally by transform)
   - Enables feature extraction pipeline isolation

2. **PHM Sensors** (9 computed features, AI group):
   - Final output of transformation (time-domain + frequency-domain)
   - Each can be independently enabled/disabled via `Report.Enabled`
   - Each has configurable reporting interval via `Report.Interval`
   - Mapped to DTDL interface for cloud digital twin

3. **SYS Sensors** (2 metadata sensors):
   - `timestamp_timestamp`: Hardware sample timestamp from raw frame
   - `device_time`: Edge device system time at feature computation
   - Provides temporal context for measurements

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

### 1. Set Appropriate Hardware Sampling Rate and Decimation

```json
{
  "Properties": {
    "AccelerationSamplingRate": 5000,    // Hardware sampling rate (Hz)
    "DecimationFactor": 2                // Decimation factor for sample reduction
  }
}
```

- `AccelerationSamplingRate` must match DAQ hardware capability (typically 5000-10000 Hz for vibration)
- `DecimationFactor` reduces the effective sampling rate: Effective Rate = AccelerationSamplingRate / DecimationFactor
- Example: 5000 Hz / 2 = 2500 Hz effective rate
- Higher decimation saves bandwidth but reduces frequency resolution

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

| Issue                      | Cause                                         | Solution                                            |
| -------------------------- | --------------------------------------------- | --------------------------------------------------- |
| No telemetry sent          | All sensors disabled (`Report.Enabled: false`)| Enable at least one sensor in `devicecfg.json`      |
| Features are NaN           | Invalid AccelerationSamplingRate or DecimationFactor | Verify parameters match DAQ hardware capability |
| High latency               | `Report.Interval` too large                   | Reduce `Report.Interval` in `devicecfg.json`        |
| High CPU usage             | Too many enabled sensors or low intervals     | Disable unused sensors, consolidate intervals       |
| Raw data reported to cloud | `daqraw_vibration_payload` has `Enabled: true` | Set `Enabled: false` (should only transform internally) |
| Missing timestamp data     | `timestamp_timestamp` or `device_time` disabled | Enable both SYS group sensors for temporal context |

---

## References

- [Implementation Guide](impl_guide_daq_data_collector.md)
- [Architecture Plan](arch_plan_daq_data_collector.md)
- [Feature Extraction Plan](feature_extraction_impl_plan.md)
