# DAQ Data Collector

DAQ Data Collector is a real-time data acquisition and feature extraction system for collecting vibration data from Advantech iDAQ hardware and sending processed data to Weda Node via NATS communication with on-the-fly feature extraction.

## Core Features

- **Real-time Data Acquisition**: Collect high-frequency vibration data from DAQ hardware (up to 10kHz)
- **Feature Extraction**: Calculate time and frequency domain features (RMS, peak, deviation, kurtosis, etc.)
- **Uniaxial Support**: Process single-axis vibration data from accelerometer
- **NATS Integration**: Communicate with Weda Node via NATS
- **Flexible Configuration**: Dynamic configuration of sampling rate, collection interval, and feature enablement

## Architecture Overview

The system follows a streaming architecture with clear separation of concerns, optimized for continuous vibration data acquisition and feature extraction:

```
+─────────────────────────────────────────────────────────────────+
|                    Device Layer                                 |
+─────────────────────────────────────────────────────────────────+
|                                                                 |
|  +───────────────────────────────────────────────────────────+  |
|  |  UniaxialVibrationDevice                                  |  |
|  |  (Streaming Device - Orchestrates DAQ + Feature Pipeline) |  |
|  +───────────────────────────────────────────────────────────+  |
|                            | extends                            |
|                            v                                    |
|  +───────────────────────────────────────────────────────────+  |
|  |  StreamingDeviceBase (SubNode.Core Framework)             |  |
|  |  (Streaming Pattern - Telemetry & Health Reporting)       |  |
|  +───────────────────────────────────────────────────────────+  |
|                            | extends                            |
|                            v                                    |
|  +───────────────────────────────────────────────────────────+  |
|  |  DeviceBase (SubNode.Core Framework)                      |  |
|  |  (Core Base - Lifecycle & Configuration Management)       |  |
|  +───────────────────────────────────────────────────────────+  |
|                                                                 |
+─────────────────────────────────────────────────────────────────+

+─────────────────────────────────────────────────────────────────+
|           Data Acquisition & Streaming Pipeline                 |
+─────────────────────────────────────────────────────────────────+
|                                                                 |
|  +───────────────────────────────────────────────────────────+  |
|  |  DaqCollector                                             |  |
|  |  (Raw Data - Streams frames from Advantech.Edge.Daq)      |  |
|  +───────────────────────────────────────────────────────────+  |
|         ↓ async enumerable (IAsyncEnumerable)                   |
|  +───────────────────────────────────────────────────────────+  |
|  |  DaqCommunication (StreamingCommunicationBase)            |  |
|  |  (Communication - Serializes DAQ frames to JSON)          |  |
|  +───────────────────────────────────────────────────────────+  |
|         ↓ streaming connection                                  |
|  +───────────────────────────────────────────────────────────+  |
|  |  Feature Extraction Pipeline                              |  |
|  |  ├─ FrequencyDomainExtractor (RMS, Peak, Displacement)    |  |
|  |  ├─ TimeDomainExtractor (Deviation, Skewness, Kurtosis)   |  |
|  |  └─ PhmFeatureTransform (NATS Telemetry Integration)      |  |
|  +───────────────────────────────────────────────────────────+  |
|         ↓ NATS publish                                          |
|  +───────────────────────────────────────────────────────────+  |
|  |  Weda Node (via NATS)                                     |  |
|  |  (Telemetry & Configuration Hub)                          |  |
|  +───────────────────────────────────────────────────────────+  |
|                                                                 |
+─────────────────────────────────────────────────────────────────+
```

## System Workflow

### 1. Hardware Data Acquisition (Local)
- **DaqCollector** streams raw acceleration samples from Advantech.Edge.Daq at configured sampling rate
- Accumulates samples until `FrameSize` is reached (calculated from sampling rate × frame interval)
- Yields complete `DaqRawFrame` through async enumerable

### 2. Communication & Serialization
- **DaqCommunication** consumes the DaqRawFrame stream via `StreamAsync()`
- Serializes frames to JSON payloads for pipeline processing

### 3. Feature Extraction Pipeline
- **FrequencyDomainExtractor**: Performs FFT analysis, calculates RMS, peak magnitude, displacement, and velocity
- **TimeDomainExtractor**: Calculates statistical features (deviation, skewness, kurtosis, crest factor)
- **PhmFeatureTransform**: Integrates extracted features with NATS telemetry system

### 4. Downlink: Configuration Updates
- Cloud publishes configuration changes via Weda Node
- `OnBeforeConfigUpdateAsync` hook validates new parameters (sampling rate, intervals, etc.)
- Device applies updates dynamically without restart

### 5. Uplink: Telemetry Reporting
- Extracted features published to Weda Node via NATS
- Raw frames available as JSON payload for offline analysis (optional)

## Supported Feature Types

