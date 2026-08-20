# DAQ Sensor and Feature Type Configuration Reference

Users specify DAQ sensor collection and feature types by setting `Parameters` and `Report` configurations.

## Sensor Configuration Structure

Each Sensor in `devicecfg.json` has the following structure:

```json
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
}
```

| Field | Description |
|-------|-------------|
| `Name` | Sensor identifier. For the raw payload sensor and the 10 standard PHM/SYS feature sensors below, this **must exactly match** one of the reserved names (case-insensitive) — see warning below |
| `SensorGroup` | Sensor group (usually `AI` for analog input; `SYS` for system metadata sensors) |
| `Parameters.FeatureName` | Feature name (select from available features list) |
| `Report.Enabled` | Whether this sensor is enabled |
| `Report.Interval` | Report interval (milliseconds) — must be a positive integer multiple of `ObservationWindowSeconds × 1000`, otherwise **the container fails to start** (see below) |
| `SensorInfo.Schema` | Expected data type (`double`, `long`, `string`, `boolean`, `integer`) |
| `SensorInfo.Description` | Sensor description |
| `SensorInfo.DisplayName` | Sensor display name. **For the raw payload sensor only**, this field is load-bearing: it must contain the substring `Raw Vibration` (case-sensitive) — the pipeline uses this to locate the raw sensor, not `ResourceId` or `Name` |

> ⚠️ **Reserved sensor `Name` values**: The raw payload sensor and the 10 standard feature sensors are matched by fixed, hardcoded names in the pipeline (`UniaxialVibrationDevice.ExtractPhmSensorMapping`). Renaming one **does not raise an error** — that sensor is silently excluded from the feature pipeline and simply stops reporting data.
>
> `timestamp_timestamp`, `device_time`, `x_axis_rms_mg`, `x_axis_peak_mg`, `x_axis_peak_to_peak_displacement`, `x_axis_oa_velocity`, `x_axis_deviation`, `x_axis_skewness`, `x_axis_kurtosis`, `x_axis_crest_factor`
>
> The raw payload sensor is identified differently: the pipeline looks for a sensor whose `SensorInfo.DisplayName` contains `Raw Vibration` — **not** by `Name` or `ResourceId`. If you customize the raw sensor's `DisplayName` (e.g., translating it), keep the substring `Raw Vibration` in it, or the feature extraction pipeline fails to start with `Raw DAQ payload sensor ('daqraw:vibration:payload') is required but not configured`.

---

## Collection Parameters Configuration

### DeviceCommunication - Hardware Settings

```json
"DeviceCommunication": {
  "DaqModuleDeviceNumber": 0
}
```

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `DaqModuleDeviceNumber` | int | DAQ module device number (see below) | 0 |

To find the correct value, run the following command **on the HOST** (not inside the container — DAQNavi's `dndev` tool reads hardware state that's only set up on the host):

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

The leftmost number is the `DaqModuleDeviceNumber`. Set it to the index of the target DAQ module (e.g., `3` for iDAQ-801 in the example above).

> **Note — Analog channel**: The collector currently reads **channel 0 (X-axis) only**. This is hard-coded in `DaqCollector.cs` and is designed for single-axis accelerometers. Multi-channel support is not available in this version.

### Properties - Collection Parameters

```json
"Properties": {
  "AcquisitionRateHz": 1000,
  "ObservationWindowSeconds": 1.0
}
```

| Parameter | Type | Description | Default | Guidance |
|-----------|------|-------------|---------|----------|
| `AcquisitionRateHz` | int | Raw data acquisition rate (Hz) | 1000 | Set ≥ 2× highest target frequency (Nyquist); e.g., 1000 Hz covers up to 500 Hz |
| `ObservationWindowSeconds` | double | Observation window duration (seconds) | 1.0 | Set ≥ 1 / lowest target frequency; e.g., 1.0 s gives 1 Hz resolution |

**Parameter Explanations**:
- **AcquisitionRateHz**: Determines the highest detectable frequency (Nyquist limit = AcquisitionRateHz / 2). Set to at least 2× your highest target frequency.
- **ObservationWindowSeconds**: Controls FFT frequency resolution (Δf = 1 / ObservationWindowSeconds). Set to at least 1 / lowest target frequency. `FrameSize` is auto-derived as `AcquisitionRateHz × ObservationWindowSeconds`.

