# Output Sensors and Feature Mapping Reference

The Feature Proxy publishes **4 output sensors** to the cloud as standard SubNode telemetry. These sensors represent the results of calling the PHM Inference Service with features received from `daq-data-collector`.

---

## Output Sensor Definitions

Sensors are declared in `devicecfg.json` under `DeviceConfigs.FeatureProxyConfig.Sensors`.

| Sensor Name | Data Type | Description |
|---|---|---|
| `health_score` | double | Overall machine health score from PHM Inference Service (0.0 = critical, 1.0 = healthy, -1 = API unavailable) |
| `anomaly_result` | string | Anomaly classification based on health score threshold (see below) |
| `processing_time_ms` | long | End-to-end PHM API inference latency in milliseconds |
| `proxy_version` | string | Feature proxy software version (e.g., `"v1.0.0"`) |

### Configuration Example

```json
{
  "Sensors": [
    {
      "Name": "health_score",
      "SensorGroup": "AI",
      "SensorInfo": {
        "DisplayName": "Health Score",
        "Description": "Overall machine health score (0.0=critical, 1.0=healthy)",
        "Schema": "double"
      },
      "Report": { "Enabled": true, "Interval": 1000 }
    },
    {
      "Name": "anomaly_result",
      "SensorGroup": "AI",
      "SensorInfo": {
        "DisplayName": "Anomaly Result",
        "Description": "Anomaly classification result: normal or anomaly_detected",
        "Schema": "string"
      },
      "Report": { "Enabled": true, "Interval": 1000 }
    },
    {
      "Name": "processing_time_ms",
      "SensorGroup": "AI",
      "SensorInfo": {
        "DisplayName": "Processing Time (ms)",
        "Description": "End-to-end PHM API inference latency in milliseconds",
        "Schema": "long"
      },
      "Report": { "Enabled": true, "Interval": 1000 }
    },
    {
      "Name": "proxy_version",
      "SensorGroup": "AI",
      "SensorInfo": {
        "DisplayName": "Proxy Version",
        "Description": "Feature proxy software version",
        "Schema": "string"
      },
      "Report": { "Enabled": true, "Interval": 1000 }
    }
  ]
}
```

---

## Anomaly Detection Logic

`anomaly_result` is derived from `health_score` using the following rules:

```
health_score < 0                       →  "unavailable"       (API failure / timeout)
health_score < PhmAnomalyThreshold     →  "anomaly_detected"
health_score ≥ PhmAnomalyThreshold     →  "normal"
```

`PhmAnomalyThreshold` is configurable in `devicecfg.json` (default: `0.3`).

When the PHM Inference Service is unavailable (timeout, connection error, or HTTP error), the proxy returns `health_score = -1`, which is outside the valid `[0.0, 1.0]` range. This distinguishes service unavailability from a genuine low health score and prevents false anomaly alerts.

---

## Feature Convention Table

The proxy maps DAQ sensor names to PHM Inference Service field names using a built-in convention table. Sensors reported by the DAQ that are **not** in this table are silently skipped (a warning is logged at startup).

| DAQ Sensor Name | PHM Service Key |
|---|---|
| `x_axis_rms_mg` | `X-Axis_RMSmg` |
| `x_axis_peak_mg` | `X-Axis_Peakmg` |
| `x_axis_oa_velocity` | `X-Axis_OAVelocity` |
| `x_axis_deviation` | `X-Axis_Deviation` |
| `x_axis_skewness` | `X-Axis_Skewness` |
| `x_axis_kurtosis` | `X-Axis_Kurtosis` |
| `x_axis_crest_factor` | `X-Axis_CrestFactor` |
| `y_axis_rms_mg` | `Y-Axis_RMSmg` |
| `y_axis_peak_mg` | `Y-Axis_Peakmg` |
| `y_axis_oa_velocity` | `Y-Axis_OAVelocity` |
| `y_axis_deviation` | `Y-Axis_Deviation` |
| `y_axis_skewness` | `Y-Axis_Skewness` |
| `y_axis_kurtosis` | `Y-Axis_Kurtosis` |
| `y_axis_crest_factor` | `Y-Axis_CrestFactor` |
| `z_axis_rms_mg` | `Z-Axis_RMSmg` |
| `z_axis_peak_mg` | `Z-Axis_Peakmg` |
| `z_axis_oa_velocity` | `Z-Axis_OAVelocity` |
| `z_axis_deviation` | `Z-Axis_Deviation` |
| `z_axis_skewness` | `Z-Axis_Skewness` |
| `z_axis_kurtosis` | `Z-Axis_Kurtosis` |
| `z_axis_crest_factor` | `Z-Axis_CrestFactor` |

