---
name: subnode
description: |
  SubNode project scaffolding tool for the Weda IoT edge computing framework. Uses `dotnet new` templates to create projects, then guides users through edge connection (simulator or real device), cloud connection (mock or real WedaNode), sensor configuration, and end-to-end verification.

  TRIGGER THIS SKILL when users:
  - Ask to create a new SubNode project or IoT device project
  - Want to scaffold a Modbus TCP device or custom protocol device
  - Mention "subnode", "weda", "edge device", "IoT project", or similar
  - Need to set up device communication (Modbus TCP, serial, DAQ)
  - Want to add a new device to the SubNode framework

  Even if the user doesn't explicitly say "subnode", trigger this skill if they're working in the edge_subnode repository and want to create a new device or project.
---

# SubNode Project Scaffolding

You are a specialized assistant for creating SubNode IoT edge computing projects. Guide users through an interactive process to build complete, verified, production-ready projects.

## Reference Project

The SubNode SDK repo is typically named `edge_subnode`. Use the user's current working directory or ask them where their `edge_subnode` repo is located. Store this as `{REPO_ROOT}` and use it throughout. When in doubt, read the templates and examples in the project.

Helper tools and scripts at `{SKILL_DIR}` = `~/.claude/skills/subnode/`:
- `scripts/test-edge-connection.sh` - Test Modbus TCP device reachability
- `scripts/test-cloud-connection.sh` - Test NATS connectivity (auto-builds NatsCheck from source on first use)
- `scripts/validate-devicecfg.sh` - Validate devicecfg.json for common errors
- `scripts/verify-project.sh` - Build + run + automated log verification
- `scripts/build-tools.sh` - Manually rebuild NatsCheck for current platform

NatsCheck source: `{REPO_ROOT}/tools/nats-check/` (built on demand, cached at `{SKILL_DIR}/tools/`).

Standalone simulator host at `{REPO_ROOT}/tools/simulator-host/` for docker-compose use.

## Step 0: Prerequisite - Install Templates

Before scaffolding, check if dotnet templates are already installed:

```bash
dotnet new list | grep -i subnode
```

If the output shows `subnode` and `wedabuilder` templates, skip to the next step. Otherwise, install them automatically:

```bash
cd {REPO_ROOT}
bash scripts/install-templates.sh
# Verify:
dotnet new list | grep -i subnode
```

If `scripts/install-templates.sh` does not exist, fall back to:
```bash
dotnet new install {REPO_ROOT}/templates/subnode
dotnet new install {REPO_ROOT}/templates/wedabuilder
```

Two templates are available:
- **`subnode`** - Manual context pattern (WedaApplicationContext + SubNode class)
- **`wedabuilder`** - Builder pattern (WedaApplication.CreateDefaultBuilder) - **recommended for most cases**

## Interactive Workflow

Gather information step by step using AskUserQuestion. Don't ask all questions at once.

### Phase 1: Basic Information

1. **Project Name** (kebab-case for directory, PascalCase for namespace)
   - Example: `power-meter` -> namespace `PowerMeter`

2. **Template** - Which pattern to use:
   - **`wedabuilder`** (recommended) - ASP.NET Core-style builder with hosted services, config from JSON files
   - **`subnode`** - Manual context with programmatic device configuration

3. **SubNodeType** - The device category (enum `SubNodeType`):
   - **`AdamEthernet`** - ADAM Ethernet-based devices (Modbus TCP/IP, HTTP/HTTPS)
   - **`SerialDevice`** - Serial communication (Modbus RTU/ASCII, custom protocols)
   - **`DaqDevice`** - High-speed data acquisition cards (PCI/PCIe drivers)
   - **`SystemMonitor`** - Built-in system monitoring (System APIs)
   - **`CustomDevice`** - User-defined custom devices (extensible protocols)

### Phase 2: Scaffold the Project

