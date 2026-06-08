# PHM Window-Aligned Streaming Proposal

**Date:** June 4, 2026 (Updated: June 8, 2026)
**Author:** Edge SubNode Development Team  
**Status:** Final (v2.0 - Independent Transform Approach)
**Version:** 2.0

---

## Executive Summary

This proposal outlines the implementation of a **PHM Window-Aligned Streaming** feature for the DAQ Data Collector service. The feature enables continuous, high-frequency publication of complete PHM (Predictive Health Maintenance) features to custom NATS topics, with publishing frequency aligned to the observation window (more immediate than standard telemetry's variable intervals). This allows downstream systems to consume machine learning features directly at the edge with predictable timing.

**Key Benefits:**
- 🎯 **Complete Feature Publishing**: Always publishes all 10 PHM features together (no partial batches)
- ⚡ **Window-Aligned Frequency**: Publishing rate tied to `ObservationWindowSeconds` (more immediate than Report.Interval)
- 📊 **Semantic Naming**: Use sensor names from configuration as `sensorId` for better readability
- 🔌 **Seamless Integration**: Independent transform pipeline, no impact on standard telemetry
- ⏱️ **Predictable Cadence**: One message per observationWindow with guaranteed complete feature set

---

## 1. Problem Statement

### Current Limitations

The current DAQ Data Collector implementation sends all telemetry (both raw vibration data and transformed PHM features) through the standard SubNode telemetry pipeline to the default NATS topic (`eco1j.weda.{deviceId}.telemetry`). This approach has limitations:

1. **No Selective Routing**: All features go to the same topic; cannot route specific features to specialized processing systems
2. **Limited Flexibility**: No way to send data to custom topics for ML inference, analytics, or edge processing
3. **Single Topic Constraint**: Cannot parallelize feature distribution or implement load balancing across multiple subscribers

### Use Cases

1. **Edge ML Inference**: Send features to a local ML inference engine running on the same edge device
2. **Feature Stream Processing**: Route features to Apache Kafka or Flink for real-time analytics
3. **Multi-Sink Distribution**: Send same features to multiple downstream systems simultaneously
4. **Isolated Feature Pipeline**: Maintain independent telemetry pipeline for features vs. raw sensor data

---

## 2. Proposed Solution

### 2.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│         UniaxialVibrationDevice                             │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ 1. Stream Data → SensorCache                           │ │
│  │    ↓                                                   │ │
│  │ 2. RunIntervalLoopAsync (processes based on intervals) │ │
│  │    ↓                                                   │ │
│  │ 3. ProcessIntervalGroupAsync                           │ │
│  │    │                                                   │ │
│  │    ├─ EnqueueTelemetryAsync                            │ │
│  │    │  ├─ Transform #1: PhmFeatureTransform (decimated) │ │
│  │    │  ├─ Filter                                        │ │
│  │    │  └─ Send (standard pipeline)                      │ │
│  │    │     └─ NATS: eco1j.weda.{deviceId}.telemetry      │ │
│  │    │                                                   │ │
│  │    └─ OnDataReceived Hook [NEW]                        │ │
│  │       └─ Transform #2: WindowAlignedPhmTransform       │ │
│  │          (independent, NO decimation)                  │ │
│  │          └─ SendTransformedFeaturesAsync               │ │
│  │             ├─ Always 10 complete features             │ │
│  │             ├─ Map ResourceId → Sensor.Name            │ │
│  │             └─ Publish to custom NATS topic            │ │
│  │                └─ NATS: phm.vibration.windowed          │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 Design Rationale: Independent Transform Approach

The **WindowAlignedPhmTransform** (Transform #2) is a completely independent instance that:

1. **Disables Frame Decimation**: Sets all PHM sensor intervals to observationWindowMs, ensuring all 10 features emit together
   - Avoids the problem where slow sensors (e.g., Report.Interval=5000ms) cause delayed or incomplete batches
   - Guarantees predictable payload structure (always 10 features, never 1-9)
   - Publishing frequency dynamically tied to observationWindow configuration

2. **Decoupled Pipeline**: Does NOT hook into the standard telemetry pipeline
   - Triggered at raw data reception (OnDataReceived), not after standard Transform/Filter
   - Zero impact on standard telemetry routing or framework behavior
   - Window-aligned streaming frequency = observationWindow frequency (configurable per deployment)

3. **Minimal Overhead**: Reuses PhmFeatureTransform logic with unified configuration
   - No code duplication: same transform logic, different interval mappings
   - Memory footprint: one additional transform instance (negligible)

### 2.3 Data Flow

```
Raw DAQ Payload
  ↓
┌─ Transform #1: PhmFeatureTransform (with decimation)
│  └─→ Variable: 1-10 features per cycle
│      └─→ Standard Pipeline
│          └─→ NATS: eco1j.weda.{deviceId}.telemetry
│
└─ Transform #2: WindowAlignedPhmTransform (NO decimation)
   └─→ Complete: Always 10 features per cycle
       ├─ For each TelemetryMeasure:
       │  ├─ Look up Sensor via resourceId
       │  ├─ Extract Sensor.Name (e.g., "x_axis_rms_mg")
       │  └─ Create measure object with sensorId = Name
       │
       └─→ NATS: phm.vibration.windowed
          {
            "deviceId": "318281269764947968",
            "timestamp": 1717504800000,
            "data": {
              "measures": [
                {
                  "sensorId": "x_axis_rms_mg",
                  "resourceId": "uuid-for-rms",
                  "value": 42.5
                },
                ...
              ]
            }
          }
```

---

## 3. Technical Specification

### 3.1 Configuration Schema

No additional configuration required. Window-aligned streaming is **always enabled** and publishes to a **hardcoded NATS subject**.

**Hardcoded Values:**

| Parameter | Value | Description |
|-----------|-------|-------------|
| `CustomTelemetryEnabled` | `true` | Always enabled - no switch needed |
| `CustomTelemetrySubject` | `"phm.vibration.windowed"` | Hardcoded NATS subject for feature publishing |

### 3.2 Implementation Details

#### 3.2.1 Class Fields

```csharp
private NatsClient? _natsClient;
private const string CustomTelemetrySubject = "phm.vibration.windowed";  // Hardcoded
private const bool CustomTelemetryEnabled = true;  // Always enabled
private PhmFeatureTransform? _windowAlignedPhmTransform;  // Independent transform (no decimation)
```

#### 3.2.2 Constructor Modifications

No changes needed - window-aligned streaming is always enabled with hardcoded subject.

```csharp
public UniaxialVibrationDevice(
    IWedaApplicationContext context,
    DeviceConfiguration configuration)
    : base(context, configuration, CreateStreamingParser(context, configuration))
{
    // ... existing code ...
    // No window-aligned streaming configuration code needed
}
```

#### 3.2.3 Initialization

In `OnAfterInitializeAsync`, initialize NATS client and custom transform (always executed):

```csharp
protected override async Task OnAfterInitializeAsync(CancellationToken ct)
{
    var resourceMapping = BuildPhmResourceMapping(Configuration);
    if (resourceMapping != null)
    {
        RegisterPhmTransform(_context, Configuration, resourceMapping.Value);
    }

    // Always initialize NATS client for window-aligned streaming
    if (CustomTelemetryEnabled)
    {
        await InitializeNatsClientAsync(ct);
        RegisterCustomPhmTransform(_context, Configuration, resourceMapping?.Value);  // ← Always runs
        _logger.LogInformation(
            "✅ Window-Aligned Streaming enabled: Subject={Subject}",
            CustomTelemetrySubject);
    }

    await base.OnAfterInitializeAsync(ct);
}
```

#### 3.2.4 Custom PHM Transform Registration

Register independent transform with no decimation (always emits 10 features):

```csharp
private void RegisterCustomPhmTransform(
    IWedaApplicationContext context,
    DeviceConfiguration configuration,
    (Dictionary<string, string> PhMSensorResourceIds, string RawSensorResourceId)? resourceMapping)
{
    if (resourceMapping == null) return;

    try
    {
        var props = configuration.Properties;
        var acquisitionRate = props.TryGetValue("AcquisitionRateHz", out var ar) 
            ? int.Parse(ar!.ToString()!) : 1000;
        var observationWindowSeconds = props.TryGetValue("ObservationWindowSeconds", out var ow) 
            ? double.Parse(ow!.ToString()!) : 1.0;
        var frameSize = (int)(acquisitionRate * observationWindowSeconds);

        // Calculate observationWindow in milliseconds
        var observationWindowMs = (int)(observationWindowSeconds * 1000);

        // Create independent transform with ALL sensors set to observationWindowMs interval
        // This effectively disables decimation: all features always emit together
        // Publishing frequency is tied to observationWindow (hardcoded window-aligned streaming is always active)
        // if observation=1000ms → 1 msg/sec, if observation=2000ms → 0.5 msg/sec, etc.
        var allSensorsAtObservationWindow = new Dictionary<string, int>
        {
            { "timestamp_timestamp", observationWindowMs },
            { "device_time", observationWindowMs },
            { "x_axis_rms_mg", observationWindowMs },
            { "x_axis_peak_mg", observationWindowMs },
            { "x_axis_peak_to_peak_displacement", observationWindowMs },
            { "x_axis_oa_velocity", observationWindowMs },
            { "x_axis_deviation", observationWindowMs },
            { "x_axis_skewness", observationWindowMs },
            { "x_axis_kurtosis", observationWindowMs },
            { "x_axis_crest_factor", observationWindowMs }
        };

        _windowAlignedPhmTransform = new PhmFeatureTransform(
            samplingRate: acquisitionRate,
            fftSize: frameSize,
            rawPayloadResourceId: resourceMapping.Value.RawSensorResourceId,
            phMSensorResourceIds: resourceMapping.Value.PhMSensorResourceIds,
            sensorIntervalMs: allSensorsAtObservationWindow,  // ← All bound to observationWindowMs
            observationWindowMs: observationWindowMs,
            logger: context.LoggerFactory.CreateLogger<PhmFeatureTransform>());

        _logger.LogInformation("✅ Custom PHM Transform registered (complete 10 features per cycle)");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to register custom PHM transform");
    }
}
```

#### 3.2.5 NATS Client Initialization

Read NATS connection settings from `systemcfg.json`:

```csharp
private async Task InitializeNatsClientAsync(CancellationToken ct)
{
    try
    {
        var systemConfigPath = Path.Combine(AppContext.BaseDirectory, "systemcfg.json");
        
        string natsUrl = "nats://127.0.0.1:4224";
        string username = "advantech_nats";
        string password = "3671be64607240cbc2b95af99c9a3b28fb5f9aa3fbe51f501478f1a678e19d48";

        if (File.Exists(systemConfigPath))
        {
            var config = JsonDocument.Parse(File.ReadAllText(systemConfigPath)).RootElement;
            if (config.TryGetProperty("WedaNode", out var wedaNode))
            {
                natsUrl = $"nats://{wedaNode.GetProperty("Url").GetString()}";
                username = wedaNode.GetProperty("Username").GetString() ?? username;
                password = wedaNode.GetProperty("Password").GetString() ?? password;
            }
        }

        var natsOpts = NatsOpts.Default with
        {
            Url = natsUrl,
            AuthOpts = new NatsAuthOpts { Username = username, Password = password }
        };

        _natsClient = new NatsClient(natsOpts);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to initialize custom NATS client");
    }
}
```

#### 3.2.6 Feature Publishing

Transform raw measures using window-aligned transform and publish all 10 features to custom subject:

```csharp
/// <summary>
/// Publish complete 10 PHM features to custom NATS subject.
/// Uses independent window-aligned PHM transform with no decimation.
/// Uses Sensor.Name from devicecfg.json as sensorId.
/// </summary>
private async Task SendTransformedFeaturesAsync(
    List<TelemetryMeasure> rawMeasures, 
    CancellationToken ct = default)
{
    if (_natsClient == null || _windowAlignedPhmTransform == null)
        return;

    try
    {
        // Transform raw measures using window-aligned transform (always produces 10 features)
        var transformedFeatures = await _windowAlignedPhmTransform.TransformAsync(
            rawMeasures,
            new TelemetryTransformContext(),
            ct);

        if (transformedFeatures.Count != 10)
        {
            _logger.LogWarning(
                "Expected 10 features from custom transform, got {Count}",
                transformedFeatures.Count);
        }

        var measures = new List<object>();

        foreach (var feature in transformedFeatures)
        {
            // Look up Sensor by ResourceId
            var sensor = Configuration.Sensors?
                .FirstOrDefault(s => s.ResourceId == feature.ResourceId);
            var sensorName = sensor?.Name ?? feature.ResourceId;

            measures.Add(new
            {
                sensorId = sensorName,           // Use Sensor.Name from config
                resourceId = feature.ResourceId,  // Keep UUID for traceability
                value = feature.Value
            });

            _logger.LogDebug(
                "Feature: sensorId={SensorId}, value={Value}, timestamp={Timestamp}",
                sensorName, feature.Value, feature.Timestamp);
        }

        var message = new
        {
            deviceId = SubNodeId,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            data = new
            {
                measures = measures,
                source = "UniaxialVibrationDevice"
            }
        };

        var json = JsonSerializer.Serialize(message);
        await _natsClient.PublishAsync(CustomTelemetrySubject, json, cancellationToken: ct);
        
        _logger.LogInformation(
            "✅ Published {Count} complete PHM features to Subject '{Subject}'",
            measures.Count, CustomTelemetrySubject);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to publish transformed features");
    }
}
```

#### 3.2.7 Event Hook

Hook into OnDataReceived (raw stream) to trigger window-aligned transform and publish:

```csharp
private void OnDataReceived(object? sender, TelemetryDataReceivedEventArgs e)
{
    // Window-aligned streaming: always publish complete 10 features per observation window
    if (_natsClient != null && _windowAlignedPhmTransform != null)
    {
        _ = SendTransformedFeaturesAsync(e.Measures);
    }
}
```

---

## 4. Configuration Examples

### 4.1 Standard Configuration

**devicecfg.json** (No window-aligned streaming configuration needed):
```json
{
  "DeviceConfigs": {
    "UniaxialVibrationDeviceConfig": {
      "Properties": {
        "AcquisitionRateHz": 1000,
        "ObservationWindowSeconds": 1.0
      }
    }
  }
}
```

**systemcfg.json** (NATS credentials required):
```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "Username": "advantech_nats",
    "Password": "3671be64607240cbc2b95af99c9a3b28fb5f9aa3fbe51f501478f1a678e19d48"
  }
}
```

### 4.2 Hardcoded Behavior

- ✅ **Window-aligned streaming is always enabled** - no configuration switch
- ✅ **Subject is always** `phm.features.vibration.analysis` - no configuration needed
- ✅ **Simplifies deployment** - fewer configuration parameters to manage

---

## 5. Data Format Specification

### 5.1 Published Message

**Note:** The `measures` array **always contains exactly 10 features** per cycle.

**Standard telemetry** (Transform #1) contains **1 to 10 features** per cycle, depending on each PHM sensor's `Report.Interval` configuration.
Based on frame decimation logic: a sensor emits when `(frameCounter % decimationFactor) == 0`,
where `decimationFactor = ceil(sensorInterval / observationWindowMs)`.

**Window-aligned streaming** (Transform #2) always contains **exactly 10 features** because all sensor intervals are set to `observationWindowMs` (decimation factor always 1).
Publishing frequency is tied to configuration:
- If `ObservationWindowSeconds = 1.0` → features published ~1 time/second
- If `ObservationWindowSeconds = 2.0` → features published ~0.5 times/second

**Example:**
```json
{
  "deviceId": "318281269764947968",
  "timestamp": 1717504800000,
  "data": {
    "measures": [
      {
        "sensorId": "timestamp_timestamp",
        "resourceId": "uuid-for-timestamp-sensor",
        "value": 1717504800000
      },
      {
        "sensorId": "device_time",
        "resourceId": "uuid-for-device-time-sensor",
        "value": 1717504800000
      },
      {
        "sensorId": "x_axis_rms_mg",
        "resourceId": "uuid-for-rms-sensor",
        "value": 42.5
      },
      {
        "sensorId": "x_axis_peak_mg",
        "resourceId": "uuid-for-peak-sensor",
        "value": 85.3
      },
      {
        "sensorId": "x_axis_skewness",
        "resourceId": "uuid-for-skewness-sensor",
        "value": 0.234
      },
      {
        "sensorId": "x_axis_kurtosis",
        "resourceId": "uuid-for-kurtosis-sensor",
        "value": 3.456
      },
      {
        "sensorId": "x_axis_deviation",
        "resourceId": "uuid-for-deviation-sensor",
        "value": 12.34
      },
      {
        "sensorId": "x_axis_oa_velocity",
        "resourceId": "uuid-for-velocity-sensor",
        "value": 5.67
      },
      {
        "sensorId": "x_axis_peak_to_peak_displacement",
        "resourceId": "uuid-for-displacement-sensor",
        "value": 0.123
      },
      {
        "sensorId": "x_axis_crest_factor",
        "resourceId": "uuid-for-crest-sensor",
        "value": 2.01
      }
    ],
    "source": "UniaxialVibrationDevice"
  }
}
```

### 5.2 Field Descriptions

| Field | Type | Source | Description |
|-------|------|--------|-------------|
| `deviceId` | string | SubNodeId | Unique device identifier |
| `timestamp` | long | DateTimeOffset.UtcNow | Message creation time (Unix ms) |
| `sensorId` | string | Sensor.Name | Human-readable sensor name from config |
| `resourceId` | string | TelemetryMeasure.ResourceId | UUID of the sensor resource |
| `value` | object | TelemetryMeasure.Value | Transformed feature value |
| `source` | string | constant | Source component identifier |

---

## 6. Consumption Example

### NATS Subscriber (C#)

```csharp
using NATS.Client.Core;
using NATS.Net;
using System.Text.Json;

var natsOpts = NatsOpts.Default with
{
    Url = "nats://127.0.0.1:4224",
    AuthOpts = new NatsAuthOpts 
    { 
        Username = "advantech_nats", 
        Password = "3671be64607240cbc2b95af99c9a3b28fb5f9aa3fbe51f501478f1a678e19d48" 
    }
};

var natsClient = new NatsClient(natsOpts);

await foreach (var msg in natsClient.SubscribeAsync<byte[]>("phm.vibration.windowed"))
{
    if (msg.Data == null) continue;

    var json = System.Text.Encoding.UTF8.GetString(msg.Data);
    var features = JsonSerializer.Deserialize<FeatureMessage>(json);
    
    Console.WriteLine($"Device: {features.DeviceId}");
    foreach (var measure in features.Data.Measures)
    {
        Console.WriteLine($"  {measure.SensorId} = {measure.Value}");
    }
}
```

### Python Subscriber

```python
import asyncio
import json
from nats.aio.client import Client

async def consume_features():
    nc = Client()
    await nc.connect("nats://127.0.0.1:4224", 
                     user="advantech_nats",
                     password="3671be64607240cbc2b95af99c9a3b28fb5f9aa3fbe51f501478f1a678e19d48")
    
    async def message_handler(msg):
        data = json.loads(msg.data.decode())
        device_id = data['deviceId']
        measures = data['data']['measures']
        
        for measure in measures:
            print(f"Device {device_id}: {measure['sensorId']} = {measure['value']}")
    
    await nc.subscribe("phm.vibration.windowed", cb=message_handler)
    await asyncio.sleep(float("inf"))

asyncio.run(consume_features())
```

---

## 7. Implementation Phases

### Phase 1: Core Implementation (Week 1)
- [ ] Add window-aligned streaming fields to `UniaxialVibrationDevice.cs`
- [ ] Implement `InitializeNatsClientAsync` method
- [ ] Implement `RegisterCustomPhmTransform` method
- [ ] Implement `SendTransformedFeaturesAsync` method
- [ ] Hook into `OnDataReceived` event

---

## 8. Error Handling & Resilience

### 8.1 Error Cases

1. **NATS Connection Failure**
   - Log error but don't block main telemetry pipeline
   - Attempt reconnection on next publish attempt

2. **Sensor Lookup Failure**
   - Fall back to `resourceId` if Sensor.Name not found
   - Log warning with resource ID for debugging

3. **JSON Serialization Error**
   - Catch and log, skip this publish cycle
   - Continue processing next features

### 8.2 Logging Strategy

- **INFO**: Successful publish, configuration changes
- **DEBUG**: Feature details (sensorId, value, timestamp)
- **WARNING**: Sensor not found, configuration issues
- **ERROR**: NATS connection failures, serialization errors

---

## 9. Performance Considerations

### 9.1 Overhead Analysis

| Operation | Overhead | Notes |
|-----------|----------|-------|
| Custom Transform | < 2ms | PhmFeatureTransform with no decimation overhead |
| Dictionary lookup | < 0.1ms | Configuration.Sensors indexed by ResourceId |
| JSON serialization | < 1ms | Fixed 10 features per publish |
| NATS publish | Network dependent | Fire-and-forget, non-blocking |
| Total per cycle | < 5ms | Negligible compared to observationWindow interval |

### 9.2 Scalability

- Always emits complete 10 features per observationWindow cycle (no variable counts)
- Independent transform adds minimal overhead (separate instance, no impact on standard telemetry)
- Publishing frequency: tied to `ObservationWindowSeconds` configuration (flexible & predictable)
  - Example: 1000ms observation → ~1 msg/sec, 2000ms observation → ~0.5 msg/sec
- NATS can handle 1000s of messages/second

---

## 10. Security Considerations

### 10.1 Authentication

- Uses same NATS credentials as standard telemetry pipeline
- Credentials read from `systemcfg.json`
- No additional credentials exposure

### 10.2 Authorization

- Custom subject follows same NATS authorization rules
- Can be restricted via NATS server ACLs

### 10.3 Data Sensitivity

- Features contain machine vibration data (not PII)
- Encryption handled by NATS TLS (optional)

---

## 11. Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Feature Publish Success Rate** | > 99.9% | Messages published / attempts |
| **Feature Count Consistency** | 100% | Always 10 features per message |
| **End-to-End Latency** | < 100ms | Publish time from raw frame to consumed |
| **System Overhead** | < 2% | Dual transform impact vs baseline |
| **Message Format Compliance** | 100% | All messages match schema |
| **Independent Pipeline** | 100% | Window-aligned streaming isolated from standard telemetry |

---

## 12. Rollback Plan

If issues arise after deployment:

1. **Disable window-aligned streaming in code**: Set `CustomTelemetryEnabled = false` in UniaxialVibrationDevice.cs (requires code change & recompile)
2. Alternatively, comment out the `RegisterCustomPhmTransform` call in `OnAfterInitializeAsync`
3. Restart service
4. Standard telemetry pipeline continues operating normally
5. Investigate root cause in non-production environment

**Future Enhancement:** Consider adding a configuration override if runtime enable/disable becomes necessary

---

## 13. Dependencies

### Required NuGet Packages
- `NATS.Net` (already in project)
- `System.Text.Json` (built-in)

### Framework Components
- `IWedaApplicationContext` (provided)
- `DeviceConfiguration` (existing)
- `TelemetryMeasure` (existing)

---

## 14. Future Enhancements

1. **Batch Publishing**: Accumulate features before publish to reduce overhead
2. **Feature Filtering**: Only publish specific features to custom subject
3. **Multi-Topic Routing**: Route different features to different topics
4. **Custom Serialization**: Support Protocol Buffers or MessagePack for efficiency
5. **Retry Policy**: Implement exponential backoff for failed publishes

---

## 15. Appendix

### A. File Locations

```
/home/advantech/vincent/edge_subnode/
├── examples/daq-data-collector/
│   ├── Devices/UniaxialVibrationDevice.cs       (implementation)
│   ├── devicecfg.json                           (config)
│   └── systemcfg.json                           (NATS settings)
└── docs/phm_window_aligned_streaming_proposal.md (this file)
```

### B. References

- [NATS Documentation](https://docs.nats.io/)
- [SubNode Telemetry Pipeline](../docs/telemetry-pipeline.md)
- [PHM Feature Transform](../Communication/Pipeline/PhmFeatureTransform.cs)

### C. Approval & Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Author | Development Team | 2026-06-04 | |
| Revisor | Technical Lead | 2026-06-08 | |
| Approver | Product Manager | | |

---

**End of Document**
