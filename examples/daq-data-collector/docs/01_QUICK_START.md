# Quick Start Guide

## Prerequisites

- Device has Docker Compose installed
- Network connectivity to NATS server
- DAQ hardware device (iDAQ-801+B10BG3 or compatible) properly connected
- Required DAQ driver software installed on device

## Quick Start

### 1. Prepare Files

Copy the following file templates and upload to device directory:
- `docker-compose.yml`
- `appsettings.json`
- `devicecfg.json`

For example, using `/opt/daq-collector`:
```bash
mkdir -p /opt/daq-collector
cd /opt/daq-collector
# Upload files via scp, rsync, or other method
```

### 2. Edit Configuration

```bash
nano appsettings.json
```

**Required Changes**:

1. **NATS Server Address** - `Nats.Url`
   ```json
   "Nats": {
     "Url": "nats://192.168.1.100:4222"
   }
   ```

2. **Device Name** - `DeviceName`
   - **New Device**: Set unique name, e.g., `daq-collector-Device-01`
   - **Already Registered**: Do not modify `weda-data/.weda` directory to preserve registration info

**Optional Adjustments - Collection Parameters**:

Location: `DeviceConfigs.UniaxialVibrationDeviceConfig.Properties`

| Parameter | Default Value | Description |
|-----------|----------------|-------------|
| `AccelerationSamplingRate` | 2500 | Acceleration sampling rate (Hz) |
| `FrameIntervalSeconds` | 1.0 | Frame collection interval (seconds) |
| `DecimationFactor` | 2 | Decimation factor |

**Common Adjustment Example**:

```json
"Properties": {
  "AccelerationSamplingRate": 2500,    // Sampling rate: 2500 Hz
  "FrameIntervalSeconds": 1.0,         // Collect one frame per second
  "DecimationFactor": 2                // 2x decimation
}
```

**Adjust Sensor Collection Frequency**:

Location: `DeviceConfigs.UniaxialVibrationDeviceConfig.Sensors[].Report.Interval`

For example, X-axis RMS:
```json
{
  "Name": "x_axis_rms_mg",
  "Report": {
    "Enabled": true,
    "Interval": 1000  // Collect once per second
  }
}
```

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

See the \"Check Installation Status\" section below

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
NAME             IMAGE                 COMMAND                STATUS
daq-collector   daq-data-collector:latest "./DaqDataCollect?? Up 10 seconds
```

- `STATUS` should show `Up`
- If showing `Exited` or `Exit X`, container startup failed

### 2. Check Logs

```bash
docker compose logs -f
```

**Normal logs (should see)**:

```
Starting Weda SubNode Application
......
Auto-generated DTDL for device 'UniaxialVibrationDeviceConfig' with 12 sensors
Loaded 1 device configuration(s): [UniaxialVibrationDeviceConfig]
......
Connecting to WedaNode
......
Connected to WedaNode
......
SubNode registered with ID: 267492018484150272
......
Uploading device configuration: DeviceId=267492018484150272, DeviceName=UniaxialVibrationDeviceConfig
Configuration uploaded successfully: DeviceId=267492018484150272, Status=applied
Uploading device response:True,code:0 where DeviceId=267492018484150272, DeviceName=UniaxialVibrationDeviceConfig
Device configuration uploaded successfully
Device initialized successfully in 169ms
```

**After running continuously, you'll see**:
```
Collecting DAQ frame for interval: 1.0 seconds
DAQ acquisition completed: 2500 samples collected
Processing features: XAxisRMSmg, XAxisPeakmg, XAxisDeviation, XAxisKurtosis
Features extracted and ready for telemetry
```

### 3. Subscribe to Telemetry Data

Use the DeviceId from logs to subscribe to telemetry:

```bash
# Example: Subscribe to vibration telemetry
SUBJECT=\"devices/267492018484150272/telemetry\"
nats sub \"$SUBJECT\"
```

## Troubleshooting

### Container Failed to Start

**Check items**:
1. Is DAQ hardware properly connected?
2. Is driver software installed?
3. Is NATS URL format correct?

```bash
# View error logs
docker compose logs
```

### Collected Data is Zero or NaN

**Possible causes**:
- DAQ hardware not connected
- Driver software version mismatch
- Sampling rate set too high or too low

**Solutions**:
1. Verify hardware connection
2. Check sampling rate configuration
3. Review error messages in logs

### Failed to Connect to NATS

**Check items**:
```bash
# Test NATS connection
telnet 192.168.1.100 4222
```

If unable to connect, verify:
- NATS server is running
- Firewall rules allow connection
- IP address and port are correct
