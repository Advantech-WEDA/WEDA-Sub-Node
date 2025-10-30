# Complete Getting Started Guide - Your First Working Example

This guide walks you through a complete working example from zero to a running IoT device application in **10 minutes**.

## What You'll Build

A complete IoT data collection system:
1. **Modbus TCP Simulator** - Acts as a physical device with 5 sensors
2. **Device Application** - Reads sensor data and processes it
3. **Sandbox Mode** - Works without cloud (NullCloudService)

**No hardware required!** Everything runs on your local machine.

---

## Prerequisites

- *> .NET 9.0 SDK installed
- *> Terminal/Command prompt
- *> 5-10 minutes of your time

---

## Step 1: Start the Modbus Simulator (2 minutes)

### What is the Modbus Simulator?

The Modbus TCP Simulator acts as a **virtual IoT device** that simulates real sensors:
- [TEMP] Temperature Sensor (18-32°C)
- [WATER] Humidity Sensor (30-80%)
- [AIR] Pressure Sensor (980-1030 hPa)
- [FAST] Voltage Sensor (220-240V)
- [ELEC] Current Sensor (0-10A)

Values change gradually over time to simulate real-world behavior.

### Start the Simulator

```bash
# Navigate to simulator directory
cd /path/to/weda_subdevice/examples/ModbusSimulatorExample

# Run the simulator
dotnet run
```

### Expected Output

```
[23:38:33 INF] ╔════════════════════════════════════════════════════════╗
[23:38:33 INF] ║ Modbus TCP Simulator - Standalone Example             ║
[23:38:33 INF] ╚════════════════════════════════════════════════════════╝
[23:38:33 INF] ✓ Simulator started successfully
[23:38:33 INF]   Listening on: 127.0.0.1:5020
[23:38:33 INF]   Slave ID: 1
[23:38:33 INF]
[23:38:33 INF] Simulated Sensors:
[23:38:33 INF]   - TemperatureSensor (Temperature): Address 40001, Float32, Unit: °C
[23:38:33 INF]   - HumiditySensor (Humidity): Address 40003, Float32, Unit: %
[23:38:33 INF]   - PressureSensor (Pressure): Address 40005, Float32, Unit: hPa
[23:38:33 INF]   - VoltageSensor (Voltage): Address 40007, Float32, Unit: V
[23:38:33 INF]   - CurrentSensor (Current): Address 40009, Float32, Unit: A
[23:38:33 INF]
[23:38:33 INF] Press Ctrl+C to stop...
```

*> **Success!** The simulator is now running on `127.0.0.1:5020`

**Keep this terminal open** - the simulator needs to run while we test the device application.

---

## Step 2: Create Your Device Application (3 minutes)

Open a **new terminal window** (keep the simulator running in the first one).

### Option A: Use Simple API Template (Recommended)

```bash
# Navigate to examples directory
cd /path/to/weda_subdevice/examples

# Create new project from template
dotnet new wedaapi -n MyFirstDeviceComplete

# Navigate to project
cd MyFirstDeviceComplete
```

### Option B: Use Custom Device Template

```bash
cd /path/to/weda_subdevice/examples
dotnet new subnode -n MyFirstDeviceComplete
cd MyFirstDeviceComplete
```

---

## Step 3: Configure the Device (3 minutes)

