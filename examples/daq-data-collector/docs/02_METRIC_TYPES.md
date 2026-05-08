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
| `Name` | Unique sensor identifier name |
| `SensorGroup` | Sensor group (usually `AI` for analog input) |
| `Parameters.FeatureName` | Feature name (select from available features list) |
| `Report.Enabled` | Whether this sensor is enabled |
| `Report.Interval` | Report interval (milliseconds) |
| `SensorInfo.Schema` | Expected data type (`double`, `long`, `string`, `boolean`, `integer`) |
| `SensorInfo.Description` | Sensor description |
| `SensorInfo.DisplayName` | Sensor display name |

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
| `DaqModuleDeviceNumber` | int | DAQ module device number | 0 |

### Properties - Collection Parameters

```json
"Properties": {
  "AccelerationSamplingRate": 2500,
  "FrameIntervalSeconds": 1.0,
  "DecimationFactor": 2
}
```

| Parameter | Type | Description | Default | Recommended Range |
|-----------|------|-------------|---------|-------------------|
| `AccelerationSamplingRate` | int | Acceleration sampling rate (Hz) | 2500 | 1000-10000 |
| `FrameIntervalSeconds` | double | Frame collection interval (seconds) | 1.0 | 0.5-5.0 |
| `DecimationFactor` | int | Decimation factor | 2 | 1-10 |

**Parameter Explanations**:
- **AccelerationSamplingRate**: Higher sampling rate captures higher frequencies (Nyquist theorem: max frequency = sampling rate / 2)
- **FrameIntervalSeconds**: Interval between complete data frame collections
- **DecimationFactor**: Reduces feature extraction data resolution

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
- **Application**: For offline analysis or custom feature extraction
- **Recommendation**: Usually set `Enabled: false` due to large data volume

**Configuration Example**:
```json
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
        "AccelerationSamplingRate": 2500,
        "FrameIntervalSeconds": 1.0,
        "DecimationFactor": 2
      },
      "Sensors": [
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
            "Description": "RMS acceleration on X-axis"
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
            "DisplayName": "X-Axis Deviation (std)",
            "Description": "Standard deviation of acceleration on X-axis"
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
            "Interval": 1000
          },
          "SensorInfo": {
            "Schema": "double",
            "DisplayName": "X-Axis Crest Factor",
            "Description": "Peak-to-RMS ratio for shock detection"
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

