# Quick Start Guide

Get started with Weda SubNode SDK in minutes. This guide will walk you through creating your first custom IoT device.

## Prerequisites

- .NET 9.0 SDK
- A Modbus TCP simulator or physical device (we'll use the built-in simulator)
- Basic knowledge of C#

## Step 1: Install the Template

### Option A: Use the Installation Script (Recommended)

Run the smart installation script that automatically handles version detection and updates:

```bash
./install-template.sh
```

The script will:
- Automatically extract version from `common.props`
- Detect if template is already installed
- Offer options to ignore/replace/update based on installed version
- Build and install the template package

### Option B: Manual Installation

Alternatively, install manually:

```bash
cd templates
dotnet pack -c Release
dotnet new install ./bin/Release/Weda.SubNode.Templates.1.0.0.nupkg
cd ..
```

### Verify Installation

Check that the template is installed:

```bash
dotnet new list | grep subnode
```

You should see:
```
Weda SubNode Custom Device  subnode  [C#]  Console/IoT/Weda/SubNode/Custom
```

### Uninstall Template (Optional)

If you need to uninstall the template:

```bash
dotnet new uninstall Weda.SubNode.Templates
```

To reinstall or update to a new version, simply run the installation script again:

```bash
./install-template.sh
```

The script will detect the installed version and offer options to update or replace.

## Step 2: Start the Modbus Simulator

Open a terminal and start the built-in Modbus TCP simulator:

```bash
cd examples/ModbusSimulatorExample
dotnet run
```

The simulator will start on `127.0.0.1:5020` and simulate temperature, humidity, and pressure sensors.

Keep this terminal running.

## Step 3: Create Your First Device

Open a new terminal and create your custom device:

```bash
cd examples
dotnet new subnode -n MyFirstDevice
cd MyFirstDevice
```

This creates a project with:
- `MyFirstDevice.cs` - Your custom device class inheriting from ModbusDevice
- `Program.cs` - Application entry point
- `appsettings.json` - Device configuration

## Step 4: Configure Your Device

Open `appsettings.json` and review the default configuration:

```json
{
  "Devices": [
    {
      "Name": "MyFirstDevice",
      "DeviceUid": "my-first-device-001",
      "GroupId": "demo",
      "Communication": {
        "Host": "127.0.0.1",
        "Port": 5020,
        "SlaveId": 1
      },
      "Sensors": [
        {
          "Name": "temperature.sensor",
          "ResourceId": "temp-001",
          "Dtmi": "dtmi:advantech:EdgeSync:Temperature;1",
          "ReadAddress": 0,
          "ReadCount": 2,
          "DataType": "Float32"
        }
      ]
    }
  ]
}
```

The configuration is already set to connect to our simulator - no changes needed!

## Step 5: Build and Run

Build and run your device:

```bash
dotnet build
dotnet run
```

You should see output like:

```
[15:04:12.345 INF] Starting MyFirstDevice application...
[15:04:12.567 INF] Device initialized successfully
[15:04:12.789 INF] Device started successfully. Press Ctrl+C to stop...
[15:04:13.012 DBG] MyFirstDevice: Data received, Count=1
[15:04:13.234 INF] Temperature: 25.42°C
```

Congratulations! Your device is now reading telemetry from the Modbus simulator.

Press `Ctrl+C` to stop.

## Step 6: Customize Your Device

Now let's add custom logic. Open `MyFirstDevice.cs`:

### Example: Add Temperature Alert

Modify the `OnDataReceived` method to add temperature monitoring:

```csharp
private void OnDataReceived(object? sender, DataReceivedEvent e)
{
    _logger.LogDebug("MyFirstDevice: Data received, Count={Count}", e.Data.Count);

    // Log temperature values with alerts
    var tempSensor = Configuration.Sensors.FirstOrDefault(s => s.Name.Contains("temperature"));
    if (tempSensor != null)
    {
        var measure = e.Data.FirstOrDefault(m => m.ResourceId == tempSensor.ResourceId);
        if (measure?.ValueObject is double value)
        {
            // Add threshold-based alerts
            var level = value switch
            {
                > 30.0 => "🔥 CRITICAL",
                > 25.0 => "⚠️  WARNING",
                _ => "✓ NORMAL"
            };

            _logger.LogInformation("🌡️  Temperature: {Value:F2}°C - {Level}", value, level);
        }
    }
}
```

Rebuild and run:

```bash
dotnet build
dotnet run
```

Now you'll see temperature alerts based on the value ranges:

```
[15:10:23.456 INF] 🌡️  Temperature: 26.34°C - ⚠️  WARNING
[15:10:24.567 INF] 🌡️  Temperature: 31.12°C - 🔥 CRITICAL
[15:10:25.678 INF] 🌡️  Temperature: 24.89°C - ✓ NORMAL
```

## Next Steps

### Add More Sensors

Edit `appsettings.json` to add humidity and pressure sensors:

```json
{
  "Sensors": [
    {
      "Name": "temperature.sensor",
      "ResourceId": "temp-001",
      "Dtmi": "dtmi:advantech:EdgeSync:Temperature;1",
      "ReadAddress": 0,
      "ReadCount": 2,
      "DataType": "Float32"
    },
    {
      "Name": "humidity.sensor",
      "ResourceId": "humidity-001",
      "Dtmi": "dtmi:advantech:EdgeSync:Humidity;1",
      "ReadAddress": 2,
      "ReadCount": 2,
      "DataType": "Float32"
    },
    {
      "Name": "pressure.sensor",
      "ResourceId": "pressure-001",
      "Dtmi": "dtmi:advantech:EdgeSync:Pressure;1",
      "ReadAddress": 4,
      "ReadCount": 2,
      "DataType": "Float32"
    }
  ]
}
```

### Explore Advanced Examples

Check out the advanced examples in the `examples/` directory:

- **[AdvancedModbusExample](examples/AdvancedModbusExample/README.md)** - Power monitoring with energy calculation
- **[ModbusDeviceExample](examples/ModbusDeviceExample/README.md)** - Using TcpModbusDeviceConfiguration
- **[ModbusDeviceConfiguration](examples/ModbusDeviceConfiguration/README.md)** - Programmatic configuration examples
- **[WedaApplicationExample](examples/WedaApplicationExample/README.md)** - Full application with cloud integration

### Learn More

- [README.md](README.md) - Complete SDK documentation
- [README_zh.md](README_zh.md) - 完整 SDK 文檔（中文）
- [Templates README](templates/README.md) - Template usage guide

## Troubleshooting

### Cannot connect to simulator

**Problem**: `Connection refused` or `Connection timeout`

**Solution**:
1. Make sure `ModbusSimulatorExample` is running
2. Check the port in `appsettings.json` matches the simulator (default: 5020)
3. Verify firewall settings allow local connections

### Template not found

**Problem**: `dotnet new subnode` says template not found

**Solution**:
```bash
# Reinstall the template
cd templates
dotnet pack -c Release
dotnet new uninstall Weda.SubNode.Templates
dotnet new install ./bin/Release/Weda.SubNode.Templates.1.0.0.nupkg
```

### Build errors

**Problem**: Build fails with reference errors

**Solution**:
```bash
# Make sure you created the project in the examples/ directory
cd examples
dotnet new subnode -n MyFirstDevice

# The template needs to reference ../../src/Weda.SubNode.Core
```

## What's Next?

Now that you have a working SubNode:

1. **Add business logic** - Process telemetry data with custom algorithms
2. **Implement commands** - Handle remote commands from cloud
3. **Add cloud integration** - Connect to Azure IoT Hub or AWS IoT Core
4. **Deploy to edge devices** - Run on Raspberry Pi, Industrial PCs, etc.

Happy coding! 🚀