Edit `appsettings.json` to connect to the simulator:

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Weda.SubNode": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  },

  "DeviceConfigs": {
    "SimulatedDevice": {
      "DeviceId": "sim-device-001",
      "DeviceName": "My Simulated Device",
      "DeviceType": "ModbusTCP",

      "DeviceCapabilities": {
        "CanReceiveCommands": false,
        "CanSendTelemetry": true,
        "SupportedProtocols": ["ModbusTCP"]
      },

      "Communication": {
        "Host": "127.0.0.1",
        "Port": 5020,
        "SlaveId": 1,
        "ConnectionTimeout": 5000,
        "RetryAttempts": 3
      },

      "Periods": {
        "PollingIntervalMs": 2000,
        "HeartbeatIntervalMs": 30000,
        "HealthCheckIntervalMs": 10000
      },

      "Sensors": [
        {
          "ResourceId": "temp-001",
          "Name": "TemperatureSensor",
          "Dtmi": "dtmi:weda:sensor:Temperature;1",
          "DeviceResourceId": "sim-device-001",
          "SensorGroup": "AI",
          "Parameters": {
            "StartAddress": 40001,
            "RegisterCount": 2,
            "DataType": "Float32"
          },
          "Config": {
            "Enabled": true,
            "Interval": 2000
          }
        },
        {
          "ResourceId": "humidity-001",
          "Name": "HumiditySensor",
          "Dtmi": "dtmi:weda:sensor:Humidity;1",
          "DeviceResourceId": "sim-device-001",
          "SensorGroup": "AI",
          "Parameters": {
            "StartAddress": 40003,
            "RegisterCount": 2,
            "DataType": "Float32"
          },
          "Config": {
            "Enabled": true,
            "Interval": 2000
          }
        },
        {
          "ResourceId": "pressure-001",
          "Name": "PressureSensor",
          "Dtmi": "dtmi:weda:sensor:Pressure;1",
          "DeviceResourceId": "sim-device-001",
          "SensorGroup": "AI",
          "Parameters": {
            "StartAddress": 40005,
            "RegisterCount": 2,
            "DataType": "Float32"
          },
          "Config": {
            "Enabled": true,
            "Interval": 2000
          }
        }
      ]
    }
  },

  "CloudService": {
    "Type": "Null",
    "Enabled": false
  }
}
```

### Configuration Explained

**Device Connection:**
- `Host`: `127.0.0.1` - Connect to localhost where simulator is running
- `Port`: `5020` - Match simulator's port
- `SlaveId`: `1` - Modbus slave ID

**Sensors:**
- Each sensor maps to a Modbus register address
- `StartAddress`: `40001`, `40003`, `40005` - Match simulator configuration
- `RegisterCount`: `2` - Float32 uses 2 registers
- `Interval`: `2000ms` - Read every 2 seconds

**Sandbox Mode:**
- `CloudService.Type`: `"Null"` - Use NullCloudService (no cloud connection)
- `CloudService.Enabled`: `false` - Disable cloud integration

---

## Step 4: Run the Device Application (2 minutes)

Build and run:

```bash
dotnet build
dotnet run
```

### Expected Output

```
[23:40:15 INF] Starting Weda SubNode application...
[23:40:15 INF] Cloud service: Null (Sandbox Mode)
[23:40:15 INF] Initializing device: SimulatedDevice (sim-device-001)
[23:40:15 INF] Device configuration loaded: 3 sensors configured
[23:40:15 DBG] Connecting to Modbus device at 127.0.0.1:5020
[23:40:15 INF] ✓ Device started successfully
[23:40:15 INF] Press Ctrl+C to stop...
[23:40:17 DBG] Reading sensor: temp-001 (TemperatureSensor)
[23:40:17 DBG] Reading sensor: humidity-001 (HumiditySensor)
[23:40:17 DBG] Reading sensor: pressure-001 (PressureSensor)
[23:40:17 INF] [DATA] TemperatureSensor: 25.3°C
[23:40:17 INF] [DATA] HumiditySensor: 54.8%
[23:40:17 INF] [DATA] PressureSensor: 1013.2 hPa
[23:40:19 INF] [DATA] TemperatureSensor: 25.5°C
[23:40:19 INF] [DATA] HumiditySensor: 55.1%
[23:40:19 INF] [DATA] PressureSensor: 1013.3 hPa
```

*> **Success!** Your device is reading data from the simulator!

---

## Step 5: Understand What's Happening

### Architecture

```
┌─────────────────────┐          ┌──────────────────────┐
│  Modbus Simulator   │          │  Device Application  │
│  (Port 5020)        │◄────────►│  (Modbus Client)     │
│                     │  Modbus  │                      │
│  - Temp: 25.3°C     │   TCP    │  - Reads every 2s    │
│  - Humidity: 54.8%  │          │  - Logs to console   │
│  - Pressure: 1013hPa│          │  - No cloud (sandbox)│
└─────────────────────┘          └──────────────────────┘
```

### Data Flow

1. **Simulator** generates realistic sensor values
2. **Device Application** polls sensors every 2 seconds via Modbus TCP
3. **Values are logged** to console (no cloud because `CloudService.Type = "Null"`)

---

## Step 6: Experiment (Optional)

### Add More Sensors

Edit `appsettings.json` to add voltage and current sensors:

```json
{
  "ResourceId": "voltage-001",
  "Name": "VoltageSensor",
  "Dtmi": "dtmi:weda:sensor:Voltage;1",
  "DeviceResourceId": "sim-device-001",
  "SensorGroup": "AI",
  "Parameters": {
    "StartAddress": 40007,
    "RegisterCount": 2,
    "DataType": "Float32"
  },
  "Config": {
    "Enabled": true,
    "Interval": 2000
  }
}
```

Restart the device application to see the new sensor.

### Add Data Transformation

Add calibration to the temperature sensor:

```json
{
  "ResourceId": "temp-001",
  "Config": {
    "Enabled": true,
    "Interval": 2000,
    "TransformPipeline": [
      {
        "Type": "Calibration",
        "Enabled": true,
        "Order": 0,
        "Parameters": {
          "Scale": 1.05,
          "Offset": -2.0
        }
      }
    ]
  }
}
```

Now temperature values will be calibrated: `value = (rawValue * 1.05) - 2.0`

### Add DSP Filter

Add Kalman filter for noise reduction:

```json
{
  "ResourceId": "temp-001",
  "Config": {
    "Enabled": true,
    "Interval": 2000,
    "DspPipeline": [
      {
        "Type": "Kalman",
        "Enabled": true,
        "Order": 0,
        "Parameters": {
          "ProcessNoise": 0.01,
          "MeasurementNoise": 0.1
        }
      }
    ]
  }
}
```

The temperature values will now be smoothed using a Kalman filter.

---

## Troubleshooting

### Error: "Connection refused" or "Cannot connect to Modbus"

**Problem**: Device can't connect to simulator

**Solutions**:
1. Make sure simulator is running in another terminal
2. Check simulator is listening on `127.0.0.1:5020`
3. Check firewall isn't blocking localhost connections
4. Verify port `5020` isn't used by another application

### Error: "No data received"

**Problem**: Device connects but no sensor data

**Solutions**:
1. Check `StartAddress` in device config matches simulator sensor addresses
2. Verify `DataType` is correct (`Float32` for all simulator sensors)
3. Check `RegisterCount` is `2` for Float32 values

### Simulator shows no connection

**Problem**: Simulator is running but shows no client connections

**Solutions**:
1. Check device application is actually running
2. Verify connection settings in `appsettings.json`
3. Look for error messages in device application logs

---

## Next Steps

Now that you have a working example, you can:

1. **[Add Data Transformations](03_add_transformation.md)** - Process sensor data
2. **[Add DSP Filters](04_add_dsp_filters.md)** - Signal processing
3. **[Connect to Real Hardware](02_sandbox_testing.md#connecting-to-real-hardware)** - Replace simulator with actual Modbus device
4. **[Enable Cloud Integration](02_sandbox_testing.md#enabling-cloud)** - Send data to NATS/DMA

---

## Summary

[SUCCESS] **Congratulations!** You've built a complete IoT data collection system:

*> **Modbus TCP Simulator** - Virtual sensors generating realistic data
*> **Device Application** - Reading and processing sensor data
*> **Sandbox Mode** - No cloud required for testing
*> **Real-time Data** - Values update every 2 seconds
*> **Extensible** - Easy to add transforms, filters, and more sensors

**Total time**: ~10 minutes from zero to working system!

---

## What You Learned

- *> How to start a Modbus TCP simulator
- *> How to configure a device to connect to Modbus
- *> How to use NullCloudService for sandbox testing
- *> How to read sensor data without hardware
- *> How sensor addresses map to Modbus registers
- *> How to add transformations and filters

**Ready for more?** Check out the [Quick Start Guide](01_quick_start.md) for different template options!
