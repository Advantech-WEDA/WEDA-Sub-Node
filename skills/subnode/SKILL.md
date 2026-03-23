---
name: subnode
description: |
  SubNode project scaffolding tool for the Weda IoT edge computing framework. Creates complete, production-ready SubNode projects with proper structure, configuration, and device implementations.

  TRIGGER THIS SKILL when users:
  - Ask to create a new SubNode project or IoT device project
  - Want to scaffold a Modbus TCP, MQTT, WebSocket, or custom protocol device
  - Mention "subnode", "weda", "edge device", "IoT project", or similar
  - Need to set up device communication (Modbus, MQTT, WebSocket)
  - Ask about creating protocol parsers or device implementations
  - Want to add a new device to the SubNode framework

  Even if the user doesn't explicitly say "subnode", trigger this skill if they're working in the edge_subnode repository and want to create a new device or project.
---

# SubNode Project Scaffolding

You are a specialized assistant for creating SubNode IoT edge computing projects. Guide users through an interactive process to build complete, production-ready projects.

## Overview

SubNode is a modular IoT edge computing framework supporting:
- **Request-Response**: Synchronous polling (Modbus TCP/RTU, OPC-UA)
- **Pub/Sub**: Asynchronous messaging (MQTT, NATS)
- **Streaming**: Bidirectional data flow (WebSocket, gRPC)

## Interactive Workflow

When the user invokes this skill, gather information step by step using AskUserQuestion. Don't ask all questions at once - gather in logical groups.

### Phase 1: Basic Information

First, collect basic project information:

1. **Project Name** (ask as free text question or derive from context)
   - Will be used for directory name and namespace
   - Example: `power-meter`, `temperature-sensor`, `air-quality`

2. **Device Type** - Ask which communication pattern:
   - **Modbus TCP** - For industrial I/O modules, PLCs, power meters
   - **MQTT/iSensing** - For smart sensors publishing JSON data
   - **WebSocket Streaming** - For real-time bidirectional communication
   - **Custom Protocol** - For implementing a custom IProtocolParser

### Phase 2: Connection Settings

Based on the device type, ask appropriate connection details:

**For Modbus TCP:**
- Host IP address (e.g., `192.168.1.100`)
- Port (default: `502`)
- Slave ID (default: `1`)
- Polling interval in milliseconds (default: `1000`)

**For MQTT/iSensing:**
- Broker URL (e.g., `mqtt://localhost:1883`)
- Topic pattern (e.g., `sensors/+/telemetry`)
- Client ID (auto-generate if not specified)
- QoS level (0, 1, or 2)

**For WebSocket:**
- WebSocket URL (e.g., `ws://localhost:8080/stream`)
- Reconnection settings

**For Custom Protocol:**
- Base communication type (TCP, Serial, HTTP)
- Connection parameters

### Phase 3: Sensor Configuration

Ask about the sensors to monitor:

1. How many sensors?
2. For each sensor (or use a pattern):
   - Name (e.g., `Temperature`, `Humidity`, `Power`)
   - Sensor Group: `AI` (Analog Input), `AO` (Analog Output), `DI` (Digital Input), `DO` (Digital Output), `SYS`, `TEMP`, `PWR`
   - Unit (e.g., `°C`, `%RH`, `kW`)

   **For Modbus sensors additionally ask:**
   - Register address
   - Register type: `HoldingRegister`, `InputRegister`, `Coil`, `DiscreteInput`
   - Data type: `UInt16`, `Int16`, `Float32`, `Float64`
   - Byte order: `BigEndian` or `LittleEndian`

### Phase 4: Data Processing (Optional)

Ask if the user needs data processing:

1. **Transforms** - Value transformations:
   - Linear Transform: `y = scale * x + offset`
   - Unit Conversion
   - Custom formula

2. **DSP Filters** - Signal processing:
   - Moving Average
   - Kalman Filter
   - Low Pass Filter
   - Change Detection (deadband)

### Phase 5: Additional Options

- Enable cloud telemetry? (default: yes)
- Telemetry reporting interval (default: `5000ms`)
- Enable event tracking for debugging?

## Project Structure to Generate

Create the following structure in `apps/{project-name}/`:

```
apps/{project-name}/
├── Program.cs                 # Application entry point
├── {DeviceName}Device.cs      # Device implementation
├── appsettings.json           # Main configuration
├── {project-name}.csproj      # Project file
└── Properties/
    └── launchSettings.json    # Debug settings
```

## Code Templates

### Program.cs Template

```csharp
using Serilog;
using Weda.SubNode.Host;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting {ProjectName}...", "{ProjectName}");

    var builder = WedaApplication.CreateDefaultBuilder(args);

    builder.AddDevice<{DeviceName}Device>("{DeviceConfigKey}");

    var app = builder.Build();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

### Device Class Templates

**For Modbus TCP (`TcpModbusDevice`):**

```csharp
using Weda.SubNode.Devices;
using Weda.SubNode.Host;

namespace {Namespace};

public class {DeviceName}Device : TcpModbusDevice
{
    public {DeviceName}Device(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var measure in e.Telemetry)
        {
            Logger.LogDebug("Received {SensorName}: {Value} {Unit}",
                measure.Name, measure.Value, measure.Unit);
        }
    }

    protected override Task OnAfterConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct)
    {
        Logger.LogInformation("Configuration updated");
        return Task.CompletedTask;
    }
}
```

**For MQTT/iSensing (`ISensingDevice`):**

```csharp
using Weda.SubNode.Devices;
using Weda.SubNode.Host;

namespace {Namespace};

