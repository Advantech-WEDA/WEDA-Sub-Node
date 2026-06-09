# PHM Vibration Feature Proxy Proposal (PUB-SUB Architecture)

**Date:** June 9, 2026 (Revised June 16, 2026)  
**Author:** Edge SubNode Development Team  
**Status:** Implementation In Progress (v5.0)
**Version:** 5.0

---

## Executive Summary

This proposal outlines the implementation of a **PHM Vibration Feature Proxy SubNode** that operates as an independent, asynchronous service. The proxy subscribes to standard SubNode telemetry published by the DAQ Data Collector, processes the vibration features through the PHM Service inference API (anomaly detection), and outputs the results as standard SubNode sensor telemetry to the cloud via NATS.

**Key Benefits:**
- Async Processing: Fire-and-forget PUB-SUB pattern - no blocking or timeouts
- SubNode Native: Full SubNode lifecycle with standard device configuration
- Cloud Integration: Results published as sensor telemetry (auto-uploaded to cloud)
- Complete Decoupling: DAQ and Proxy operate independently
- Resilient Handling: Proxy failures don't affect DAQ data collection
- Dynamic Feature Discovery: On startup, queries DAQ device capabilities via NATS Request to dynamically resolve both the subscription topic (`eco1j.weda.{deviceId}.telemetry`) and the `resourceShortId → API field name` mapping — no hardcoded sensor list or topic required

---

## 1. Problem Statement

### Current Limitations

The current DAQ Data Collector publishes window-aligned PHM features to NATS but they are not analyzed or acted upon at the edge:

1. **No Edge Analysis**: Features published but not consumed by analysis services
2. **No Cloud Insights**: Proxy analysis results not sent to cloud for long-term monitoring
3. **Tight Coupling**: Would require DAQ to wait for synchronous responses
4. **Operational Visibility**: Cannot track proxy service health as independent SubNode
5. **Hardcoded Feature Mapping**: DAQ sensor IDs are statically mapped to PHM Service field names, making it brittle when the DAQ device configuration changes or resources are re-provisioned

### Use Cases

1. **Real-Time Cloud Analytics**: Proxy analysis results auto-published to cloud via telemetry
2. **Predictive Maintenance Pipeline**: Health scores tracked over time for trend analysis
3. **Distributed Processing**: Multiple proxy services can process features independently
4. **Service Monitoring**: Proxy SubNode appears in cloud dashboard with health status
5. **Dynamic Sensor Discovery**: Query DAQ device capability at startup to obtain the `deviceId` (for subscription topic derivation) and `resourceShortId → sensor name` mapping (for feature identification), enabling automatic mapping from incoming telemetry `sensorId` values to PHM Service inference field names

---

## 2. Proposed Solution

### 2.1 High-Level Architecture

```
                      [STARTUP ONLY — NATS Request/Reply]
                      ┌─────────────────────────────────────────────────────┐
                      │ eco1j.weda.dm.subnode.cap.query                     │
                      │   req: { data: {} } (query all)                     │
                      │   rsp: deviceId + sensors (name+resourceId per item)│
                      └──────────┬──────────────────────────────────────────┘
                                 │
┌─────────────────────────────────────────────────────────────────────┐
│ DAQ Data Collector (daq-data-collector)                             │
│  ├─ Respond: eco1j.weda.dm.subnode.cap.query.req  (capabilities)    │
│  └─ Publish: eco1j.weda.{deviceId}.telemetry (standard telemetry)   │
└──────────────────────────────┬──────────────────────────────────────┘
                               │
                               ↓ NATS PUB-SUB
                               │
┌──────────────────────────────────────────────────────────────────────┐
│ Feature Proxy SubNode (daq-feature-proxy)  [INDEPENDENT SubNode]     │
│  ├─ [Startup] Query DAQ device cap → derive topic + build feature map│
│  │    └─ DeviceCapabilityClient                                      │
│  │        ├─ Request: eco1j.weda.dm.subnode.cap.query.req            │
│  │        ├─ Receive: deviceId + sensor list (name, resourceId)      │
│  │        ├─ Extract deviceId → derive topic                         │
│  │        └─ Build ShortIdToNameMap (resourceShortId → sensor name)  │
│  │                                                                   │
│  ├─ Subscribe: eco1j.weda.{deviceId}.telemetry  (derived at startup) │
│  │    ↓                                                              │
│  │ Process: Run proxy analysis (PHM Service inference)               │
│  │    ├─ Parse measures (sensorId = resourceShortId e.g. "4dbb8")    │
│  │    ├─ Map resourceShortId → sensor name → API field name          │
│  │    ├─ Call REST API → POST /api/v1/models/<ModelId>/infer         │
│  │    └─ Get healthScore (0.0-1.0, high=healthy)                     │
│  │                                                                   │
│  └─ Publish: eco1j.weda.{proxyId}.telemetry                          │
│      └─ Sensors:                                                     │
│         ├─ health_score       (healthScore from API: 0.87)           │
│         ├─ anomaly_result     (string: "anomaly_detected"/"normal")  │
│         ├─ processing_time_ms (latency: 45)                          │
│         └─ proxy_version      (string: "v1.2.3")                     │
│                                                                      │
│      ↓ (Auto-synced to Cloud)                                        │
│      Cloud Dashboard & Analytics                                     │
└──────────────────────────────────────────────────────────────────────┘
```

### 2.2 Design Rationale: Layered Architecture with PUB-SUB and External API

The **PUB-SUB** approach combined with **layered architecture** and **PHM Service REST API integration** provides:

1. **Complete Decoupling**: DAQ publishes, Proxy subscribes independently
   - No synchronous wait or timeout (NATS: async fire-and-forget)
   - DAQ unaffected by proxy availability
   - Proxy can start/stop without impacting DAQ
   - External API calls isolated in Transform layer

2. **Dynamic Feature Mapping via Device Capability Query**:
   - At startup, Proxy sends a NATS Request to `eco1j.weda.dm.subnode.cap.query.req` (no filter) and matches the target device by `deviceName` locally
   - The response contains the `deviceId` (used to derive the subscription topic `eco1j.weda.{deviceId}.telemetry`) and the full sensor list with `name` and `resourceId` per sensor
   - Standard telemetry messages use `sensorId = resourceShortId` (last 5 chars of the `resourceId` UUID, e.g. `resourceId "b73a9e41-...-65ce13c4dbb8"` → shortId `"4dbb8"`)
   - `DeviceCapabilityClient` extracts `resourceShortId → sensor name` pairs to build `ShortIdToNameMap` at runtime
   - `FeatureProxyDevice` then combines `ShortIdToNameMap` with the convention table to build `shortId → API field name` (e.g. `"4dbb8"` → `"x_axis_rms_mg"` → `"X-Axis_RMSmg"`)
   - Since `resourceShortId` values are device-specific UUID fragments, a static fallback map is not viable — capability query is required to start the subscription

3. **Standardized Architecture**: Follows SubNode framework patterns
   - **Communication Layer**: NATS subscription + message parsing
   - **Transform Layer**: ProxyAnalysisTransform implements `ITelemetryTransform` (same interface as `PhmFeatureTransform`)
   - **Device Layer**: Standard telemetry publishing via `SendTelemetryAsync`
   - **External API**: Health score computation via PHM Service REST endpoint