> **Note**: `x_axis_peak_to_peak_displacement` and `x_axis_deviation` (when sensor name is `x_axis_deviation`) are excluded if not present in the table. Only features that both the DAQ publishes AND appear in the convention table are forwarded to the PHM Inference Service.

---

## Feature Mapping Chain

The two-step mapping is performed at **startup** and cached for the lifetime of the container.

**Step 1 — Capability Query** (`DeviceCapabilityClient`):

At startup, the proxy sends a NATS Request to the WEDA Node device manager and matches the DAQ device by name. The response provides:
- `deviceId` — used to derive the subscription topic `eco1j.weda.{deviceId}.telemetry`
- Per-sensor `resourceId` — the last 5 characters of each sensor's UUID become the `resourceShortId` (e.g., UUID `b73a9e41-...-65ce13c4dbb8` → shortId `4dbb8`)

**Step 2 — Convention Table** (`FeatureProxyDevice`):

The proxy combines the `resourceShortId → sensor name` map with the convention table above to build `resourceShortId → PHM Service key` (e.g., `4dbb8` → `X-Axis_RMSmg`).

At runtime, each incoming telemetry measure's `sensorId` (which equals `resourceShortId`) is looked up in this map. Measures not found in the map are silently skipped.

---

## Configuration Parameters Reference

| Parameter | Location | Required | Default | Description |
|---|---|---|---|---|
| `DaqDataCollectorSubNodeName` | `devicecfg.json` Properties | **Yes** | — | Must exactly match `SubNode.Name` in the DAQ's `devicecfg.json` |
| `PhmApiModelId` | `devicecfg.json` Properties | **Yes** | — | UUID of a PHM model in `ready` status |
| `PhmApiEndpoint` | `devicecfg.json` Properties | No | `http://172.16.9.17:8000/api/v1/models` | PHM Inference Service base URL |
| `PhmApiTimeoutMs` | `devicecfg.json` Properties | No | `5000` | API call timeout in milliseconds |
| `PhmAnomalyThreshold` | `devicecfg.json` Properties | No | `0.3` | Health score threshold below which anomaly is declared |

---

## Troubleshooting

### Zero Features Mapped at Startup

**Log message**:
```
[INF] Capability resolved: ..., 0 feature sensors mapped
```

**Cause**: The DAQ device reports sensors whose names do not appear in the convention table.

**Action**: Cross-check the sensor names reported by the DAQ's capability response against the convention table above. Sensor names must be exact snake_case matches (e.g., `x_axis_rms_mg`, not `XAxisRMSmg`).

### Specific Features Missing from PHM Request

**Log message**:
```
[WRN] Sensor 'x_axis_peak_to_peak_displacement' (shortId=55b0c) is not in the convention table; skipping
```

**Cause**: The DAQ reports a feature that is not accepted by the configured PHM model.

**Action**: This is expected behaviour for features outside the convention table. No action required unless the PHM model explicitly requires this feature.

### health_score Always -1

**Cause**: PHM Inference Service is unreachable or returning errors for every request.

**Action**: Verify PHM Service health: `curl http://<phm-host>:8000/api/v1/healthz`