public class {DeviceName}Device : ISensingDevice
{
    public {DeviceName}Device(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var measure in e.Telemetry)
        {
            Logger.LogDebug("Received {SensorName}: {Value} {Unit}",
                measure.Name, measure.Value, measure.Unit);
        }
    }
}
```

**For WebSocket Streaming (`StreamingDeviceBase`):**

```csharp
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Host;

namespace {Namespace};

public class {DeviceName}Device : StreamingDeviceBase<{DeviceName}StreamingParser>
{
    public {DeviceName}Device(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
        EnableDataReceivedTracking = true;
    }

    protected override {DeviceName}StreamingParser CreateProtocolParser()
    {
        return new {DeviceName}StreamingParser(Configuration, Logger);
    }
}
```

**For Custom Protocol:**

```csharp
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Host;

namespace {Namespace};

public class {DeviceName}Device : RequestResponseDeviceBase<{DeviceName}ProtocolParser>
{
    public {DeviceName}Device(IWedaApplicationContext context, string configKey)
        : base(context, configKey)
    {
        EnableDataReceivedTracking = true;
    }

    protected override {DeviceName}ProtocolParser CreateProtocolParser()
    {
        return new {DeviceName}ProtocolParser(Configuration, Logger);
    }
}

// TODO: Implement your custom protocol parser
public class {DeviceName}ProtocolParser : IRequestResponseProtocolParser
{
    // Implement protocol-specific logic here
}
```

### appsettings.json Template

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" }
    ]
  },
  "SubNode": {
    "Name": "{SubNodeName}",
    "SubNodeType": "{SubNodeType}",
    "Manufacturer": "Custom",
    "Model": "{DeviceName}",
    "SwVersion": "1.0.0",
    "AutoGenEnabled": true
  },
  "DeviceConfigs": [
    {
      "ConfigKey": "{DeviceConfigKey}",
      "Enabled": true,
      "DeviceCommunication": {
        // Connection settings based on device type
      },
      "Properties": {
        // Protocol-specific properties
      },
      "ConnectionSettings": {
        "RetryCount": 3,
        "RetryDelayMs": 1000,
        "TimeoutMs": 5000
      },
      "Periods": {
        "TelemetryReportIntervalMs": 5000,
        "HealthReportIntervalMs": 60000
      },
      "Sensors": [
        // Sensor definitions
      ]
    }
  ]
}
```

### .csproj Template

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>{Namespace}</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Weda.SubNode.Host/Weda.SubNode.Host.csproj" />
    <ProjectReference Include="../../src/Weda.SubNode.Devices/Weda.SubNode.Devices.csproj" />
    <ProjectReference Include="../../src/Weda.SubNode.Cloud/Weda.SubNode.Cloud.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Serilog" Version="4.2.0" />
    <PackageReference Include="Serilog.Extensions.Logging" Version="9.0.0" />
    <PackageReference Include="Serilog.Settings.Configuration" Version="9.0.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

## Sensor Configuration Examples

### Modbus Sensor

```json
{
  "ResourceId": "{auto-generate-uuid}",
  "Name": "Temperature",
  "Dtmi": "dtmi:com:example:Temperature;1",
  "SensorGroup": "TEMP",
  "Parameters": {
    "RegisterAddress": 0,
    "RegisterType": "HoldingRegister",
    "DataType": "Float32",
    "ByteOrder": "BigEndian"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000,
    "Transforms": [
      {
        "Type": "linear",
        "Parameters": {
          "Scale": 0.1,
          "Offset": 0
        }
      }
    ],
    "DspFilters": [
      {
        "Type": "movingaverage",
        "Parameters": {
          "WindowSize": 5
        }
      }
    ]
  }
}
```

### MQTT/iSensing Sensor

```json
{
  "ResourceId": "{auto-generate-uuid}",
  "Name": "Humidity",
  "Dtmi": "dtmi:com:example:Humidity;1",
  "SensorGroup": "AI",
  "Parameters": {
    "JsonPath": "$.sensors.humidity",
    "Unit": "%RH"
  },
  "Report": {
    "Enabled": true,
    "Interval": 5000
  }
}
```

## Transform and DSP Filter Reference

### Available Transforms

| Type | Parameters | Description |
|------|------------|-------------|
| `linear` | `Scale`, `Offset` | Linear transformation: y = scale * x + offset |
| `polynomial` | `Coefficients` | Polynomial transformation |
| `lookup` | `Table` | Lookup table interpolation |

### Available DSP Filters

| Type | Parameters | Description |
|------|------------|-------------|
| `movingaverage` | `WindowSize` | Simple moving average |
| `kalman` | `ProcessNoise`, `MeasurementNoise` | Kalman filter |
| `lowpass` | `CutoffFrequency`, `SampleRate` | Low pass filter |
| `deadband` | `Threshold` | Change detection with deadband |

## After Scaffolding

After creating the project, remind the user:

1. **Build the project:**
   ```bash
   cd apps/{project-name}
   dotnet build
   ```

2. **Run the project:**
   ```bash
   dotnet run
   ```

3. **Configure cloud connection** (if needed):
   - Add NATS connection settings to appsettings.json
   - Configure DMA (Device Management Agent) endpoint

4. **Customize the device:**
   - Add additional event handlers
   - Implement custom protocol parsing (for Custom type)
   - Add more sensors as needed

## Important Notes

- Always generate UUIDs for ResourceId fields using `Guid.NewGuid().ToString()`
- Use PascalCase for class names, kebab-case for project directories
- Ensure namespace matches directory structure
- Reference existing examples in `examples/` for patterns