3. **Why `ITelemetryTransform` but NOT `EnqueueTelemetryAsync` pipeline**:
   `ProxyAnalysisTransform` implements `ITelemetryTransform` for interface consistency and testability,
   but the output measures are sent directly via `SendTelemetryAsync` rather than through
   `EnqueueTelemetryAsync`. This is intentional for two reasons:

   - **Validate stage incompatibility**: `EnqueueTelemetryAsync` validates each incoming measure's
     `ResourceId` against the device's registered sensors in `devicecfg.json`. The proxy device only
     registers its 4 output sensors (`health_score`, `anomaly_result`, etc.). The 10 incoming feature
     measures carry DAQ-side ResourceIds (`x_axis_rms_mg`, etc.) which are not registered — the
     validate stage would discard them before they ever reach the transform.

   - **Semantic mismatch**: The `EnqueueTelemetryAsync` pipeline is designed for a device's own sensor
     readings flowing toward the cloud. Feature measures are *external consumed inputs*, not sensor
     readings of the proxy device itself. Routing them through `EnqueueTelemetryAsync` would require
     registering 10 dummy feature sensors in `devicecfg.json` solely to pass validation — which is
     semantically incorrect and adds unnecessary configuration overhead.

4. **Cloud Integration**: Results automatically synchronized to cloud
   - Proxy sensors appear in cloud as any other device
   - Health scores (from PHM Service API) tracked over time for trend analysis
   - Standard telemetry output pipeline with API response latency transparency

### 2.3 Data Flow (Layered Architecture)

```
                ┌──────────────── STARTUP PHASE ───────────────────┐
                │                                                  │
                │  FeatureProxyDevice.OnAfterInitializeAsync       │
                │    ↓                                             │
                │  DeviceCapabilityClient                          │
                │    ├─ NATS Request:                              │
                │    │   eco1j.weda.dm.subnode.cap.query.req       │
                │    │   body: { "data": {} } (query all)          │
                │    ├─ Timeout: 10s (retry up to 3×)              │
                │    ├─ Match device by deviceName locally         │
                │    ├─ Extract deviceId                           │
                │    └─ Build ShortIdToNameMap                     │
                │        "4dbb8" → "x_axis_rms_mg"                 │
                │        "bcf5e" → "x_axis_peak_mg" (etc.)         │
                │                                                  │
                │  FeatureProxyDevice                              │
                │    ├─ Derive topic:                              │
                │    │   eco1j.weda.{deviceId}.telemetry           │
                │    └─ Build ShortIdToApiMap (shortId→API key)    │
                │        "4dbb8" → "X-Axis_RMSmg"                  │
                │        "bcf5e" → "X-Axis_Peakmg" (etc.)          │
                │                                                  │
                └──────────────────┬───────────────────────────────┘
                                   │ map + topic injected into
                                   ↓ FeatureSubscriptionHandler + ProxyAnalysisTransform
DAQ Device (daq-data-collector)
  Publish: eco1j.weda.{deviceId}.telemetry
    └─ Standard telemetry (sensorId = resourceShortId)

        ↓ NATS PUB-SUB

┌─ Feature Proxy SubNode (daq-feature-proxy) ─────────────┐
│                                                         │
│ COMMUNICATION LAYER                                     │
│ ├─ FeatureSubscriptionHandler                           │
│ │   ├─ Subscribe: eco1j.weda.{deviceId}.telemetry       │
│ │   ├─ Parse: JSON message deserialization              │
│ │   └─ Extract: measures array (sensorId=shortId)       │
│ │                 ↓                                     │
│ │   Forward to Transform                                │
│ │                                                       │
│ TRANSFORM LAYER                                         │
│ ├─ ProxyAnalysisTransform                               │
│ │   ├─ Input: measures keyed by resourceShortId         │
│ │   ├─ Process:                                         │
│ │   │   ├─ Map shortId → PHM Service names              │
│ │   │   │   (via ShortIdToApiMap built at startup)      │
│ │   │   ├─ POST /api/v1/models/<ModelId>/infer          │
│ │   │   │   └─ No authentication header required        │
│ │   │   │   └─ Timeout: 5000ms                          │
│ │   │   ├─ Receive healthScore (0.0-1.0) from API       │
│ │   │   └─ Handle: timeout → fallback (default 0.5)     │
│ │   └─ Output: 4 sensor measures                        │
│ │       ├─ health_score                                 │
│ │       ├─ anomaly_result                               │
│ │       ├─ processing_time_ms                           │
│ │       └─ proxy_version                                │
│ │                 ↓                                     │
│ DEVICE LAYER                                            │
│ ├─ SendTelemetryAsync                                   │
│ │   └─ Publish: eco1j.weda.{proxyId}.telemetry          │
│ │       └─ Standard SubNode sensor output               │
│                                                         │
└─────────────────────────────────────────────────────────┘
        ↓
    Cloud Dashboard & Analytics
```

---

## 3. Technical Specification

### 3.1 Configuration Schema

**Feature Proxy SubNode Configuration:**

| Parameter | Value | Description |
|-----------|-------|-------------|
| `ProxyEnabled` | `true` | Always enabled - proxy analysis always active |
| `DaqDataCollectorSubNodeName` | `"UniaxialVibrationDevice2"` | Device name of the DAQ Data Collector; used to match the capability response and derive the subscription topic |
| `AnalysisModel` | `"anomaly_detector_v1"` | Proxy analysis model identifier |
| `PhmAnomalyThreshold` | `0.3` | Health score below this value → "anomaly_detected" |
| `PhmApiEndpoint` | `"http://localhost:8000/api/v1/models"` | PHM Service base URL |
| `PhmApiModelId` | `"<ModelId>"` | PHM Service model UUID (must be in `ready` status) |
| `PhmApiAuthType` | `"None"` | No authentication required |
| `PhmApiTimeoutMs` | `5000` | API call timeout in milliseconds |
| `PhmApiFallbackValue` | `0.5` | Default health_score if API fails (neutral/unknown) |

> **Note on DaqDataCollectorSubNodeName**: Must match the `deviceName` field in the DAQ SubNode capability response. Use `eco1j.weda.dm.subnode.cap.query.req` (no filter) to list all SubNodes and confirm the exact name string. Device names must be unique within the SubNode system.

> **Note on SubscriptionSubject**: Not configurable. The subscription topic `eco1j.weda.{deviceId}.telemetry` is derived dynamically at startup from the capability query result. `deviceId` is obtained from the matching SubNode entry in the capability response.

> **Note on ModelId**: The `PhmApiModelId` must reference a PHM Service model that is in `ready` (trained) status. Use `GET /api/v1/models/` to list available models and `GET /api/v1/models/{modelId}/status` to verify status before configuring.

### 3.2 Implementation Details (Layered Architecture)

#### 3.2.0 Startup: Device Capability Client

**File:** `Communication/DeviceCapabilityClient.cs`

At startup, `FeatureProxyDevice.OnAfterInitializeAsync` calls `DeviceCapabilityClient.QueryCapabilityAsync` before starting the NATS subscription. The client sends a NATS Request/Reply to the WedaNode device manager subject and parses the response to extract the `deviceId` (used to derive the subscription topic) and the `resourceShortId → sensor name` map (used to identify incoming telemetry measures).

**NATS Request:**

```
Subject: eco1j.weda.dm.subnode.cap.query.req
Payload:
{
  "reqSeqId": "query-subnode-caps",
  "data": {}
}
```

> No `deviceIds` filter — all SubNodes are returned. The client matches by `deviceName` locally.

**NATS Response** (see `deviceCap.json` for full example):

```json
{
  "code": 0,
  "data": {
    "subNodes": [
      {
        "deviceId": "318281269764947968",
        "deviceName": "UniaxialVibrationDevice2",
        "capabilities": {
          "sensors": [
            { "name": "x_axis_rms_mg",    "resourceId": "b73a9e41-9d5a-5f3b-96e9-65ce13c4dbb8" },
            { "name": "x_axis_peak_mg",   "resourceId": "88913282-f68d-5f97-863b-1f77b99bcf5e" },
            { "name": "x_axis_oa_velocity","resourceId": "05fb2929-169b-5663-a835-fa2421ce247e" }
          ]
        }
      }
    ]
  }
}
```

