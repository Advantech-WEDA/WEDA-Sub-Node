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
- `systemcfg.json`
- `devicecfg.json`
- `customcfg.json`

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

1. **WEDA Node Address** - `WedaNode.Url` in `systemcfg.json`
   ```json
   "WedaNode": {
     "Url": "192.168.1.100:4224"
   }
   ```

2. **Device Name** - `SubNode.Name` in `devicecfg.json`
   - **New Device**: Set a unique name, e.g., `daq-collector-Device-01`
   - **Already Registered**: Do not modify the `weda-data/` directory to preserve registration info

**Optional Adjustments - Collection Parameters**:

Location: `DeviceConfigs.UniaxialVibrationDeviceConfig.Properties`

| Parameter | Default Value | Description |
|-----------|----------------|-------------|
| `AcquisitionRateHz` | 1000 | Raw data acquisition rate (Hz); set to ≥ 2× highest target frequency (Nyquist) |
| `ObservationWindowSeconds` | 1.0 | Observation window duration (seconds); set to ≥ 1 / lowest target frequency |

**Common Adjustment Example**:

```json
"Properties": {
  "AcquisitionRateHz": 1000,          // Acquisition rate: 1000 Hz (Nyquist limit: 500 Hz)
  "ObservationWindowSeconds": 1.0     // Observation window: 1.0 s → 1 Hz frequency resolution
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
NAME                  IMAGE                       COMMAND   STATUS
daq-data-collector    daq-data-collector:latest   ...       Up 10 seconds
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
Starting DAQ streaming: DaqDeviceNumber=3, SamplingRate=1000 Hz, FrameSize=1000, FrameInterval=1 s
Discovering DAQ modules...
Discovered 1 DAQ module(s).
Found target DAQ module: DeviceNumber=3
Successfully created DAQ module instance for device 3.
Configured DAQ module for streaming: SampleClockSource=BackplaneClock, SampleInterval=1 ms
Received report with 1000 samples for channel 0.
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
