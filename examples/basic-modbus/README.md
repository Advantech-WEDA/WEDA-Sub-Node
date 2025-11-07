# Modbus SubNode Example

This example demonstrates a **Weda SubNode** connecting to a Modbus TCP simulator and sending telemetry to the cloud.

## Purpose

This example shows how to:
- Create a ModbusDevice using **TcpModbusDeviceConfiguration** (strongly-typed configuration)
- Load sensor configurations directly from appsettings.json or programmatically
- Connect to a physical Modbus device (or simulator)
- Auto-initialize device with Cloud registration
- Generate deviceId and resourceIds using UUID5 algorithm
- Read telemetry from Modbus registers
- Send telemetry to Cloud (using NullCloudService for testing)
- Persist deviceId across restarts (`.weda` folder)

## Architecture

```
┌─────────────────────┐
│  TcpModbusSimulator │  <- Physical Device (or Simulator)
│  (Port 5020)        │     Running ModbusSimulatorExample
└──────────┬──────────┘
           │ TCP Modbus Protocol
           │
┌──────────▼──────────┐
│   ModbusDevice      │  <- Weda SubNode SDK
│   (This Example)    │
│                     │
│  - Connects via TCP │
│  - Gets deviceId    │
│  - Generates IDs    │
│  - Reads telemetry  │
└──────────┬──────────┘
           │ NATS (mocked)
           │
┌──────────▼──────────┐
│  NullCloudService   │  <- Mock Cloud Service
│                     │     (Logs telemetry)
└─────────────────────┘
```

## Prerequisites

**You must start the simulator first:**

```bash
# Terminal 1 - Start the simulator
cd examples/ModbusSimulatorExample
dotnet run
```

The simulator must be running on `127.0.0.1:5020` before starting this example.

## Running the Example

```bash
# Terminal 2 - Start the SubNode
cd examples/ModbusDeviceExample
dotnet run
```

## What Happens

### Step 1: Device Configuration
- Loads SubNode configuration (groupId, deviceName, deviceUid)
- Loads Modbus connection settings (IP, port, slaveId)
- Loads sensor mappings from appsettings.json
- Generates deviceId using UUID5(groupId, deviceUid)
- Generates resourceId for each sensor using UUID5(deviceId, sensorName)

### Step 2: Initialize Device
The device automatically:
1. Connects to Modbus TCP simulator
2. Connects to Cloud service (NullCloudService)
3. Gets or registers deviceId:
   - First run: Registers with Cloud, saves to `.weda/ModbusSubNode.weda`
   - Subsequent runs: Loads deviceId from file (no re-registration)
4. Uploads device configuration to Cloud
5. Subscribes to Cloud events (configuration updates, commands)

### Step 3: Test Reading
- Reads all sensor values once via Modbus
- Displays values with resourceIds

### Step 4: Background Tasks
- Starts periodic telemetry reading (default: every 5 seconds)
- Sends telemetry to Cloud via NATS
- Continues until Ctrl+C

### Step 5: Cleanup
- Stops background tasks
- Disconnects from Modbus and Cloud
- Properly disposes resources

## Device ID Persistence

On first run, the device:
- Registers with Cloud to get deviceId
- Saves deviceId to `.weda/ModbusSubNode.weda`

On subsequent runs, the device:
- Loads deviceId from `.weda/ModbusSubNode.weda`
- Skips re-registration with Cloud
- Falls back to Cloud registration if file read fails

## Configuration

Edit [appsettings.json](appsettings.json) to customize:

### SubNode Identity
```json
{
  "SubNode": {
    "GroupId": "weda",
    "DeviceName": "ModbusSubNode"
  }
}
```

### Modbus Connection
```json
{
  "ModbusConnection": {
    "IpAddress": "127.0.0.1",
    "Port": 5020,
    "SlaveId": 1
  }
}
```

### Sensor Mappings
```json
{
  "Sensors": [
    {
      "Name": "temperature.sensor",
      "Dtmi": "dtmi:advantech:EdgeSync:Temperature;1",
      "RegisterAddress": 0,
      "RegisterCount": 2,
      "DataType": "Float32",
      "SensorGroup": "Temperature"
    }
  ]
}
```

**Note**: RegisterAddress is 0-based (Modbus addressing offset already handled by simulator).

## Testing Device ID Persistence

Run the example twice to verify persistence:

```bash
# First run - device registers and saves ID
dotnet run
# Check .weda/ModbusSubNode.weda file created
# Note the deviceId in logs

# Second run - device loads ID from file
dotnet run
# Verify same deviceId is used (no re-registration)
```

## Logging

Logs are written to:
- Console: Minimal format for readability
- File: `logs/modbus-device-<date>.log` with detailed format
- Debug output: For IDE debugging

## Architecture Separation

This example demonstrates the **separation of concerns**:

1. **Simulator** (ModbusSimulatorExample):
   - Pure Modbus TCP device
   - No knowledge of SubNode SDK
   - No deviceId or resourceId
   - Can be tested with any Modbus client

2. **SubNode** (This example):
   - Reads from simulator via standard Modbus TCP
   - Enriches data with deviceId and resourceId
   - Handles Cloud communication
   - Manages device lifecycle

The two components communicate only via **standard Modbus TCP protocol**.