> `resourceShortId` = last 5 chars of `resourceId` UUID. For example, `"b73a9e41-...-65ce13c4dbb8"` → shortId `"4dbb8"`. Standard telemetry messages use `sensorId = resourceShortId`.

**Return type:**

```csharp
public record DeviceCapability(
    string DeviceId,
    IReadOnlyDictionary<string, string> ShortIdToNameMap  // resourceShortId → sensor name
);
```

**Implementation:**

```csharp
// namespace daq_feature_proxy.Communication
public class DeviceCapabilityClient
{
    private const string CapQuerySubject = "eco1j.weda.dm.subnode.cap.query.req";
    private const int CapQueryTimeoutMs = 10000;

    private readonly NatsClient _natsClient;
    private readonly ILogger _logger;

    // Returns (deviceId, shortId→name map) for the target DAQ device.
    // Queries all SubNodes and matches by deviceName on the client side.
    // Retries up to 3× before returning null (caller must treat null as fatal — capability required).
    public async Task<DeviceCapability?> QueryCapabilityAsync(
        string daqDeviceName, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            reqSeqId = "query-subnode-caps",
            data = new { }
        });

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(CapQueryTimeoutMs);
            try
            {
                var reply = await _natsClient.RequestAsync<string, string>(
                    CapQuerySubject, payload, cancellationToken: cts.Token);

                if (reply.Data is null) continue;

                return ParseCapability(reply.Data, daqDeviceName);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Capability query attempt {Attempt} timed out for device '{DeviceName}'",
                    attempt + 1, daqDeviceName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Capability query attempt {Attempt} failed for device '{DeviceName}'",
                    attempt + 1, daqDeviceName);
            }
        }

        _logger.LogError(
            "Could not retrieve DAQ device capabilities after 3 attempts for '{DeviceName}' — proxy will not start",
            daqDeviceName);
        return null;
    }

    private static DeviceCapability? ParseCapability(string json, string deviceName)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
        if (!data.TryGetProperty("subNodes", out var subNodes)) return null;

        foreach (var node in subNodes.EnumerateArray())
        {
            if (!node.TryGetProperty("deviceName", out var dn) || dn.GetString() != deviceName) continue;
            if (!node.TryGetProperty("deviceId", out var did)) return null;

            var deviceId = did.GetString() ?? string.Empty;
            if (!node.TryGetProperty("capabilities", out var caps)) return null;
            if (!caps.TryGetProperty("sensors", out var sensors)) return null;

            var shortIdToName = sensors.EnumerateArray()
                .Where(s => s.TryGetProperty("sensorGroup", out var g) && g.GetString() != "SYS")
                .Select(s =>
                {
                    s.TryGetProperty("name", out var n);
                    s.TryGetProperty("resourceId", out var r);
                    var name = n.GetString();
                    var resourceId = r.GetString();
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(resourceId)
                        || name == "daqraw_vibration_payload") return null;
                    var shortId = resourceId.Length >= 5
                        ? resourceId[^5..].Replace("-", "")  // last 5 non-hyphen chars
                        : null;
                    return shortId is not null ? (shortId, name) : ((string, string)?)null;
                })
                .Where(p => p.HasValue)
                .ToDictionary(p => p!.Value.Item1, p => p!.Value.Item2);

            return new DeviceCapability(deviceId, shortIdToName);
        }

        return null;
    }
}
```

> **resourceShortId extraction**: Standard telemetry uses the last 5 chars of the `resourceId` UUID (excluding hyphens). Given `"b73a9e41-9d5a-5f3b-96e9-65ce13c4dbb8"`, the last 5 chars of the UUID are `"4dbb8"` (from `"65ce13c4dbb8"`). The proxy matches incoming `sensorId` values against these shortIds to identify which sensor each telemetry measure corresponds to.

**Feature Name Convention — DAQ → PHM Service API Mapping:**

The PHM Service API uses Title-Case field names with axis prefix and unit suffix. The two-step mapping chain is:
1. `DeviceCapabilityClient` produces `ShortIdToNameMap`: `resourceShortId → sensor name` (e.g. `"4dbb8"` → `"x_axis_rms_mg"`)
2. `FeatureProxyDevice` applies the convention table to produce `ShortIdToApiMap`: `resourceShortId → PHM Service key` (e.g. `"4dbb8"` → `"X-Axis_RMSmg"`)
3. `ProxyAnalysisTransform` uses `ShortIdToApiMap` to build the inference request from incoming measures

**Convention table** (sensor name → PHM Service key, applied by `FeatureProxyDevice`):

| DAQ sensor name (from capability) | PHM Service key |
|-----------------------------------|-----------------|
| `x_axis_rms_mg`                   | `X-Axis_RMSmg`  |
| `x_axis_peak_mg`                  | `X-Axis_Peakmg` |
| `x_axis_oa_velocity`              | `X-Axis_OAVelocity` |
| `x_axis_skewness`                 | `X-Axis_Skewness` |
| `x_axis_kurtosis`                 | `X-Axis_Kurtosis` |
| `x_axis_crest_factor`             | `X-Axis_CrestFactor` |
| *(y/z axes follow same pattern)*  | *(Y-Axis_…, Z-Axis_…)* |

Sensors in the capability response that are **not** in the convention table are logged as warnings and skipped — they will not be forwarded to the inference API.

**Example `ShortIdToApiMap`** (combined result for `UniaxialVibrationDevice2`):

| `resourceShortId` (sensorId in telemetry) | Sensor name      | PHM Service key      |
|-------------------------------------------|------------------|----------------------|
| `4dbb8`                                   | `x_axis_rms_mg`  | `X-Axis_RMSmg`       |
| `bcf5e`                                   | `x_axis_peak_mg` | `X-Axis_Peakmg`      |
| `e247e`                                   | `x_axis_oa_velocity` | `X-Axis_OAVelocity` |
| `2c0bf`                                   | `x_axis_skewness` | `X-Axis_Skewness`   |
| `85e51`                                   | `x_axis_kurtosis` | `X-Axis_Kurtosis`   |
| `1d3a4`                                   | `x_axis_crest_factor` | `X-Axis_CrestFactor` |

#### 3.2.1 Communication Layer: Feature Subscription Handler

**File:** `Communication/Pipeline/FeatureSubscriptionHandler.cs`

```csharp
// namespace daq_feature_proxy.Communication.Pipeline
public class FeatureSubscriptionHandler
{
    public async Task StartAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Feature subscription started on '{Subject}'", _subject);

        await foreach (var msg in _natsClient.SubscribeAsync<string>(_subject, cancellationToken: ct))
        {
            if (string.IsNullOrEmpty(msg.Data)) continue;
            _ = ProcessMessageAsync(msg.Data, ct);   // fire-and-forget per message
        }
    }

    private async Task ProcessMessageAsync(string messageJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(messageJson);
        var root = doc.RootElement;
        var timestamp = root.GetProperty("timestamp").GetInt64();

        var features = new Dictionary<string, double>();
        foreach (var m in root.GetProperty("data").GetProperty("measures").EnumerateArray())
        {
            var sensorId = m.GetProperty("sensorId").GetString();
            if (!string.IsNullOrEmpty(sensorId))
                features[sensorId] = m.GetProperty("value").GetDouble();
        }

        var outputMeasures = await _transform.AnalyzeAsync(features, timestamp, ct);
        if (outputMeasures.Count > 0)
            await _sendTelemetry(outputMeasures, ct);
    }
}
```

