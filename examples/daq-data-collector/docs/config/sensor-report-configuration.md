# Sensor Report Configuration Guide

> Understanding `Report.Enabled` and `Report.Interval` in the WEDA SubNode Framework

## Table of Contents

1. [Overview](#overview)
2. [Report.Enabled](#reportenabled)
3. [Report.Interval](#reportinterval)
4. [Report.Interval Override Scenarios](#reportinterval-override-scenarios)
5. [Bulk Update Strategies](#bulk-update-strategies)
6. [Implementation Examples](#implementation-examples)

---

## Overview

Every sensor in the WEDA SubNode framework has a `Report` configuration that controls two critical aspects:
- **Enabled**: Whether the sensor is actively collecting and reporting data
- **Interval**: The sampling/reporting frequency in milliseconds

```json
{
  "Name": "x_axis_rms_mg",
  "Report": {
    "Enabled": true,
    "Interval": 1000
  }
}
```

---

## Report.Enabled

### Purpose

`Report.Enabled` is a **switch to enable/disable sensor data collection and transmission**.

### Behavior

- **true**: Sensor is actively polled, data is collected, transformed, and reported to the cloud
- **false**: Sensor is skipped entirely; no data is collected or sent

### How It Works

In `DeviceBase.cs`, only effectively enabled sensors are selected for processing:

```csharp
protected List<(int IntervalMs, List<Sensor> Sensors)> GroupSensorsByInterval()
{
    return Configuration.Sensors
        .Where(s => s.IsEffectivelyEnabled)  // ← Filters enabled sensors only
        .GroupBy(s => (int)s.Report.Interval)
        .Select(g => (IntervalMs: g.Key, Sensors: g.ToList()))
        .ToList();
}
```

Where `IsEffectivelyEnabled` is:

```csharp
public bool IsEffectivelyEnabled => DeviceEnabled && Report.Enabled;
```

This means:
- If `DeviceEnabled = false` (device-level), all sensors are skipped (regardless of `Report.Enabled`)
- If `Report.Enabled = false`, only that sensor is skipped

### Example: DAQ Raw Frame

In the DAQ data collector configuration:

```json
{
  "Name": "daqraw_vibration_payload",
  "Report": {
    "Enabled": true,       // ← Raw frame is reported
    "Interval": 1000
  }
}
```

Setting `Enabled: true` means:
- Raw acceleration data is extracted and reported
- Computed PHM features are also published alongside raw data
- Raw JSON payload is available for pipeline processing

---

## Report.Interval

### Purpose

`Report.Interval` defines the **sampling/reporting frequency** for a sensor in milliseconds.

### Behavior

- **Unit**: Milliseconds (e.g., `1000` = once per second)
- **Grouping**: Sensors with identical intervals are grouped and read together
- **Polling**: Each interval group has its own `PeriodicTimer` task
- **Read Frequency**: Determines how often the device queries the sensor

### How It Works

In `DeviceBase.cs`, sensors are grouped by interval:

```csharp
// Example: If you have sensors with intervals [1000, 1000, 2000]:
var groups = GroupSensorsByInterval();
// Result: [(1000ms, [sensor1, sensor2]), (2000ms, [sensor3])]
```

Then a separate polling loop runs for each group:

```csharp
protected async Task RunIntervalLoopAsync(
    List<Sensor> sensors,
    int intervalMs,  // ← From Report.Interval
    ...)
{
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));
    
    while (await timer.WaitForNextTickAsync(cancellationToken))
    {
        await ProcessIntervalGroupAsync(sensors, cancellationToken);  // Read sensors
    }
}
```

### Example: DAQ Configuration

In your DAQ data collector:

```json
"Sensors": [
  {
    "Name": "daqraw_vibration_payload",
    "Report": {
      "Enabled": false,
      "Interval": 1000     // ← If enabled, would read every 1000ms
    }
  },
  {
    "Name": "x_axis_rms_mg",
    "Report": {
      "Enabled": true,
      "Interval": 1000     // ← Read every 1000ms
    }
  },
  {
    "Name": "x_axis_peak_mg",
    "Report": {
      "Enabled": true,
      "Interval": 1000     // ← Read every 1000ms (grouped with x_axis_rms_mg)
    }
  }
]
```

**Execution Flow**:
1. System scans 11 sensors, finds all have `Interval: 1000`
2. Groups 10 enabled sensors into "1000ms interval group"
3. Creates periodic task: every 1000ms, read these 10 sensors
4. Disabled `daqraw_vibration_payload` is filtered out (not in any polling loop)

---

## Report.Interval Override Scenarios

The `Report.Interval` value can be overwritten in three scenarios:

### Scenario 1: Cloud Configuration Update (Primary)

When the device receives a configuration update from WedaCore, `Report.Interval` is overwritten:

**Location**: `ConfigurationUpdateHelper.cs` line 223

```csharp
public static List<string> ApplysensorReportUpdates(
    DeviceConfiguration deviceConfig,
    IReadOnlyList<SubNodeSensorReportDto>? desiredSensors)
{
    foreach (var desiredSensor in desiredSensors)
    {
        var sensor = deviceConfig.Sensors.FirstOrDefault(s =>
            s.Name.Equals(desiredSensor.Name, StringComparison.OrdinalIgnoreCase));

        if (sensor != null && desiredSensor.Config != null)
        {
            sensor.Report.Interval = desiredSensor.Config.Interval;  // ← Overwritten
        }
    }
}
```

**Trigger Events**:
- Device startup (loads initial config from cloud)
- Runtime configuration update from WedaCore
- Configuration patch deployment

**Example**: Cloud sends message to change `x_axis_rms_mg` interval from 1000ms to 5000ms:

```json
{
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "UniaxialVibrationDeviceConfig": {
              "sensors": [
                {
                  "name": "x_axis_rms_mg",
                  "config": {
                    "interval": 5000  // ← New interval
                  }
                }
              ]
            }
          }
        }
      }
    }
  }
}
```

### Scenario 2: Configuration Update Rollback (Failure Recovery)

If configuration update validation fails or raises an exception, the framework restores previous values:

**Location**: `ConfigurationUpdateHelper.cs` line 185

```csharp
public static void RestoreBackup(DeviceConfiguration config, DeviceConfigurationBackup backup)
{
    foreach (var sensorBackup in backup.SensorBackups)
    {
        var sensor = config.Sensors.FirstOrDefault(s =>
            s.Name.Equals(sensorBackup.Name, StringComparison.OrdinalIgnoreCase));

        if (sensor != null)
        {
            sensor.Report.Interval = sensorBackup.Interval;  // ← Restored to previous value
        }
    }
}
```

**Purpose**: Ensure configuration consistency if updates fail

### Scenario 3: New Sensor Creation from DTO (Initialization)

When creating sensors from DTOs (Data Transfer Objects), `Report.Interval` is set:

**Location**: `ConfigurationUpdateHelper.cs` line 1007

```csharp
if (dto.Report != null)
{
    sensor.Report.Interval = dto.Report.Interval;  // ← Set from DTO
}
```

**Scenarios**:
- Initial load from `devicecfg.json`
- Creating sensors from cloud API responses
- Sensor replacement operations

---

## Bulk Update Strategies

Currently, **there is no built-in global mechanism** to update all sensors' `Report.Interval` simultaneously. However, there are **3 alternative approaches**:

### Strategy 1: Cloud Batch Configuration (Simplest)

Specify the same `Interval` for all sensors in the WedaCore configuration update message:

**Advantages**:
- No code changes required
- Leverages existing update mechanism
- Remote management capability

**Implementation**:

```json
{
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "UniaxialVibrationDeviceConfig": {
              "sensors": [
                { "name": "daqraw_vibration_payload", "config": { "interval": 5000 } },
                { "name": "x_axis_rms_mg", "config": { "interval": 5000 } },
                { "name": "x_axis_peak_mg", "config": { "interval": 5000 } },
                { "name": "x_axis_peak_to_peak_displacement", "config": { "interval": 5000 } },
                { "name": "x_axis_oa_velocity", "config": { "interval": 5000 } },
                { "name": "x_axis_deviation", "config": { "interval": 5000 } },
                { "name": "x_axis_skewness", "config": { "interval": 5000 } },
                { "name": "x_axis_kurtosis", "config": { "interval": 5000 } },
                { "name": "x_axis_crest_factor", "config": { "interval": 5000 } },
                { "name": "timestamp_timestamp", "config": { "interval": 5000 } },
                { "name": "device_time", "config": { "interval": 5000 } }
              ]
            }
          }
        }
      }
    }
  }
}
```

### Strategy 2: Custom Batch Update Method (Flexible)

Add a public method to `DeviceBase` or a derived class:

**Advantages**:
- Direct and efficient
- Easy to test
- Suitable for runtime logic control

**Implementation**:

```csharp
public void SetAllSensorIntervals(int intervalMs)
{
    foreach (var sensor in Configuration.Sensors)
    {
        sensor.Report.Interval = intervalMs;
    }
    
    _logger.LogInformation(
        "Updated all {Count} sensors to interval {Interval}ms", 
        Configuration.Sensors.Count, 
        intervalMs);
}
```

**Usage in Command Handler**:

```csharp
// Example: custom command "set.sensorinterval"
public async Task HandleSetSensorIntervalAsync(int intervalMs, CancellationToken ct)
{
    _device.SetAllSensorIntervals(intervalMs);
    await _device.StartBackgroundTasksAsync(ct);
    return new { status = "success", message = $"All sensors updated to {intervalMs}ms" };
}
```

### Strategy 3: Initialize in devicecfg.json (Development Time)

Set all sensors to the same `Interval` during development/deployment:

**Advantages**:
- Configuration-level consistency
- No repetitive coding
- Clear during deployment

**Implementation**:

```json
{
  "Sensors": [
    {
      "Name": "daqraw_vibration_payload",
      "Report": {
        "Enabled": false,
        "Interval": 5000    // ← Same interval
      }
    },
    {
      "Name": "x_axis_rms_mg",
      "Report": {
        "Enabled": true,
        "Interval": 5000    // ← Same interval
      }
    },
    {
      "Name": "x_axis_peak_mg",
      "Report": {
        "Enabled": true,
        "Interval": 5000    // ← Same interval
      }
    }
    // ... all other sensors with Interval: 5000
  ]
}
```

---

## Strategy Comparison

| Strategy          | Use Case                  | Complexity | Dynamic | Scope           |
| ----------------- | ------------------------- | ---------- | ------- | --------------- |
| **Cloud Batch**   | Remote dynamic adjustment | ⭐ Simple   | ✅ Yes   | All sensors     |
| **Custom Method** | Local logic control       | ⭐⭐ Medium  | ✅ Yes   | All or selected |
| **JSON Config**   | Development/deployment    | ⭐ Simple   | ❌ No    | All sensors     |

---

## Implementation Examples

### Example 1: Using Cloud Batch Configuration

Send update from WedaCore to double the sampling rate:

```json
{
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "UniaxialVibrationDeviceConfig": {
              "sensors": [
                // All 11 sensors updated to 2000ms
                { "name": "daqraw_vibration_payload", "config": { "interval": 2000 } },
                { "name": "x_axis_rms_mg", "config": { "interval": 2000 } },
                // ... more sensors
              ]
            }
          }
        }
      }
    }
  }
}
```

### Example 2: Using Custom Method in DAQ Device

Extend `DaqDataCollectorDevice` with batch interval update:

```csharp
public class DaqDataCollectorDevice : StreamingDeviceBase
{
    public void SetAllSensorSamplingRate(int intervalMs)
    {
        foreach (var sensor in Configuration.Sensors)
        {
            sensor.Report.Interval = intervalMs;
            _logger.LogInformation(
                "Sensor '{Name}' interval updated to {Interval}ms",
                sensor.Name, intervalMs);
        }
        
        // Recalculate batch send period
        RecalculateSendPeriod();
    }

    private void RecalculateSendPeriod()
    {
        var minInterval = Configuration.Sensors
            .Where(s => s.IsEffectivelyEnabled)
            .Min(s => (int)s.Report.Interval);
        
        _logger.LogInformation("New batch send period: {Period}ms", minInterval);
    }
}
```

### Example 3: Initialize All Sensors with Same Interval

In `devicecfg.json`, use a consistent interval:

```json
{
  "SubNode": {
    "Name": "UniaxialVibrationDevice",
    "SubNodeType": "DaqDevice"
  },
  "DeviceConfigs": {
    "UniaxialVibrationDeviceConfig": {
      "Properties": {
        "AccelerationSamplingRate": 2500
      },
      "Sensors": [
        {
          "Name": "daqraw_vibration_payload",
          "Report": { "Enabled": false, "Interval": 5000 }
        },
        {
          "Name": "x_axis_rms_mg",
          "Report": { "Enabled": true, "Interval": 5000 }
        },
        // ... all sensors with Interval: 5000
      ]
    }
  }
}
```

---

## Recommendations

**For DAQ Applications**:

| Scenario               | Recommended Strategy       | Rationale                           |
| ---------------------- | -------------------------- | ----------------------------------- |
| Frequent tuning        | Strategy 2 (Custom Method) | Runtime flexibility + local control |
| One-time setup         | Strategy 3 (JSON Config)   | Simple, clear, deployment-time      |
| Production management  | Strategy 1 (Cloud Batch)   | Remote management + monitoring      |
| Development            | Strategy 3 (JSON Config)   | Easy iteration                      |
| Production adjustments | Strategy 1 (Cloud Batch)   | Audit trail + remote capability     |

---

## Related Configuration

- **Report.Enabled**: Controls whether a sensor participates in data collection
- **Report.Interval**: Controls HOW OFTEN a sensor is sampled (in milliseconds)
- **Record.Enabled**: Controls whether telemetry is stored locally
- **Record.Interval**: Controls local storage frequency (0 = use Report.Interval)

See also:
- [Architecture Overview](./arch_plan_daq_data_collector.md)
- [Data Flow](./devicecfg_dataflow.md)
- [Implementation Guide](./impl_guide_daq_data_collector.md)

---

## Summary

- **Report.Enabled**: Master switch for sensor data collection (enabled/disabled)
- **Report.Interval**: Polling frequency for each sensor (in milliseconds)
- **Override Scenarios**: Cloud updates, rollback recovery, DTO initialization
- **Bulk Updates**: Use cloud configuration, custom methods, or JSON initialization
- **Best Practice**: Match your update strategy to your operational requirements
