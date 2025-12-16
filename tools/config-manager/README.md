# SubNode Config Manager

A simple web-based configuration management tool for SubNode devices.

## Features

- Load configuration from project path (`appsettings.json` and `.weda/*.json`)
- Visual editor for sensor configurations (enable/disable, interval, etc.)
- JSON editor for advanced editing
- Send configuration updates to devices via NATS

## Usage

```bash
# Run the tool
cd tools/config-manager
dotnet run

# Open browser
open http://localhost:5000
```

## How it works

1. Enter the path to your SubNode project (e.g., `/path/to/examples/wise-4012`)
2. Click "Load Configuration" to read:
   - `appsettings.json` - Device configuration
   - `.weda/subnode.registration.json` - Device registration and NATS topics
3. Edit sensor configurations using the visual or JSON editor
4. Click "Apply Configuration" to send the update via NATS

## API Endpoints

### GET /api/config?path={projectPath}

Reads configuration files from the specified project path.

### POST /api/config/apply

Sends configuration update to the device via NATS.

Request body:
```json
{
  "natsUrl": "nats://localhost:4222",
  "natsUsername": "optional",
  "natsPassword": "optional",
  "registration": { ... },
  "deviceConfigs": { ... }
}
```

## Configuration Update Message Format

The tool sends configuration updates in the following format:

```json
{
  "protoVer": "eco1j",
  "groupId": "weda",
  "deviceId": "258874645107703808",
  "cmd": "updateCmd",
  "seqId": 1734567890123,
  "reqSeqId": "guid",
  "timestamp": 1734567890123,
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "DeviceConfigs": { ... }
        }
      }
    }
  }
}
```