#### 3.2.2 Transform Layer: Proxy Analysis Transform

**File:** `Communication/Pipeline/ProxyAnalysisTransform.cs`

> **Key Design Note**: The PHM Service API returns `healthScore` (0.0 = critical, 1.0 = healthy).
> The proxy outputs this value directly as `health_score`.
> Anomaly detection uses: `health_score < 0.3 → "anomaly_detected"`.

```csharp
// namespace daq_feature_proxy.Communication.Pipeline
// Does NOT implement ITelemetryTransform — input/output types differ from standard pipeline
public class ProxyAnalysisTransform
{
    // ShortIdToApiMap is built at startup from the DAQ device capability query.
    // Key: resourceShortId (5-char hex, e.g. "4dbb8"), Value: PHM Service feature key (Title-Case_Unit)
    // Capability query is required — no static fallback (shortIds are device-specific UUID fragments)
    private readonly IReadOnlyDictionary<string, string> _shortIdToApiMap;

    // Input: feature dict keyed by resourceShortId (e.g. "4dbb8" → 42.5)
    // Output: 4 TelemetryMeasures with ResourceIds from devicecfg.json sensors
    public async Task<List<TelemetryMeasure>> AnalyzeAsync(
        Dictionary<string, double> inputFeatures, long timestampMs, CancellationToken ct)
    {
        var apiFeatures = new Dictionary<string, double>();
        foreach (var (shortId, val) in inputFeatures)
            if (_shortIdToApiMap.TryGetValue(shortId, out var apiName))
                apiFeatures[apiName] = val;

        var healthScore = await CallInferenceApiAsync(apiFeatures, timestampMs, ct);
        var anomalyResult = healthScore < _anomalyThreshold ? "anomaly_detected" : "normal";
        var processingTimeMs = ...;

        return BuildOutputMeasures(healthScore, anomalyResult, processingTimeMs, timestampMs);
    }

    private async Task<double> CallInferenceApiAsync(...)
    {
        // POST {_apiBaseEndpoint}/{_modelId}/infer
        // Body: { "data": [{ "timestamp": "yyyy-MM-dd HH:mm:ss", "features": {...} }] }
        // On success: return doc.RootElement.GetProperty("data")[0].GetProperty("healthScore").GetDouble()
        // On timeout/error: return _fallbackValue (0.5)
    }
}
```

#### 3.2.3 Device Layer Integration

**File:** `Devices/FeatureProxyDevice.cs`

```csharp
// namespace daq_feature_proxy.Devices
public class FeatureProxyDevice : DeviceBase
{
    // Config read via configuration.Properties.TryGetValue(key, out var v)
    protected override async Task OnAfterInitializeAsync(CancellationToken ct)
    {
        var props = Configuration.Properties;
        var apiEndpoint    = ReadProp(props, "PhmApiEndpoint", "...");
        var modelId        = ReadProp(props, "PhmApiModelId", "");
        var daqDeviceName  = ReadProp(props, "DaqDataCollectorSubNodeName", "");
        // ... read PhmApiTimeoutMs, PhmApiFallbackValue, PhmAnomalyThreshold

        // sensor ResourceIds populated by framework before this call
        var sensorResourceIds = Configuration.Sensors
            .ToDictionary(s => s.Name, s => s.ResourceId);

        _natsClient = await InitializeNatsClientAsync(ct);   // reads systemcfg.json WedaNode

        // Step 1: Query all SubNodes and find the target device by name
        var capClient  = new DeviceCapabilityClient(_natsClient, _logger);
        var capability = await capClient.QueryCapabilityAsync(daqDeviceName, ct);

        if (capability is null)
        {
            _logger.LogError("Capability query failed — feature proxy will not start");
            await base.OnAfterInitializeAsync(ct);
            return;
        }

        // Step 2: Derive subscription topic and build shortId → API key map
        var subject      = $"eco1j.weda.{capability.DeviceId}.telemetry";
        var shortIdToApi = BuildShortIdToApiMap(capability.ShortIdToNameMap);

        _logger.LogInformation(
            "Capability resolved: DeviceId={DeviceId}, Topic={Topic}, {Count} feature sensors mapped",
            capability.DeviceId, subject, shortIdToApi.Count);

        // Step 3: Inject map into transform and start subscription
        var transform = new ProxyAnalysisTransform(_logger, apiEndpoint, modelId, shortIdToApi, ...);
        var handler   = new FeatureSubscriptionHandler(
            _natsClient, subject, transform, _logger,
            async (measures, token) => await SendTelemetryAsync(measures, token));

        _ = handler.StartAsync(ct);   // fire-and-forget background subscription
        await base.OnAfterInitializeAsync(ct);
    }

    // Converts shortId → sensor name map (from capability) to shortId → PHM Service key
    // using the axis-unit naming convention table (e.g. x_axis_rms_mg → X-Axis_RMSmg)
    private IReadOnlyDictionary<string, string> BuildShortIdToApiMap(
        IReadOnlyDictionary<string, string> shortIdToName)
    {
        var map = new Dictionary<string, string>();
        foreach (var (shortId, name) in shortIdToName)
        {
            if (FeatureConventionTable.TryGetValue(name, out var apiName))
                map[shortId] = apiName;
            else
                _logger.LogWarning(
                    "Sensor '{Name}' from capability response is not in the convention table; skipping", name);
        }
        return map;
    }

    // NullCommunication + NullParser inner classes satisfy DeviceBase constructor requirement
    // ReadTelemetryAsync / ReadSensorsForIntervalGroupAsync return empty (push-based device)
}
```

---

## 4. Configuration Examples

### 4.1 Feature Proxy SubNode Configuration

**devicecfg.json** (Feature Proxy device configuration):

```json
{
  "DeviceConfigs": {
    "FeatureProxyConfig": {
      "Enabled": true,
      "Dtdl": { "AutoGenEnabled": true },
      "Properties": {
        "DaqDataCollectorSubNodeName": "UniaxialVibrationDevice2",
        "PhmAnomalyThreshold": 0.3,
        "PhmApiEndpoint": "http://localhost:8000/api/v1/models",
        "PhmApiModelId": "<ModelId>",
        "PhmApiTimeoutMs": 5000,
        "PhmApiFallbackValue": 0.5
      },
      "Sensors": [
        { "Name": "health_score",       "SensorGroup": "PHM", "Report": { "Interval": 1000 } },
        { "Name": "anomaly_result",     "SensorGroup": "PHM", "Report": { "Interval": 1000 } },
        { "Name": "processing_time_ms", "SensorGroup": "PHM", "Report": { "Interval": 1000 } },
        { "Name": "proxy_version",      "SensorGroup": "PHM", "Report": { "Interval": 1000 } }
      ]
    }
  }
}
```

### 4.2 NATS Configuration

**systemcfg.json** (same NATS broker as DAQ):

```json
{
  "WedaNode": {
    "Url": "127.0.0.1:4224",
    "AuthStrategy": "UserPassword",
    "Username": "advantech_nats",
    "Password": "3671be64607240cbc2b95af99c9a3b28fb5f9aa3fbe51f501478f1a678e19d48"
  }
}
```

### 4.3 Hardcoded Behavior

- Proxy analysis always enabled - continuous subscription to telemetry
- Capability query subject is always `eco1j.weda.dm.subnode.cap.query.req` — not configurable
- Capability query timeout is always 10 000 ms with 3 retries — not configurable
- Subscription topic `eco1j.weda.{deviceId}.telemetry` is derived from the capability query result — not configurable
- Async processing - no timeouts or blocking
- Standard output - publishes to default telemetry topic

---

## 5. Data Format Specification

### 5.0 Startup: Device Capability Query (NATS Request/Reply)