### Time Domain Features (Statistical Analysis)
- **Deviation**: Standard deviation of acceleration values
- **Skewness**: Asymmetry of acceleration distribution
- **Kurtosis**: Tail weight of acceleration distribution
- **Crest Factor**: Peak-to-RMS ratio indicating shock content

### Frequency Domain Features (FFT-based)
- **RMS (mG)**: Root-mean-square from FFT spectrum (Parseval's theorem)
- **Peak (mG)**: Maximum magnitude from FFT frequency spectrum
- **Peak-to-Peak Displacement**: Displacement amplitude via double integration
- **Overall Velocity**: Overall acceleration velocity RMS from FFT

### Raw Data
- **DAQ Raw Frame**: Complete JSON payload for offline analysis

## Configuration Parameters

| Parameter | Default | Range | Description |
|-----------|---------|-------|-------------|
| AccelerationSamplingRate | 2500 | 1000-10000 | Sampling rate (Hz) |
| FrameIntervalSeconds | 1.0 | 0.5-5.0 | Collection interval (seconds) |
| DecimationFactor | 2 | 1-10 | Decimation factor |

## Quick Start

### Method 1: Local Execution (Development)

```bash
cd examples/daq-collector
dotnet run
```

### Method 2: Docker Container (Recommended for Production)

```bash
# 1. Prepare configuration files
mkdir -p /opt/daq-collector
cd /opt/daq-collector

# Edit appsettings.json (modify NATS URL)
# Edit devicecfg.json (optional, adjust sampling parameters)

# 2. Start the container
docker compose up -d

# 3. View logs
docker compose logs -f
```

## Documentation

See the `docs/` directory:

- [01_QUICK_START.md](docs/01_QUICK_START.md) - Quick Start Guide
- [02_METRIC_TYPES.md](docs/02_METRIC_TYPES.md) - Sensor and Feature Types Reference
- [03_DOCKER_DEPLOY.md](docs/03_DOCKER_DEPLOY.md) - Docker Deployment and Multi-platform Support
- [04_TESTING_GUIDE.md](docs/04_TESTING_GUIDE.md) - Testing Guide

## Project Structure

```
daq-data-collector/
├── Communication/         # DAQ Communication Layer
├── Devices/               # Device Implementations
├── Models/                # Data Models
├── Protocols/             # Protocol Handling
├── Commands/              # Command Processing
├── Dockerfile             # Docker Image Definition
├── docker-compose.yml     # Docker Compose Configuration
├── Program.cs             # Application Entry Point
├── appsettings.json       # Application Configuration
├── devicecfg.json         # Device Configuration
├── systemcfg.json         # System Configuration
└── docs/                  # Documentation
```

## Configuration Files

### appsettings.json

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  },
  "Nats": {
    "Url": "nats://192.168.1.100:4222"
  }
}
```

### devicecfg.json

```json
{
  "DeviceConfigs": {
    "UniaxialVibrationDeviceConfig": {
      "Enabled": true,
      "Properties": {
        "AccelerationSamplingRate": 2500,
        "FrameIntervalSeconds": 1.0,
        "DecimationFactor": 2
      },
      "Sensors": [
        {
          "Name": "x_axis_rms_mg",
          "Parameters": {
            "FeatureName": "XAxisRMSmg"
          },
          "Report": {
            "Enabled": true,
            "Interval": 1000
          }
        }
      ]
    }
  }
}
```

## Common Operations

### Modify Collection Parameters

Edit `devicecfg.json`:

```bash
# Set sampling rate to 5000 Hz
"AccelerationSamplingRate": 5000

# Set collection interval to 0.5 seconds
"FrameIntervalSeconds": 0.5

# Restart container to apply changes
docker compose down && docker compose up -d
```

### Enable/Disable Features

In the `Sensors` section of `devicecfg.json`:

```json
{
  "Name": "feature_name",
  "Report": {
    "Enabled": true,  // Set to false to disable
    "Interval": 1000
  }
}
```

### View Real-time Data

```bash
# View container logs
docker compose logs -f

# Subscribe to NATS telemetry topic
nats sub "devices/YOUR_DEVICE_ID/telemetry"
```

## Troubleshooting

### Container Failed to Start

```bash
# View error logs
docker compose logs

# Verify configuration file format
cat appsettings.json
cat devicecfg.json
```

### Collected Data is Zero

1. Check DAQ hardware connection
2. Verify driver version
3. Check system logs for errors

### Failed to Connect NATS

```bash
# Test NATS connection
telnet NATS_HOST NATS_PORT

# Check firewall rules
sudo ufw status
```

## Support

- See detailed documentation in `docs/` folder
- Check container logs for error information
- Review project configuration examples

## License

Proprietary - Advantech

## Changelog

See [CHANGELOG.md](../../CHANGELOG.md)