> ⚠️ **`Report.Interval` is validated at startup, not just a guideline**: every enabled sensor's `Report.Interval` (ms) must be a positive integer multiple of `ObservationWindowSeconds × 1000`. If it isn't, `UniaxialVibrationDevice` throws during initialization and **the container fails to start** — this also applies to configuration updates pushed from the cloud, which are rejected with a validation error instead of being applied.

---

## Available Feature Types - Uniaxial Vibration

### Frequency Domain Features (FFT-based)

#### RMS (XAxisRMSmg)
- **Data Type**: double
- **Unit**: mG (milligravity)
- **Description**: Root-mean-square extracted from FFT spectrum using Parseval's theorem
- **Application**: Represents overall acceleration energy in frequency domain
- **Extraction Method**: FrequencyDomainExtractor (FFT-based)

**Configuration Example**:
```json
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
    "DisplayName": "X-Axis RMS (mG)",
    "Description": "RMS acceleration from FFT analysis"
  }
}
```

#### Peak (XAxisPeakmg)
- **Data Type**: double
- **Unit**: mG (milligravity)
- **Description**: Maximum magnitude from FFT frequency spectrum
- **Application**: Detect dominant frequency components and spectral peaks
- **Extraction Method**: FrequencyDomainExtractor (FFT-based)

#### Peak-to-Peak Displacement (XAxisPeakToPeakDisplacement)
- **Data Type**: double
- **Unit**: Displacement unit
- **Description**: Peak-to-peak displacement amplitude via double integration of FFT
- **Application**: Measure displacement for low-frequency bearing faults
- **Extraction Method**: FrequencyDomainExtractor (FFT double integration)

#### Overall Velocity (XAxisOAVelocity)
- **Data Type**: double
- **Unit**: Velocity unit
- **Description**: Overall acceleration velocity RMS from FFT spectrum
- **Application**: Detect overall vibration velocity envelope
- **Extraction Method**: FrequencyDomainExtractor (FFT-based)

### Time Domain Features (Statistical Analysis)

#### Deviation (XAxisDeviation)
- **Data Type**: double
- **Unit**: (unitless, standard deviation)
- **Description**: Standard deviation of raw acceleration samples
- **Application**: Measure vibration variability and distribution spread
- **Extraction Method**: TimeDomainExtractor (statistical analysis)

### Statistical Features (Time Domain)

#### Skewness (XAxisSkewness)
- **Data Type**: double
- **Unit**: (unitless)
- **Description**: Asymmetry of acceleration distribution
- **Application**: Identify asymmetric vibration patterns indicating specific fault types
- **Extraction Method**: TimeDomainExtractor (statistical analysis)

#### Kurtosis (XAxisKurtosis)
- **Data Type**: double
- **Unit**: (unitless)
- **Description**: Tail weight of acceleration distribution (higher kurtosis = more impulsive)
- **Application**: Detect impulsive events and bearing defects
- **Extraction Method**: TimeDomainExtractor (statistical analysis)

#### Crest Factor (XAxisCrestFactor)
- **Data Type**: double
- **Unit**: (unitless, ratio)
- **Description**: Ratio of peak amplitude to RMS value
- **Application**: Identify shock-like disturbances and impact events
- **Extraction Method**: TimeDomainExtractor (peak-to-RMS calculation)

**Configuration Example**:
```json
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
    "Description": "Kurtosis for detecting impulsive faults"
  }
}
```

### Raw Data

#### daqraw_vibration_payload
- **Data Type**: JSON string
- **Description**: Complete JSON data from raw DAQ collection frame
- **Application**: Internal trigger for `PhmFeatureTransform` — must remain enabled for the feature extraction pipeline to function
- **Note**: The pipeline locates this sensor by `SensorInfo.DisplayName` containing `Raw Vibration` (case-sensitive) — **not** by `ResourceId` or `Name`. Keep that substring in the `DisplayName` if you customize it (see the reserved-name warning above)

**Configuration Example**:
```json
{
  "ResourceId": "daqraw:vibration:payload",
  "Name": "daqraw_vibration_payload",
  "SensorGroup": "AI",
  "Report": {
    "Enabled": true,
    "Interval": 2000
  },
  "SensorInfo": {
    "Schema": "application/json",
    "DisplayName": "Raw Vibration Payload",
    "Description": "Raw DAQ frame JSON payload for feature extraction pipeline"
  }
}
```