**Subject:** `eco1j.weda.dm.subnode.cap.query.req`  
**Pattern:** NATS Request/Reply (synchronous at startup, before subscription begins)

**Request Payload:**

```json
{
  "reqSeqId": "query-subnode-caps",
  "data": {}
}
```

> No `deviceIds` filter — all SubNodes are returned. The client iterates `data.subNodes[]` and matches on `deviceName == DaqDataCollectorSubNodeName`.

**Reply Payload** (condensed — see `docs/deviceCap.json` for full example):

```json
{
  "code": 0,
  "message": "OK",
  "data": {
    "subNodes": [
      {
        "deviceId": "318281269764947968",
        "deviceName": "UniaxialVibrationDevice2",
        "capabilities": {
          "sensors": [
            { "name": "x_axis_rms_mg",    "resourceId": "b73a9e41-9d5a-5f3b-96e9-65ce13c4dbb8" },
            { "name": "x_axis_peak_mg",   "resourceId": "88913282-f68d-5f97-863b-1f77b99bcf5e" },
            { "name": "x_axis_oa_velocity","resourceId": "05fb2929-169b-5663-a835-fa2421ce247e" },
            { "name": "x_axis_skewness",  "resourceId": "f71ec1cb-6581-5599-be73-870cb102c0bf" },
            { "name": "x_axis_kurtosis",  "resourceId": "3436648a-d2a8-5ec3-b7ca-5be9a0485e51" },
            { "name": "x_axis_crest_factor","resourceId": "299f8747-f951-5004-987c-4d1cc891d3a4" }
          ]
        }
      }
    ]
  }
}
```

> **Parsing note**: `DeviceCapabilityClient` filters out system sensors (`timestamp_timestamp`, `device_time`, `daqraw_vibration_payload`) — only sensors in the `AI` group representing computed features are forwarded to the feature map builder.

> **Failure behaviour**: If all 3 retry attempts fail, `DeviceCapabilityClient` returns `null` and `FeatureProxyDevice` logs an error and aborts startup — the proxy will not run. `LogWarning` is emitted per attempt; `LogError` is emitted on total failure.

### 5.1 Input: Standard Telemetry Message (from DAQ)

**Subject:** `eco1j.weda.{deviceId}.telemetry`  (e.g. `eco1j.weda.318281269764947968.telemetry`)

The DAQ publishes standard SubNode telemetry where `sensorId = resourceShortId` (last 5 chars of `resourceId` UUID). The proxy receives this stream and extracts feature measures by matching `sensorId` against the `ShortIdToApiMap` built at startup. SYS-group sensors (`a58c1`, `4724b`) are ignored via the map (they are not in the convention table).

```json
{
  "seqId": 145,
  "timestamp": 1781577276562,
  "data": {
    "measures": [
      { "sensorId": "a58c1", "value": 1781577272853, "timestamp": 1781577274829 },
      { "sensorId": "4724b", "value": 1781577274829, "timestamp": 1781577274829 },
      { "sensorId": "4dbb8", "value": 1.44e-05, "timestamp": 1781577274829 },
      { "sensorId": "bcf5e", "value": 1.57e-05, "timestamp": 1781577274829 },
      { "sensorId": "55b0c", "value": 2.01e-08, "timestamp": 1781577274829 },
      { "sensorId": "e247e", "value": 1.37e-10, "timestamp": 1781577274829 },
      { "sensorId": "d7d22", "value": 2.34e-05, "timestamp": 1781577274829 },
      { "sensorId": "2c0bf", "value": -0.047,   "timestamp": 1781577274829 },
      { "sensorId": "85e51", "value": -1.203,   "timestamp": 1781577274829 },
      { "sensorId": "1d3a4", "value": 1.273,    "timestamp": 1781577274829 }
    ]
  }
}
```

> `sensorId` values `a58c1` and `4724b` are SYS-group sensors (`timestamp_timestamp`, `device_time`) — not in the convention table, silently skipped by the transform. The remaining 8 entries are AI-group vibration features.

### 5.2 PHM Service API Request (sent by Proxy)

**Endpoint:** `POST http://localhost:8000/api/v1/models/<ModelId>/infer`  
**Auth:** None

```json
{
  "data": [
    {
      "timestamp": "2024-06-04 16:00:00",
      "features": {
        "X-Axis_RMSmg": 42.5,
        "X-Axis_Peakmg": 85.3,
        "X-Axis_OAVelocity": 5.0,
        "X-Axis_Skewness": 7.0,
        "X-Axis_Kurtosis": 18.0,
        "X-Axis_CrestFactor": 2.01
      }
    }
  ]
}
```

> **Feature Name Mapping** (telemetry `sensorId` → PHM Service API, via `ShortIdToApiMap`):
>
> | `sensorId` in telemetry | Sensor name           | PHM Service Name (`features` key) |
> |-------------------------|-----------------------|-----------------------------------|
> | `4dbb8`                 | `x_axis_rms_mg`       | `X-Axis_RMSmg`                    |
> | `bcf5e`                 | `x_axis_peak_mg`      | `X-Axis_Peakmg`                   |
> | `e247e`                 | `x_axis_oa_velocity`  | `X-Axis_OAVelocity`               |
> | `2c0bf`                 | `x_axis_skewness`     | `X-Axis_Skewness`                 |
> | `85e51`                 | `x_axis_kurtosis`     | `X-Axis_Kurtosis`                 |
> | `1d3a4`                 | `x_axis_crest_factor` | `X-Axis_CrestFactor`              |
> | `y_axis_rms_mg`       | `Y-Axis_RMSmg`                    |
> | `z_axis_rms_mg`       | `Z-Axis_RMSmg`                    |
> | *(etc.)*              | *(same pattern)*                  |

### 5.3 PHM Service API Response

```json
{
  "data": [
    {
      "timestamp": "2024-06-04T16:00:00Z",
      "healthScore": 0.87,
      "isoHealthScore": 0.90,
      "aiHealthScore": 0.85,
      "contributingFactors": [
        {
          "feature": "X-Axis_RMSmg",
          "zScore": 1.2,
          "contribution": 0.35
        }
      ]
    }
  ]
}
```

> **Threshold**: `healthScore < 0.3 → "anomaly_detected"`, otherwise `"normal"`.

### 5.4 Output: Proxy Sensor Telemetry (to Cloud)

**Subject:** `eco1j.weda.{proxyId}.telemetry`

Feature Proxy outputs analysis results as standard telemetry sensors:

```json
{
  "deviceId": "{proxy-subnode-id}",
  "timestamp": 1717504800050,
  "data": {
    "measures": [
      {
        "sensorId": "health_score",
        "value": 0.87,
        "timestamp": 1717504800000
      },
      {
        "sensorId": "anomaly_result",
        "value": "normal",
        "timestamp": 1717504800000
      },
      {
        "sensorId": "processing_time_ms",
        "value": 45,
        "timestamp": 1717504800000
      },
      {
        "sensorId": "proxy_version",
        "value": "v1.2.3",
        "timestamp": 1717504800000
      }
    ]
  }
}
```

### 5.5 Output Field Descriptions

| Sensor | Type | Description |
|--------|------|-------------|
| `health_score` | double | PHM Service health score (0.0=critical, 1.0=healthy) |
| `anomaly_result` | string | `"anomaly_detected"` if `health_score < 0.3`, else `"normal"` |
| `processing_time_ms` | long | End-to-end processing time (includes API call) |
| `proxy_version` | string | Proxy service version used for analysis |

---

## 6. Feature Proxy SubNode Implementation Structure

### Folder Structure (Layered)

