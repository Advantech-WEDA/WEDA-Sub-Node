# Quick Start Guide

## Prerequisites

- Device has Docker Compose installed
- Network connectivity to the WedaNode's NATS endpoint (WedaNode communicates over the NATS protocol — this is the same host/port you'll set as `WedaNode.Url` below, not a separate standalone NATS server)
- Read/pull access to the private image registry `harbor.arfa.wise-paas.com` — confirm you can `docker login` before starting
- DAQ hardware device (iDAQ-801+B10BG3 or compatible) properly connected
- Required DAQ driver software (DAQNavi + SUSI) installed on device — verified in Step 1 below
- (Optional, for the telemetry verification step) [NATS CLI](https://github.com/nats-io/natscli) installed

## Quick Start

### 1. Verify DAQNavi Driver is Installed

Most Advantech iDAQ devices ship with DAQNavi pre-installed. Confirm before proceeding:

```bash
sudo /opt/advantech/tools/dndev
```

- **Lists your DAQ module(s)** (e.g., `DAQNavi devices list in system: ...`) → DAQNavi is ready, continue to Step 2.
- **Command not found, or no devices listed** → DAQNavi isn't installed (or the hardware isn't detected). Follow [00_DAQNAVI_DRIVER_INSTALL.md](00_DAQNAVI_DRIVER_INSTALL.md) to install the driver for your architecture, then come back here.
  > **Note**: ARM driver requests can take **3-5 business days** to process — plan for this before committing to a deployment schedule.

### 2. Prepare Files

Copy the following file templates and upload to device directory:
- `docker-compose.yml`
- `appsettings.json` — Serilog (logging) settings only; not used for WedaNode/NATS connection
- `systemcfg.json` — WedaNode connection settings
- `devicecfg.json` — device identity and DAQ collection settings
- `customcfg.json` — optional free-form config for user-defined settings (loaded into a `CustomConfig` section); this example doesn't use it, so the shipped empty `{}` is expected — leave it as-is

For example, using `/opt/Advantech/data-collector`:
```bash
mkdir -p /opt/Advantech/data-collector
cd /opt/Advantech/data-collector
# Upload files via scp, rsync, or other method
```

> **Note**: A `weda-data/` directory is created automatically on first `docker compose up` to persist device registration state — you don't need to create it manually.

### 3. Log In and Pull the Image

The image is hosted on a private registry. Log in once per device, then pull:

```bash
docker login harbor.arfa.wise-paas.com
docker compose pull
```

If your device cannot reach the registry, see [03_DOCKER_DEPLOY.md](03_DOCKER_DEPLOY.md) for offline (tar-based) deployment instead.

### 4. Edit Configuration

Edit `systemcfg.json` and `devicecfg.json` (both uploaded in Step 2). `appsettings.json` only controls logging and normally needs no changes.

```bash
nano systemcfg.json
nano devicecfg.json
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

3. **DAQ Module Device Number** - `DeviceCommunication.DaqModuleDeviceNumber` in `devicecfg.json`

   Run the following command **on the HOST** (not inside the container — DAQNavi's `dndev` tool reads hardware state that's only set up on the host) to list available DAQ modules:
   ```bash
   sudo /opt/advantech/tools/dndev
   ```
   Example output:
   ```
   DAQNavi devices list in system:
    0, USB series\iDAQ-934 Chassis, BID#0\"iDAQ-751,BID#1"
    1, USB series\iDAQ-934 Chassis, BID#0\"iDAQ-815,BID#2"
    2, USB series\iDAQ-934 Chassis, BID#0\"iDAQ-821,BID#3"
    3, USB series\iDAQ-934 Chassis, BID#0\"iDAQ-801,BID#4"
   ```
   Set `DaqModuleDeviceNumber` to the leftmost index of the target module (e.g., `3` for iDAQ-801).

   > **Note**: The collector reads **channel 0 (X-axis) only** — this is hard-coded and not configurable.

   > **About `docker-compose.yml`'s `devices:` block**: DAQNavi accesses hardware through `/dev/daqN` character device nodes — the `/opt/advantech/` and `/var/lib/daq/` mounts alone are not sufficient. On the **host**, run `find /dev -maxdepth 1 -name 'daq*' | sort` to see which nodes actually exist, then uncomment the matching `- /dev/daqN:/dev/daqN` lines in `docker-compose.yml` (add more lines if your system exposes additional module indices). The index shown by `find` should line up with the `DaqModuleDeviceNumber` you set above.

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

### 5. Start Container

```bash
docker compose up -d
```

### 6. Verify Deployment Status

See the \"Check Installation Status\" section below

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

### 4. Stop / Restart Container

```bash
# Stop
docker compose down

# Restart after config changes (no rebuild needed)
docker compose up -d
```

## Troubleshooting

### Image Pull Failed / Unauthorized

**Check items**:
```bash
# Confirm login
docker login harbor.arfa.wise-paas.com

# Retry pull with verbose output
docker compose pull
```

If this fails, verify:
- Registry credentials are correct and not expired
- Device has network access to `harbor.arfa.wise-paas.com`
- If the device cannot reach the registry at all, use the offline (tar-based) deployment method in [03_DOCKER_DEPLOY.md](03_DOCKER_DEPLOY.md) instead

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
# Test connectivity to the port configured in systemcfg.json's WedaNode.Url
# (example below uses 4224 — replace with your actual configured port)
telnet 192.168.1.100 4224
```

If unable to connect, verify:
- WedaNode is running
- Firewall rules allow connection
- IP address and port match `WedaNode.Url` in `systemcfg.json`