### System Metadata Sensors (SensorGroup: SYS)

These sensors are populated automatically by `PhmFeatureTransform` and do not require a `Parameters.FeatureName` mapping in the `Sensors` list. They must be listed in `Sensors` with `SensorGroup: "SYS"` for the platform to register them.

#### Timestamp (timestamp_timestamp)
- **Data Type**: dateTime
- **Description**: Hardware sample timestamp from the first sample in the DAQ frame
- **FeatureName**: `Timestamp`

#### Device Time (device_time)
- **Data Type**: dateTime
- **Description**: Edge device system clock at feature computation time
- **FeatureName**: `DeviceTime`

**Configuration Example**:
```json
{
  "Name": "timestamp_timestamp",
  "SensorGroup": "SYS",
  "Parameters": { "FeatureName": "Timestamp" },
  "Report": { "Enabled": true, "Interval": 2000 },
  "SensorInfo": {
    "Schema": "dateTime",
    "DisplayName": "Timestamp",
    "Description": "Hardware sample timestamp"
  }
},
{
  "Name": "device_time",
  "SensorGroup": "SYS",
  "Parameters": { "FeatureName": "DeviceTime" },
  "Report": { "Enabled": true, "Interval": 2000 },
  "SensorInfo": {
    "Schema": "dateTime",
    "DisplayName": "Device Time",
    "Description": "Edge device system clock at feature computation time"
  }
}
```

---

## Complete Configuration Example

### Recommended Configuration (for Monitoring and Fault Diagnosis)

```json
{
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
        "AcquisitionRateHz": 1000,
        "ObservationWindowSeconds": 1.0
      },
      "Sensors": [
        {
          "ResourceId": "daqraw:vibration:payload",
          "Name": "daqraw_vibration_payload",
          "SensorGroup": "AI",
          "Report": {
            "Enabled": true,
            "Interval": 2000
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
            "Interval": 2000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis RMS (mG)",
            "Description": "RMS acceleration on X-axis"
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
            "Interval": 2000
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
            "Interval": 2000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis Peak-to-Peak Displacement",
            "Description": "Peak-to-peak displacement via double spectral integration"
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
            "Interval": 2000
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
            "Interval": 2000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis Deviation (std)",
            "Description": "Standard deviation of acceleration on X-axis"
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
            "Interval": 2000
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
            "Interval": 2000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis Kurtosis",
            "Description": "Kurtosis (tail weight) of acceleration distribution"
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
            "Interval": 2000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis Crest Factor",
            "Description": "Peak-to-RMS ratio for shock detection"
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
            "Interval": 2000
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
            "Interval": 2000
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

---

## Configuration Best Practices

### 1. Sampling Rate Selection

- **Low-speed rotating equipment (<3000 RPM)**: Sampling rate 1000-2500 Hz
- **Mid-speed rotating equipment (3000-10000 RPM)**: Sampling rate 2500-5000 Hz
- **High-speed equipment (>10000 RPM)**: Sampling rate 5000-10000 Hz

### 2. Frame Collection Interval

- **Real-time monitoring**: 0.5-1.0 seconds
- **Periodic checks**: 2.0-5.0 seconds
- **Long-term archival**: 5.0 seconds or more

### 3. Feature Enablement Recommendation

| Use Case | Enable | Disable |
|----------|--------|---------|
| Real-time monitoring | RMS, Peak Freq | Raw data |
| Fault diagnosis | RMS, Peak, FFT Energy | Raw data |
| Data collection | All features + raw data | - |

### 4. Report Interval Recommendation

| Purpose | Interval |
|---------|----------|
| Real-time alerts | 1000 ms (1 second) |
| Trend analysis | 5000 ms (5 seconds) |
| Long-term statistics | 30000 ms (30 seconds) |

---

## Troubleshooting

### Feature Values are Zero or NaN

**Possible Causes**:
- Hardware not connected or connection improper
- Driver version mismatch
- Sampling rate too high causing buffer overflow

**Solutions**:
1. Verify hardware connection status
2. Check driver version
3. Lower sampling rate and retry

### Specific Axis Features Missing

**Possible Causes**:
- That axis hardware failed
- Sensor calibration incorrect

**Solutions**:
1. Check hardware connection and status
2. Recalibrate sensor
3. Review system logs for errors