```
examples/daq-feature-proxy/
├── Program.cs                          (SubNode host)
├── devicecfg.json                      (Proxy config)
├── systemcfg.json                      (NATS settings)
├── Devices/
│   └── FeatureProxyDevice.cs           (Device layer - lifecycle, cap query wiring)
├── Communication/
│   ├── DeviceCapabilityClient.cs       (Startup: NATS Request/Reply cap query + map builder)
│   └── Pipeline/
│       ├── FeatureSubscriptionHandler.cs  (Communication layer)
│       └── ProxyAnalysisTransform.cs      (Transform / API layer)
└── docs/
    ├── deviceCap.json                  (Example capability response from DAQ)
    └── phm_vibration_feature_proxy_proposal.md
```

### Layer Responsibilities

#### Startup / Capability Layer
- **DeviceCapabilityClient** (`Communication/`)
  - Called once during `OnAfterInitializeAsync`, before the subscription loop starts
  - Sends NATS Request to `eco1j.weda.dm.subnode.cap.query.req` with empty `data: {}` (no filter)
  - Iterates `data.subNodes[]`, matches by `deviceName == DaqDataCollectorSubNodeName`
  - Extracts `deviceId` and AI-group sensor `name` + `resourceId` (excludes SYS-group and `daqraw_vibration_payload`)
  - Computes `resourceShortId` = last 5 chars of `resourceId` UUID
  - Returns `DeviceCapability(DeviceId, ShortIdToNameMap)` or `null` on total failure (3× 10 s)
  - `null` result signals `FeatureProxyDevice` to abort startup (no static fallback — shortIds are device-specific)

#### Communication Layer
- **FeatureSubscriptionHandler** (`Communication/Pipeline/`)
  - Subscribe to `eco1j.weda.{deviceId}.telemetry` (topic derived from capability query result) via `await foreach` (NATS.Net)
  - Parse JSON → `Dictionary<string, double>` (keyed by `resourceShortId`)
  - Fire-and-forget per message; calls `ProxyAnalysisTransform.AnalyzeAsync`
  - Returns results via `SendTelemetryAsync` callback delegate

#### Transform / API Layer
- **ProxyAnalysisTransform** (`Communication/Pipeline/`)
  - Does **not** implement `ITelemetryTransform` (input is `Dictionary<string,double>`, not `List<TelemetryMeasure>`)
  - Input: feature dict keyed by `resourceShortId` (e.g. `"4dbb8"` → 42.5)
  - Map: `shortId` → PHM Service names via `_shortIdToApiMap` (built from capability query — no static fallback)
  - Process: `POST /api/v1/models/{modelId}/infer`, extract `data[0].healthScore`
  - Output: `List<TelemetryMeasure>` with ResourceIds from `devicecfg.json` sensors
  - Fallback: returns `PhmApiFallbackValue` on timeout / connection error

#### Device Layer
- **FeatureProxyDevice** (`Devices/`)
  - Inherits `DeviceBase` with `NullCommunication` + `NullParser` inner classes
  - `ReadTelemetryAsync` / `ReadSensorsForIntervalGroupAsync`: return empty (push-based)
  - `OnAfterInitializeAsync`: reads config, calls `DeviceCapabilityClient`, derives topic, builds `ShortIdToApiMap`, starts NATS subscription
  - Calls `SendTelemetryAsync` to deliver results to the standard telemetry pipeline

---

## 7. Implementation Phases

### Phase 1: Layered Architecture Implementation (Week 1)

**Communication Layer:**
- [x] Create `Communication/Pipeline/FeatureSubscriptionHandler.cs`
- [x] Implement NATS subscription to `phm.vibration.windowed` via `await foreach`
- [x] Implement JSON message parsing → `Dictionary<string, double>`

**Transform / API Layer:**
- [x] Create `Communication/Pipeline/ProxyAnalysisTransform.cs`
- [x] Implement DAQ→API feature name mapping dictionary (static fallback)
- [x] Implement `CallInferenceApiAsync` for PHM Service `/infer` endpoint
- [x] Implement fallback (returns `PhmApiFallbackValue`) on timeout / error

**Device Layer:**
- [x] Create `Devices/FeatureProxyDevice.cs` (inherits `DeviceBase`)
- [x] Initialize NATS client from `systemcfg.json` (WedaNode section)
- [x] Wire subscription → transform → `SendTelemetryAsync` via callback delegate
- [x] `dotnet build` passes with 0 errors

### Phase 2: Dynamic Device Capability Discovery (Week 2)

**Startup / Capability Layer:**
- [x] Create `Communication/DeviceCapabilityClient.cs`
- [x] Implement NATS Request/Reply to `eco1j.weda.dm.subnode.cap.query.req`
- [x] Implement retry logic (3× with 10 s timeout)
- [x] Add `DaqDataCollectorSubNodeName` to `devicecfg.json`
- [x] Refactor `ProxyAnalysisTransform` to accept injected feature map
- [x] Update `FeatureProxyDevice.OnAfterInitializeAsync` to call `DeviceCapabilityClient`
- [x] Add `BuildFeatureMapFromCapability` convention-table method to `FeatureProxyDevice`

**Phase 2 → Phase 3: Dynamic Subscription Topic (v5.0)**
- [ ] Refactor `DeviceCapabilityClient` to return `DeviceCapability(DeviceId, ShortIdToNameMap)` instead of `IReadOnlyList<string>`
- [ ] Add `DeviceCapability` record to `Communication/`
- [ ] Update JSON parsing to extract `deviceId` and `resourceShortId` per sensor (last 5 chars of `resourceId` UUID)
- [ ] Change capability query failure from "use StaticFallbackMap" to "abort startup"
- [ ] Remove `StaticFallbackMap` from `ProxyAnalysisTransform`
- [ ] Rename `_daqToApiFeatureMap` → `_shortIdToApiMap` in `ProxyAnalysisTransform`
- [ ] Update `FeatureProxyDevice`: rename `BuildFeatureMapFromCapability` → `BuildShortIdToApiMap`; accept `ShortIdToNameMap` input
- [ ] Derive subscription topic `eco1j.weda.{capability.DeviceId}.telemetry` in `FeatureProxyDevice`
- [ ] Remove `SubscriptionSubject` from `devicecfg.json`
- [ ] `dotnet build` passes with 0 errors

**Remaining:**
- [ ] Set `PhmApiModelId` in `devicecfg.json` to a trained model UUID
- [ ] Integration test with live DAQ + PHM Service
- [ ] Verify PHM Service model is in `ready` status: `GET /api/v1/models/`
- [ ] Verify capability query returns correct sensor list and topic at startup (check logs)

---

## 8. Error Handling & Resilience

### 8.1 Error Cases

1. **Startup - Device Capability Query Failure**
   - NATS broker unreachable at startup, or DAQ device not yet registered
   - Request timeout (no responder), or response missing expected device name / `deviceId`
   - **Handling**: Retry up to 3× (10 s each); on total failure LogError and abort startup — proxy will not start
   - **Why no fallback**: `resourceShortId` values are device-specific UUID fragments; a static fallback map cannot know the correct shortId-to-name mapping for the actual device in use

2. **Startup - Capability Response Contains Unknown Sensor Names**
   - DAQ device registers sensors not present in the convention table
   - **Handling**: LogWarning per unknown sensor name; skip — only mapped sensors forwarded to inference API
   - **Impact**: No impact on other features that are successfully mapped; proxy starts with partial map

3. **Communication Layer - NATS Connection Failure**
   - Connection lost to NATS broker
   - **Handling**: Reconnect automatically (NatsClient built-in), LogError

4. **Communication Layer - Malformed Feature Message**
   - Missing required fields (deviceId, timestamp, data)
   - Invalid JSON format
   - **Handling**: LogError, skip message, continue to next