Run `dotnet new` in the `apps/` directory (create it if it doesn't exist):

```bash
mkdir -p {REPO_ROOT}/apps
cd {REPO_ROOT}/apps
dotnet new {template} -n {ProjectName}
```

This generates the complete project structure including:
- `Program.cs` - Application entry point
- `MyFirstDevice.cs` - Device implementation (TcpModbusDevice subclass)
- `appsettings.json` - Serilog configuration + simulator config (wedabuilder only)
- `devicecfg.json` - SubNode metadata + device configs + sensors
- `systemcfg.json` - WedaNode (NATS) connection settings
- `{ProjectName}.csproj` - Project references

### Phase 3: Edge Connection Setup

**CRITICAL: The user MUST have a working edge data source before proceeding.**

Ask the user: "Do you have a real device ready, or should we use the built-in Modbus TCP simulator?"

#### Option A: Use Standalone Simulator via docker-compose (recommended)

The simulator runs as a **separate container**, keeping the app's Program.cs clean. Add to the project's `docker-compose.yml`:

```yaml
services:
  simulator:
    build:
      context: ../..
      dockerfile: tools/simulator-host/Dockerfile
    volumes:
      - ./simulator-config.json:/app/appsettings.json:ro
    network_mode: host
    restart: unless-stopped

  app:
    build:
      context: .
      dockerfile: Dockerfile
    volumes:
      - ./devicecfg.json:/app/devicecfg.json:ro
      - ./systemcfg.json:/app/systemcfg.json:ro
      - ./appsettings.json:/app/appsettings.json:ro
    network_mode: host
    restart: unless-stopped
    depends_on:
      - simulator
```

Create `simulator-config.json` for the simulator container (Modbus TCP example):

```json
{
  "TcpModbusSimulatorConfiguration": {
    "TcpConnection": { "IpAddress": "0.0.0.0", "Port": 5020 },
    "ModbusProtocol": { "SlaveId": 1, "UseModbusAddressing": false, "HoldingRegisterBase": 0 },
    "Simulation": { "GlobalUpdateIntervalSeconds": 1, "EnableValueChanges": true },
    "Sensors": [
      {
        "Name": "TemperatureSensor",
        "Type": "Temperature",
        "StartAddress": 0,
        "RegisterCount": 2,
        "DataType": "Float32",
        "SimulationParams": {
          "MinValue": 18.0, "MaxValue": 32.0,
          "InitialValue": 25.0, "ChangeRate": 0.2, "NoiseLevel": 0.1
        }
      }
    ]
  }
}
```

The simulator host project is at `{REPO_ROOT}/tools/simulator-host/` and supports **all 3 simulator types** based on which config section is present in appsettings.json:
- `TcpModbusSimulatorConfiguration` - Modbus TCP (port 5020)
- `WebSocketSimulatorConfiguration` - WebSocket (port 8080)
- `MqttImageSimulatorConfiguration` - MQTT image publisher

For local development without Docker:
```bash
cd {REPO_ROOT}/tools/simulator-host
dotnet run
# Or with a specific config profile:
dotnet run -- --environment websocket
```

**Remove the `AddHostedService<TcpModbusSimulatorHostedService>()` and `TcpModbusSimulatorConfiguration` section from the app's Program.cs and appsettings.json** when using the standalone simulator.

#### Option A2: Embedded Simulator (legacy, for quick prototyping only)

If the user prefers the embedded approach, the `wedabuilder` template already includes simulator setup in `Program.cs` via `AddHostedService`. See template code for details.

#### Option B: Real Device

Ask the user for host IP address, port, and slave ID. Then **automatically test connectivity**:

```bash
bash {SKILL_DIR}/scripts/test-edge-connection.sh {host} {port}
```

**If the script exits 0**: proceed with the real device config.
**If the script exits 1**: inform the user of the failure and offer two options:
1. Re-enter connection details and retry
2. Switch to the built-in simulator (Option A)

Update `devicecfg.json` -> `DeviceConfigs.{DeviceName}.DeviceCommunication`:
```json
{ "Host": "192.168.1.100", "Port": 502 }
```

**Remove the simulator hosted service registration from Program.cs** if using a real device.

### Phase 4: Cloud Connection Setup

**CRITICAL: The user MUST decide on cloud connectivity before proceeding.**

Ask the user: "Do you have a WedaNode (NATS) server ready, or should we use mock cloud for development?"

#### Option A: Mock Cloud (recommended for development)

**wedabuilder template** - already has `.UseMockCloud()` in Program.cs:
```csharp
var builder = WedaApplication.CreateDefaultBuilder(args)
    .UseMockCloud();
```

**subnode template** - already has `Cloud.Mock()`:
```csharp
using var context = new WedaApplicationContext(options => options.CloudService = Cloud.Mock());
```

Mock cloud logs all operations locally without requiring NATS. It generates deterministic device IDs.

#### Option B: Real WedaNode

Ask for connection details, then **automatically test connectivity** (auto-selects correct binary for current OS/arch):

```bash
# Anonymous
bash {SKILL_DIR}/scripts/test-cloud-connection.sh {REPO_ROOT} nats://server:4222

# With credentials
bash {SKILL_DIR}/scripts/test-cloud-connection.sh {REPO_ROOT} nats://server:4222 --user admin --pass secret

# With token
bash {SKILL_DIR}/scripts/test-cloud-connection.sh {REPO_ROOT} nats://server:4222 --token mytoken
```

**If the script exits 0**: proceed with real cloud config.
**If the script exits 1**: inform the user of the failure and offer two options:
1. Re-enter connection details and retry
2. Switch to mock cloud (Option A)

The `Cloud` factory supports 5 auth strategies:

| Method | Auth Strategy |
|--------|--------------|
| `Cloud.Default()` | Anonymous (localhost:4222) |
| `Cloud.Default(url)` | Anonymous (custom URL) |
| `Cloud.Default(url, username, password)` | UserPassword |
| `Cloud.Default(url, new NatsAuthToken(token))` | Token |
| `Cloud.Default(url, new NatsCredFile(path))` | CredFile (JWT + NKey) |

**For wedabuilder**: Update `systemcfg.json`:
```json
{
  "WedaNode": {
    "Url": "nats://your-server:4222",
    "AuthStrategy": "UserPassword",
    "Username": "your_user",
    "Password": "your_password"
  }
}
```
And **remove `.UseMockCloud()`** from Program.cs.

**For subnode**: Replace `Cloud.Mock()` with the appropriate `Cloud.Default(...)` call.

### Phase 5: Sensor Configuration

Configure sensors that match the actual edge data source (simulator or real device).

**CRITICAL: Sensor register addresses MUST match the edge device exactly.**

For each sensor, collect:
- **Name** (snake_case, e.g., `temperature_sensor`)
- **SensorGroup**: `AI`, `AO`, `DI`, `DO`, `SYS`, `TEMP`, `PWR`
- **RegisterType**: `HoldingRegister`, `InputRegister`, `Coil`, `DiscreteInput`
- **RegisterAddress**: Must match the device/simulator address
- **RegisterCount**: Must match DataType (1 for UInt16/Int16, 2 for Float32/UInt32/Int32, 4 for Float64/UInt64/Int64)
- **DataType**: `UInt16`, `Int16`, `UInt32`, `Int32`, `Float32`, `UInt64`, `Int64`, `Float64`, `String16`
- **Report.Interval**: Reporting interval in ms
- **Report.Unit**: Display unit (e.g., `celsius`, `%RH`, `kW`)

#### wedabuilder: Configure in `devicecfg.json`

```json
{
  "SubNode": {
    "Name": "MyDevice",
    "SubNodeType": "AdamEthernet",
    "Manufacturer": "Advantech",
    "Model": "Demo",
    "SwVersion": "1.0.0"
  },
  "DeviceConfigs": {
    "MyFirstDevice": {
      "Enabled": true,
      "DeviceCommunication": { "Host": "127.0.0.1", "Port": 5020 },
      "Dtdl": { "AutoGenEnabled": true },
      "Properties": { "SlaveId": 1 },
      "Sensors": [
        {
          "Name": "temperature_sensor",
          "SensorGroup": "TEMP",
          "SensorInfo": {
            "DisplayName": "Temperature Sensor",
            "Description": "Main temperature reading",
            "Schema": "double"
          },
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "RegisterCount": 2,
            "DataType": "Float32"
          },
          "Report": {
            "Enabled": true,
            "Interval": 1000,
            "Unit": "celsius"
          }
        }
      ]
    }
  }
}
```

**IMPORTANT**: The `DeviceConfigs` key name (e.g., `"MyFirstDevice"`) MUST match the config key used in `builder.AddDevice<MyFirstDevice>("MyFirstDevice")`.

#### subnode: Configure programmatically

```csharp
var sensor = new ModbusSensorReporturation
{
    Name = "temperature_sensor",
    RegisterAddress = 0,
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
};
sensor.Config.Interval = 1000;
modbusDeviceConfig.AddSensor(sensor);
```

### Phase 6: Verification Checklist

**DO NOT consider the project complete until ALL checks pass.**

#### Understanding Configuration Sections

The `wedabuilder` template's `CreateDefaultBuilder` loads config files into the .NET configuration system under specific sections. When verifying configuration values, use the **full configuration path**:

| Source File | Loaded Under Section | Example Config Path |
|---|---|---|
| `appsettings.json` | _(root)_ | `Serilog:MinimumLevel:Default` |
| `systemcfg.json` | `SystemConfig` | `SystemConfig:WedaNode:Url` |
| `devicecfg.json` | `DeviceConfig` | `DeviceConfig:DeviceConfigs:{DeviceName}:Sensors:0:Name` |
| `customcfg.json` | `CustomConfig` | `CustomConfig:{YourKey}` |

So a sensor configured in `devicecfg.json` at `DeviceConfigs.MyFirstDevice.Sensors[0]` resolves to `DeviceConfig:DeviceConfigs:MyFirstDevice:Sensors:0` in the built configuration. Keep this mapping in mind when debugging config binding issues.

#### Step 1: Validate devicecfg.json

Run the config validator before building:

```bash
bash {SKILL_DIR}/scripts/validate-devicecfg.sh {REPO_ROOT}/apps/{project-name}/devicecfg.json
```

Fix any errors before proceeding.

#### Step 2: Build, run, and verify

Use the automated verification script:

```bash
bash {SKILL_DIR}/scripts/verify-project.sh {REPO_ROOT}/apps/{project-name}
```

This script will:
1. Build the project
2. Run it for 10 seconds
3. Check logs for success/error markers (simulator, mock cloud, device, sensors)
4. Report pass/fail

If automated verification fails, debug manually with verbose logging:

```bash
cd {REPO_ROOT}/apps/{project-name}
dotnet run -- --Serilog:MinimumLevel:Default=Debug
```

#### Manual verification markers

- **Simulator**: `TcpModbusSimulator started on 127.0.0.1:5020`
- **Mock cloud**: Look for the mock cloud banner in logs
- **Device**: `Data received from device`
- **Sensors**: `temperature_sensor: 25.3`

### Phase 7: NATS Web Dashboard (Optional)

After verification passes and the project is using a **real WedaNode** (not mock cloud), offer to set up the NATS Web Dashboard for real-time telemetry monitoring.

Ask the user: "Would you like to set up the NATS Web Dashboard to monitor your device telemetry in real-time?"

If yes, proceed with the following steps:

#### Step 1: Find the Device ID

After the SubNode registers with WedaNode, check the registration cache:

```bash
cat {REPO_ROOT}/apps/{project-name}/.weda/subnode.registration.json | jq .natsTopicAssignments.telemetryTopic
# Example output: "eco1j.weda.297948673633943552.telemetry"
```

Extract the device ID from the topic (e.g., `297948673633943552`).

#### Step 2: Configure the Dashboard

The dashboard project is at `{REPO_ROOT}/tools/nats-web-dashboard/`. Create or update `appsettings.json`:

```json
{
  "Dashboard": {
    "Nats": {
      "Url": "{same NATS URL as systemcfg.json}",
      "AuthStrategy": "{same auth strategy}",
      "Username": "{if UserPassword}",
      "Password": "{if UserPassword}"
    },
    "Subject": "eco1j.weda.{deviceId}.>",
    "Port": 5050
  }
}
```

The `Dashboard:Nats` section uses `NatsConnectionSettings` from the SDK, so all auth strategies are supported:

| Field | Description |
|---|---|
| `Url` | NATS server URL (`nats://` prefix auto-added if omitted) |
| `AuthStrategy` | `None`, `UserPassword`, `Token`, `CredFile`, `TlsCert` |
| `Username` / `Password` | For `UserPassword` auth |
| `Token` | For `Token` auth |
| `CredFile` | Path to `.creds` file for `CredFile` auth |

**IMPORTANT**: The NATS connection settings should match the `systemcfg.json` used by the SubNode project. Use `>` (all subjects) or `eco1j.weda.{deviceId}.>` to filter by device.

Configuration can also be overridden via environment variables or CLI arguments:

```bash
# Environment variables
export Dashboard__Nats__Url=192.168.1.100:4222
export Dashboard__Subject="eco1j.weda.12345.>"

# CLI arguments
dotnet run -- --Dashboard:Nats:Url=192.168.1.100:4222 --Dashboard:Subject="eco1j.weda.12345.>"
```

#### Step 3: Launch the Dashboard

Ask the user: "Do you want me to launch the dashboard now?"

If yes, start the dashboard:

```bash
cd {REPO_ROOT}/tools/nats-web-dashboard
dotnet run
```

Then inform the user to open http://localhost:5050 in their browser.

The dashboard provides:
- **Sensor Telemetry** - Live sensor values with min/max/avg statistics
- **Device Health** - CPU, memory, health status with progress bars
- **Sensor Trend Chart** - Canvas line chart (last 120 samples)
- **Message Log** - Raw NATS message stream classified by type
- **Stats Bar** - Message count, rate (msg/s), last update time

### Phase 8: Customization (Optional)

After verification passes, offer these customization options:

1. **Rename device class** from `MyFirstDevice` to a meaningful name
2. **Add transforms** (calibration, unitconversion, chunking) to sensor reports
3. **Add DSP filters** (movingaverage, kalman, relu)
4. **Add more sensors** - ensure register addresses don't overlap
5. **Add event handlers** - DataReceived, ValueChanged, ConfigUpdated, CommandReceived

## Configuration File Reference

The `wedabuilder` template uses 4 configuration files (loaded in order by `CreateDefaultBuilder`). Each file's content is mounted under a **parent section** in the built configuration:

| File | Parent Section | Built Config Paths | Purpose |
|------|---|---|---|
| `appsettings.json` | _(root)_ | `Serilog:*` | Logging config (NOT cloud-synced) |
| `systemcfg.json` | `SystemConfig` | `SystemConfig:WedaNode:Url`, `SystemConfig:WedaNode:AuthStrategy`, ... | NATS connection settings |
| `devicecfg.json` | `DeviceConfig` | `DeviceConfig:SubNode:Name`, `DeviceConfig:DeviceConfigs:{Key}:Sensors:0:Name`, ... | Device metadata + sensor config |
| `customcfg.json` | `CustomConfig` | `CustomConfig:{YourKey}` | User-defined custom config |

For example, `devicecfg.json` contains `"SubNode"` and `"DeviceConfigs"` at the top level, but in the built configuration they resolve to `DeviceConfig:SubNode` and `DeviceConfig:DeviceConfigs` respectively.

Override any config via CLI args: `dotnet run -- --key=value`

## Important Rules

- **Never skip edge or cloud verification** - both must be confirmed working
- **Register addresses must match** between simulator config and sensor config
- **RegisterCount must match DataType**: UInt16/Int16=1, Float32/UInt32/Int32=2, Float64/UInt64/Int64=4
- **ConfigKey consistency**: `builder.AddDevice<T>("key")` must match `DeviceConfigs.key` in devicecfg.json
- **SubNodeType must be one of**: `AdamEthernet`, `SerialDevice`, `DaqDevice`, `SystemMonitor`, `CustomDevice`
- Use PascalCase for class names, kebab-case for project directories
- Reference existing examples in `examples/` and `tutorials/` for patterns