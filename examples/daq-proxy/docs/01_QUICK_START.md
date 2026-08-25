# Quick Start Guide

## Prerequisites

- Device has Docker Compose installed
- Network connectivity to NATS server (same broker as `daq-collector`)
- `daq-collector` is running and registered with the WEDA Node
- PHM Inference Service is running and has a model in `ready` status

## Quick Start

### 1. Prepare Files

Copy the following file templates and upload to the device directory:
- `docker-compose.yml`
- `appsettings.json`
- `systemcfg.json`
- `devicecfg.json`
- `customcfg.json`

For example, using `/opt/daq-feature-proxy`:
```bash
mkdir -p /opt/daq-feature-proxy
cd /opt/daq-feature-proxy
# Upload files via scp, rsync, or other method
```

### 2. Edit Configuration

#### systemcfg.json — WEDA Node Connection

```json
{
  "WedaNode": {
    "Url": "192.168.1.100:4224",
    "AuthStrategy": "UserPassword",
    "Username": "advantech_nats",
    "Password": "<password>"
  }
}
```

Must point to the **same NATS broker** as the `daq-collector`.

---

#### devicecfg.json — Proxy Configuration

**Required Changes**:

1. **DAQ SubNode Name** - `DaqDataCollectorSubNodeName`

   Must exactly match the `SubNode.Name` in `daq-collector`'s `devicecfg.json`:
   ```json
   "DaqDataCollectorSubNodeName": "UniaxialVibrationDevice2"
   ```
   The proxy uses this name to find the DAQ device in the WEDA Node capability registry at startup.

2. **PHM Model ID** - `PhmApiModelId`

   The UUID of a trained PHM model. The model must be in `ready` status:
   ```json
   "PhmApiModelId": "e4f32d57-9ced-41e8-ac69-03d9376b676d"
   ```
   To list available models:
   ```bash
   curl http://<phm-host>:8000/api/v1/models/
   ```

3. **PHM API Endpoint** - `PhmApiEndpoint`

   Base URL of the PHM Inference Service:
   ```json
   "PhmApiEndpoint": "http://172.16.9.17:8000/api/v1/models"
   ```

**Optional Adjustments**:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `PhmApiTimeoutMs` | `5000` | PHM API call timeout (ms). If exceeded, `health_score` falls back to `-1` and `anomaly_result` becomes `"unavailable"` |
| `PhmAnomalyThreshold` | `0.3` | Health score below this value triggers `"anomaly_detected"` |

**Restart container after changes** (no need to rebuild image):
```bash
docker compose down
docker compose up -d
```

### 3. Start Container

```bash
docker compose up -d
```

### 4. Verify Deployment Status

See the "Check Installation Status" section below.

### 5. Stop Container

```bash
docker compose down
```

---

## Check Installation Status

### 1. Check Container Status

```bash
docker compose ps
```

**Should see**:
```
NAME                  IMAGE                      COMMAND   STATUS
daq-feature-proxy     daq-feature-proxy:latest   ...       Up 10 seconds
```

- `STATUS` should show `Up`
- If showing `Exited` or `Exit X`, container startup failed — check logs

### 2. Check Logs

```bash
docker compose logs -f
```

**Normal startup (should see)**:

```
===========================================
    DAQ Feature Proxy
===========================================

Starting DAQ feature proxy...
Press Ctrl+C to stop.

[HH:mm:ss INF] Starting Weda SubNode Application
...
[HH:mm:ss INF] Connecting to WedaNode
[HH:mm:ss INF] Connected to WedaNode
[HH:mm:ss INF] SubNode registered with ID: 267492018484150273
...
[HH:mm:ss INF] NATS client initialized: nats://192.168.1.100:4224
[HH:mm:ss INF] Capability resolved: DeviceId=267492018484150272, Topic=eco1j.weda.267492018484150272.telemetry, 7 feature sensors mapped
[HH:mm:ss INF] PHM Feature Proxy initialized: Subject=eco1j.weda.267492018484150272.telemetry, ModelId=e4f32d57-..., Endpoint=http://172.16.9.17:8000/api/v1/models
```

**After processing the first telemetry frame (every ~1 second)**:
```
[HH:mm:ss INF] Feature message received, processing...
[HH:mm:ss INF] PHM API response: healthScore=0.87, anomaly_result=normal, processingTime=45ms
```

### 3. Check Output Telemetry

Proxy results are published to the WEDA Node as standard sensor telemetry. Verify by checking the cloud dashboard or subscribing to the proxy's telemetry subject:

```bash
# Replace <proxyId> with the SubNode registered ID from the startup logs
nats sub "eco1j.weda.<proxyId>.telemetry" --server nats://192.168.1.100:4224 -u advantech_nats -p <password>
```

---

## Troubleshooting

### Container Failed to Start

**Check items**:
1. Is `systemcfg.json` pointing to the correct NATS server?
2. Is `PhmApiModelId` configured and non-empty?
3. Is `DaqDataCollectorSubNodeName` configured and non-empty?

```bash
docker compose logs
```

### Capability Query Failed — Proxy Will Not Start

**Log message**:
```
[ERR] Capability query failed — feature proxy will not start
```

**Possible causes**:
- `daq-collector` is not running or not yet registered with WEDA Node
- `DaqDataCollectorSubNodeName` does not match the exact `SubNode.Name` in the DAQ's `devicecfg.json`
- NATS broker unreachable

**Solutions**:
1. Verify `daq-collector` is running: `docker compose ps` on the DAQ host
2. Check `SubNode.Name` in the DAQ's `devicecfg.json` and ensure it matches exactly (case-sensitive)
3. Test NATS connectivity: `telnet 192.168.1.100 4224`

### PHM Service Unavailable

**Log message**:
```
[WRN] PHM API timed out after 5000ms for model e4f32d57-...
```

**Impact**: Proxy continues running. Telemetry is still published with `health_score=-1` and `anomaly_result="unavailable"`.

**Solutions**:
1. Verify PHM Service is running: `curl http://<phm-host>:8000/api/v1/healthz`
2. Verify model is in `ready` status: `curl http://<phm-host>:8000/api/v1/models/<modelId>/status`
3. Check `PhmApiEndpoint` and `PhmApiModelId` in `devicecfg.json`

### No Features Being Processed

**Log message**:
```
[WRN] Received empty feature set, skipping
```

**Possible causes**:
- The DAQ is publishing features but none match the feature convention table
- All features from the capability response are unknown sensor names

**Solutions**:
1. Check the number of mapped sensors in the startup log (`N feature sensors mapped`)
2. If `0 feature sensors mapped`, the DAQ sensor names don't match the expected convention (e.g., `x_axis_rms_mg`)
3. Review the DAQ `devicecfg.json` sensor names and compare with the convention table in `02_OUTPUT_SENSORS.md`