5. **Transform Layer - Feature Name Mapping Miss**
   - DAQ sensor name not found in `_daqToApiFeatureMap` (neither dynamic nor static fallback)
   - **Handling**: LogWarning, skip that feature, submit remaining mapped features

6. **Transform Layer - PHM Service API Connection Failure**
   - Cannot connect to `http://localhost:8000`
   - Network unreachable, DNS resolution failure
   - **Handling**: LogWarning, use fallback health_score (0.5), continue

7. **Transform Layer - PHM Service API Timeout**
   - API does not respond within 5000ms
   - Network latency too high
   - **Handling**: LogWarning, return fallback health_score (0.5), continue
   - **Never block**: Process next message immediately

8. **Transform Layer - PHM Service API Returns Error**
   - API returns HTTP 5xx (server error)
   - API returns HTTP 4xx (model not found or not trained)
   - Invalid response format (missing `data[0].healthScore` field)
   - **Handling**: LogWarning, use fallback health_score (0.5), continue

9. **Transform Layer - Model Not Ready**
   - PHM Service model status is not `ready` (e.g., `untrained`, `training`, `failed`)
   - **Handling**: API returns 4xx, LogWarning with model status, use fallback

10. **Device Layer - Telemetry Publish Failure**
    - Cloud sync connection issues
    - **Handling**: LogError, continue processing next message

### 8.2 Logging Strategy

- **INFO**: Startup capability query succeeded; `DeviceId`, derived topic, number of sensors mapped
- **INFO**: Successful proxy analysis with health_score and anomaly_result
- **WARNING**: Capability query retry/timeout (per attempt)
- **WARNING**: Unknown sensor name from capability response (not in convention table)
- **WARNING**: Feature mapping misses, API timeouts, fallback used, model not ready
- **ERROR**: Capability query total failure — proxy startup aborted
- **ERROR**: NATS connection failures, subscription errors, publish failures
- **DEBUG**: Capability query request/response details, feature message details, API request/response details (optional)

### 8.3 Resilience Behavior

```
[STARTUP — runs once before subscription begins]
Device Capability Query (DeviceCapabilityClient)
  ├─ Success: DeviceCapability(DeviceId, ShortIdToNameMap) → derive topic + build ShortIdToApiMap → inject into transform
  └─ Total failure (3 retries): LogError → abort startup (proxy will not run)
  ↓
Subscription Started (FeatureSubscriptionHandler) on eco1j.weda.{deviceId}.telemetry

[RUNTIME — per received message]
Feature Message Received (Communication Layer)
  ↓
Parse & Extract (may fail)
  ├─ Valid: Forward to Transform
  └─ Invalid: LogError → Skip
  ↓
Feature Name Mapping (partial failure OK)
  ├─ Mapped features: Forward to API
  └─ Unmapped: LogWarning → Skip that feature
  ↓
PHM Service API Call (may fail)
  ├─ Success: healthScore → output as health_score
  └─ Timeout/Error: LogWarning → Return fallback (0.5)
  ↓
Publish Telemetry (may fail)
  ├─ Success: Output to cloud
  └─ Error: LogError → Continue
  ↓
Process Next Message (non-blocking)
```

Key: **Capability failure aborts startup cleanly; runtime failures don't block subsequent messages**

---

## 9. Performance Considerations

### 9.1 Overhead Analysis (by Layer)

| Layer | Operation | Overhead | Notes |
|-------|-----------|----------|-------|
| **Communication** | NATS subscription + message read | < 1ms | Async, background |
| **Communication** | JSON deserialization | < 1ms | Fixed message size |
| **Transform** | Feature name mapping | < 1ms | Dictionary lookup |
| **Transform** | PHM Service API call | ~50-500ms (typical) | Network + ML inference |
| **Transform** | Response parsing | < 1ms | Extract healthScore from JSON |
| **Transform** | Sensor output generation | < 1ms | 4 sensors always |
| **Device** | Telemetry publish to cloud | < 1ms | Standard output |
| **Total per cycle** | End-to-end latency (normal) | ~55-510ms | Async, non-blocking |
| **Total per cycle** | End-to-end latency (API timeout) | 5000ms + fallback | Resilient fallback |

### 9.2 Scalability

- **Feature publishing rate**: 1 per second (1000ms observation window)
- **Processing latency**: < 5000ms leaves buffer before next message arrives
- **NATS throughput**: Can handle 1000s of messages/second
- **PHM Service throughput**: Limited by `http://localhost:8000` capacity
  - At 1 call/second with 5000ms timeout, async client can have multiple requests in flight
- **Memory**: Minimal - processes one message at a time per layer
- **Multiple instances**: Multiple proxy instances can subscribe independently
  - Each instance calls PHM Service independently
  - PHM Service must handle concurrent callers

---

## 10. Security Considerations

### 10.1 Authentication

**NATS Authentication:**
- Same NATS credentials as other SubNodes
- Shared WedaNode NATS connection

**PHM Service API Authentication:**
- **No authentication required** - PHM Service (`http://localhost:8000`) is an open internal API
- Service is deployed on internal network (172.16.x.x)
- No `Authorization` header needed
- No credentials to manage or rotate

### 10.2 Authorization

**NATS Authorization:**
- Feature Proxy requests on: `eco1j.weda.dm.subnode.cap.query.req` (startup only)
- Feature Proxy subscribes to: `eco1j.weda.{deviceId}.telemetry` (derived at startup from capability)
- Feature Proxy publishes to: `eco1j.weda.{proxyId}.telemetry` (standard)
- Can be restricted via NATS ACLs

**PHM Service Authorization:**
- No explicit authorization - network-level access control via 172.16.x.x subnet
- Ensure PHM Service host is only accessible from trusted internal hosts

### 10.3 Data Sensitivity

- Input: Vibration features (non-PII)
- Output: Health scores and version info (non-sensitive)
- Cloud sync: Standard telemetry encryption
- **API Communication**: Internal HTTP (172.16.x.x) - acceptable for internal services
  - If PHM Service moves to public network, switch to HTTPS

---

## 11. Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Feature Message Processing Rate** | > 99% | Messages processed / messages received |
| **Average Analysis Latency** | < 5050ms | Processing time (end-to-end, including API call) |
| **API Call Success Rate** | > 95% | Successful API responses / total attempts |
| **API Call Latency (p95)** | < 5000ms | 95th percentile response time from PHM Service |
| **Fallback Usage Rate** | < 5% | API failures using fallback health_score / total |
| **Error Rate** | < 1% | Failed analyses / total attempts |
| **Uptime** | > 99.9% | Continuous operation without crashes |
| **Cloud Sync Success** | 100% | Sensor outputs reaching cloud |
| **Independent Operation** | 100% | Proxy failures don't affect DAQ |
| **PHM Service Resilience** | 100% | Proxy continues even if PHM Service is unavailable |

---

## 12. Rollback Plan

If feature proxy causes issues:

1. **Stop proxy service**: Kill daq-feature-proxy process
   - DAQ continues publishing standard telemetry (proxy no longer subscribed)
   - No impact on main DAQ data collection or telemetry
2. **If PHM Service is unavailable**:
   - Proxy automatically uses fallback health_score (0.5)
   - Continues processing and publishing results
   - No need to restart proxy
3. **Disable proxy without stopping service**:
   - Set `ProxyEnabled = false` in devicecfg.json
   - Update running configuration
4. **Disable API calls without stopping proxy**:
   - Set `PhmApiEndpoint` to empty or null
   - Proxy will skip API calls and use fallback value
   - Continue processing messages
5. **Switch model version**:
   - Update `PhmApiModelId` to a different trained model UUID
   - Use `GET /api/v1/models/` to list available trained models

---

## 13. Dependencies

### Required NuGet Packages

- `NATS.Net` (PUB-SUB subscription, already in `Weda.SubNode.Host` transitive)
- `System.Text.Json` (JSON message parsing and API serialization)
- Weda.SubNode framework libraries (`Weda.SubNode.Host` project reference)

### Framework Components

- `DeviceBase` (Device layer base class — `Weda.SubNode.Core`)
- `IWedaApplicationContext` (app context — `Weda.SubNode.Abstractions`)
- `TelemetryMeasure` (sensor output format — `Weda.SubNode.Abstractions`)
- `ICommunication` / `IProtocolParserCore` (satisfied by `NullCommunication`/`NullParser` inner classes)

### External Service Dependencies

- **NATS Broker** (shared with DAQ)
  - Startup request: `eco1j.weda.dm.subnode.cap.query.req` (device capability discovery)
  - Input subscription: `eco1j.weda.{deviceId}.telemetry` (derived from capability; published by DAQ)
  - Output publish: `eco1j.weda.{proxyId}.telemetry` (standard telemetry)
- **DAQ Data Collector** (telemetry publisher and capability responder)
  - Provides continuous standard telemetry stream (`sensorId = resourceShortId`)
  - Responds to `eco1j.weda.dm.subnode.cap.query.req` with `deviceId` + sensor list at startup
- **PHM Service** (external ML inference)
  - Base URL: `http://localhost:8000`
  - Infer Endpoint: `POST /api/v1/models/{modelId}/infer`
  - Auth: None
  - Request format: `InferRequest` with `data[]` array (timestamp + features dict)
  - Response format: `InferResponse` with `data[0].healthScore` (0.0-1.0)
  - Expected latency: < 5000ms (p95)
  - Fallback: On failure, use default health_score 0.5 (neutral/unknown state)

### Communication vs Transform Layer Dependencies

**Startup / Capability Layer (DeviceCapabilityClient):**
- `NatsClient` - NATS Request/Reply (reuses same client as subscription)
- `System.Text.Json` - JSON parsing of capability response
- `ILogger` - Logging (retry warnings, startup abort on failure)
- Called by: `FeatureProxyDevice.OnAfterInitializeAsync`
- Returns: `DeviceCapability(DeviceId, ShortIdToNameMap)` → consumed by `FeatureProxyDevice.BuildShortIdToApiMap`

**Communication Layer (FeatureSubscriptionHandler):**
- `NatsClient` - NATS connection management
- `System.Text.Json` - JSON parsing
- `ILogger` - Logging
- Forwards to: `ProxyAnalysisTransform`

**Transform Layer (ProxyAnalysisTransform):**
- Does NOT implement `ITelemetryTransform` (input is `Dictionary<string,double>`, not `List<TelemetryMeasure>`)
- `HttpClient` - for REST API calls to PHM Service
- `_shortIdToApiMap` - injected at construction from capability query result (no static fallback)
- Fallback handling - use 0.5 on API failure
- No direct dependencies on NatsClient (communication abstracted)
- Testable independently with mock HttpClient and a pre-built shortIdToApiMap fixture

---

## 14. Future Enhancements

### Phase 2: Advanced Analysis Features

1. **Multiple Analysis Models**:
   - Support different PHM Service models (by machine type or sensor config)
   - Make ProxyAnalysisTransform swappable
   - Factory pattern for model selection

2. **Model Versioning**:
   - Support multiple model versions
   - Track active model in `proxy_version` sensor
   - Auto-detect model status changes

3. **Threshold-Based Alerting**:
   - Auto-alert when `health_score < PhmAnomalyThreshold`
   - Integrate with alert service

### Phase 3: Advanced Transform Features

4. **Batch Analysis**:
   - Accumulate multiple feature sets before analysis
   - Improve efficiency for high-frequency scenarios

5. **Response Caching**:
   - Cache health scores for similar feature vectors
   - Reduce API call frequency

6. **Extended Output Sensors**:
   - Expose `isoHealthScore` and `aiHealthScore` separately
   - Surface `contributingFactors` as individual sensor readings

### Architectural Improvements

7. **Layer Abstraction**:
   - Abstract `IFeatureSubscription` interface
   - Support multiple communication backends (not just NATS)
   - Mock for testing

8. **Transform Pipeline**:
   - Chain multiple transforms (feature normalization before proxy analysis)
   - Unit conversion transform for `OAVelocity` (mm/s requirement)

---

## 15. Appendix

### A. PHM Service API Summary

**Base URL:** `http://localhost:8000`  
**Auth:** None  
**Key Endpoint for Proxy:**

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/v1/models/{modelId}/infer` | Run health score inference |
| `GET` | `/api/v1/models/` | List all models (find trained model IDs) |
| `GET` | `/api/v1/models/{modelId}/status` | Check if model is `ready` before use |
| `GET` | `/api/v1/healthz` | PHM Service health check |

### B. File Locations (Layered Architecture)

```
/home/advantech/vincent/edge_subnode/
├── examples/daq-data-collector/
│   ├── Devices/UniaxialVibrationDevice.cs        (publishes standard telemetry + responds to cap query)
│   ├── docs/
│   │   └── phm_window_aligned_streaming_proposal.md
│   └── ...
│
├── examples/daq-feature-proxy/                    [Feature Proxy SubNode]
│   ├── Program.cs                                 (SubNode host)
│   ├── devicecfg.json                             (proxy config, includes DaqDataCollectorSubNodeName)
│   ├── systemcfg.json                             (NATS settings)
│   ├── Devices/
│   │   └── FeatureProxyDevice.cs                  (Device layer, wires cap query → transform)
│   ├── Communication/
│   │   ├── DeviceCapabilityClient.cs              (Startup: NATS cap query + map builder)
│   │   └── Pipeline/
│   │       ├── FeatureSubscriptionHandler.cs      (Communication layer)
│   │       └── ProxyAnalysisTransform.cs          (Transform / API layer)
│   └── docs/
│       ├── deviceCap.json                         (Example DAQ capability response)
│       ├── TestPubSub.cs                          (NATS Request/Reply test reference)
│       └── phm_vibration_feature_proxy_proposal.md (this file)
└── ...
```

### C. References & Related Architecture

**Architectural Patterns:**
- [PhmFeatureTransform](../../../daq-data-collector/Communication/Pipeline/PhmFeatureTransform.cs) - Similar Transform layer pattern
- [ITelemetryTransform Interface](../../../docs/api/ITelemetryTransform.md) - Base class for transforms

**PHM Service API:**
- Swagger UI: `http://localhost:8000/docs`
- OpenAPI spec: `http://localhost:8000/openapi.json`

**NATS & Communication:**
- [NATS PUB-SUB Documentation](https://docs.nats.io/nats-concepts/core-nats/pubsub)
- [NATS.Net Subscribe API](https://github.com/nats-io/nats.net.v2)

**Related Proposals:**
- [Window-Aligned Streaming Proposal](../daq-data-collector/docs/phm_window_aligned_streaming_proposal.md) - Feature source
- [Weda SubNode Framework](../../../docs/subnode-framework.md) - Layered architecture patterns

**Testing Approach:**
- Unit test ProxyAnalysisTransform independently with mock HttpClient
- Verify feature name mapping covers all DAQ-published sensors
- Integration test Communication layer with mock NATS
- End-to-end test: confirm `health_score` correctly reflects PHM Service response

### D. Approval & Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Author | Development Team | 2026-06-09 | |
| Revised | Development Team | 2026-06-10 | |
| Revised | Development Team | 2026-06-15 | |
| Revisor | Technical Lead | | |
| Approver | Product Manager | | |

---

**End of Document**
