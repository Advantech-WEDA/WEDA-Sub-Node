---
title: Modbus Register Scanner
category: modbus
order: 1
parent: null
related:
  - path: device_lifecycle.md
    title: Device Lifecycle
  - path: how_to_start.md
    title: How to Start
author: Rain Hu
version: 0.0.1
date: 2025-10-15
description: Modbus Register Scanner Guide - Automatically detect Modbus device register configurations
tags: [Modbus, Scanner, Protocol, Device Discovery]
---

# Modbus Register Scanner

The Modbus Scanner is a utility that helps you discover available sensors on a Modbus device **before** writing the configuration. This is especially useful when working with new devices where you don't know the register addresses or data types.

## Features

- **Auto-discovery**: Scan a range of Modbus holding registers
- **Multi-type parsing**: Automatically tries parsing as UInt16, Int16, UInt32, Int32, Float32, Float64
- **Configuration suggestions**: Generates ready-to-use sensor configuration JSON
- **Configurable**: Control scan range, delay, and data types to test
- **Detailed logging**: See raw hex/decimal values and parsed results

## Quick Start

### 1. Basic Usage

```csharp
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Core.Protocols.Modbus;

// Create minimal device configuration (no sensors needed for scanning)
var config = new DeviceConfiguration
{
    DeviceName = "Scanner Test",
    DeviceType = "ModbusTCP",
    Communication = new Dictionary<string, object>
    {
        ["Host"] = "192.168.1.100",
        ["Port"] = 502,
        ["SlaveId"] = 1
    },
    Sensors = []  // Empty - we'll discover via scanning
};

// Create device and connect
var communication = WedaFactory.Communication.Tcp.Create("192.168.1.100", 502);
var device = new ModbusDevice(config, communication, WedaFactory.Cloud.Null);
await device.InitializeAsync();

// Scan registers
var results = await device.ScanRegistersAsync();

// Generate configuration suggestions
var suggestions = device.GenerateSensorSuggestions(results);
```

### 2. Run the Example

We provide a complete example in `ProgramWithScan.cs`:

```bash
cd examples/Wise4012Example

# Update the IP address in ProgramWithScan.cs first
dotnet run --project Wise4012Example.csproj -- scan
```

Or use the Program.cs directly:
```bash
# Run the scanner version
dotnet run -c Release
```

## Configuration Options

### Scan Configuration

```csharp
var scanConfig = new ModbusScanConfig
{
    StartAddress = 0,           // Starting register address
    EndAddress = 99,            // Ending register address
    RegistersPerScan = 4,       // Registers to read per scan (4 for Float64 support)
    DelayBetweenScans = 100,    // Delay in ms between scans
    DataTypesToTest = [         // Data types to attempt parsing
        ModbusDataType.UInt16,
        ModbusDataType.Int16,
        ModbusDataType.UInt32,
        ModbusDataType.Int32,
        ModbusDataType.Float32,
        ModbusDataType.Float64
    ]
};

var results = await device.ScanRegistersAsync(scanConfig);
```

### Scan Strategies

**Quick Scan (0-9)**: Fast initial discovery
```csharp
var quickScan = new ModbusScanConfig
{
    StartAddress = 0,
    EndAddress = 9,
    DelayBetweenScans = 50
};
```

**Full Scan (0-99)**: Comprehensive discovery
```csharp
var fullScan = new ModbusScanConfig
{
    StartAddress = 0,
    EndAddress = 99,
    DelayBetweenScans = 100
};
```

**Targeted Scan**: Scan specific range
```csharp
var targetedScan = new ModbusScanConfig
{
    StartAddress = 40000,
    EndAddress = 40010,
    RegistersPerScan = 2  // Only read 2 registers if you know they're 16/32-bit
};
```

## Output Format

### Console Output Example

```
================================================================================
Modbus Register Scan Results
================================================================================

Address 000 (0x0000):
  Raw Hex:  [0x4248, 0x0000, 0x0000, 0x0000]
  Raw Dec:  [16968, 0, 0, 0]
  UInt16    : 16968
  Int16     : 16968
  Float32   : 50.0

Address 002 (0x0002):
  Raw Hex:  [0x4248, 0x0000, 0x0000, 0x0000]
  Raw Dec:  [16968, 0, 0, 0]
  UInt16    : 16968
  Int16     : 16968
  Float32   : 75.5

================================================================================
Summary: 2 registers with non-zero data
================================================================================

Suggested Sensor Configurations:
Copy the following to your appsettings.json:

"Sensors": [
  {
    "Name": "sensor.0",
    "Dtmi": "dtmi:advantech:EdgeSync:Sensor;1",
    "Parameters": {
      "RegisterType": "HoldingRegister",
      "RegisterAddress": 0,
      "RegisterCount": 2,
      "DataType": "Float32"
    },
    "Config": {
      "Enabled": true,
      "Interval": 1000
    }
  },
  {
    "Name": "sensor.2",
    "Dtmi": "dtmi:advantech:EdgeSync:Sensor;1",
    "Parameters": {
      "RegisterType": "HoldingRegister",
      "RegisterAddress": 2,
      "RegisterCount": 2,
      "DataType": "Float32"
    },
    "Config": {
      "Enabled": true,
      "Interval": 1000
    }
  }
]
```

### Programmatic Access

```csharp
var results = await device.ScanRegistersAsync();

foreach (var result in results.Where(r => r.Success))
{
    Console.WriteLine($"Address: {result.Address}");
    Console.WriteLine($"Raw: {string.Join(", ", result.RawValues)}");

    foreach (var (dataType, value) in result.ParsedValues)
    {
        Console.WriteLine($"  {dataType}: {value}");
    }
}
```

## Use Cases

### 1. New Device Discovery

When working with a new Modbus device, scan to discover available registers:

```csharp
// Do a full scan
var results = await device.ScanRegistersAsync(new ModbusScanConfig
{
    StartAddress = 0,
    EndAddress = 99
});

// Get configuration suggestions
var suggestions = device.GenerateSensorSuggestions(results);

// Copy the output to appsettings.json
```

### 2. Verify Device Configuration

Check if your device is responding at expected addresses:

```csharp
// Scan known address range
var results = await device.ScanRegistersAsync(new ModbusScanConfig
{
    StartAddress = 0,
    EndAddress = 10
});

// Check specific address
var address0 = results.FirstOrDefault(r => r.Address == 0);
if (address0?.Success == true)
{
    Console.WriteLine($"Address 0 has data: {address0.RawValues[0]}");
}
```

### 3. Data Type Detection

Not sure if your sensor is Float32 or UInt16? Scan and compare:

```csharp
var results = await device.ScanRegistersAsync();
var address0 = results.First(r => r.Address == 0);

Console.WriteLine($"As Float32: {address0.ParsedValues[ModbusDataType.Float32]}");
Console.WriteLine($"As UInt16: {address0.ParsedValues[ModbusDataType.UInt16]}");
Console.WriteLine($"As Int16: {address0.ParsedValues[ModbusDataType.Int16]}");
```

## Troubleshooting

### No Data Found

If scan shows no non-zero registers:

1. **Check connection**: Verify IP address, port, and SlaveId
2. **Device status**: Ensure device is powered on and sending data
3. **Expand range**: Try scanning 0-99 instead of 0-9
4. **Check function code**: Scanner uses 0x03 (Read Holding Registers)
5. **Firewall**: Check network connectivity

### Timeout Errors

If scans are timing out:

1. **Increase delay**: Use `DelayBetweenScans = 200` or higher
2. **Reduce range**: Scan smaller ranges (e.g., 10 addresses at a time)
3. **Check network**: Ensure stable connection to device

### Wrong Data Types

If parsed values look incorrect:

1. **Verify byte order**: Some devices use different endianness
2. **Check documentation**: Refer to device manual for register map
3. **Test different types**: Try all data types to see which makes sense
4. **Register count**: Ensure you're reading enough registers for the data type

## Advanced Usage

### Custom Scanner

For more control, use `ModbusScanner` directly:

```csharp
using Weda.SubNode.Core.Protocols.Modbus;

var communication = WedaFactory.Communication.Tcp.Create("192.168.1.100", 502);
await communication.ConnectAsync();

var scanner = new ModbusScanner(communication, slaveId: 1, logger);
var results = await scanner.ScanHoldingRegistersAsync(scanConfig);

scanner.PrintScanResults(results, logger);
```

### Export to JSON

Generate sensor configuration JSON programmatically:

```csharp
var suggestions = device.GenerateSensorSuggestions(results);
var json = JsonSerializer.Serialize(suggestions, new JsonSerializerOptions
{
    WriteIndented = true
});

File.WriteAllText("sensor-config.json", json);
```

## Data Type Reference

| Data Type | Registers | Size | Example Use Case |
|-----------|-----------|------|------------------|
| UInt16    | 1         | 16-bit unsigned | Status flags, small counters |
| Int16     | 1         | 16-bit signed | Temperature (×10), small values |
| UInt32    | 2         | 32-bit unsigned | Large counters, timestamps |
| Int32     | 2         | 32-bit signed | Large signed values |
| Float32   | 2         | IEEE 754 single | Temperature, voltage, current |
| Float64   | 4         | IEEE 754 double | High-precision measurements |

## Best Practices

1. **Start Small**: Begin with a quick scan (0-9) before doing full scan
2. **Add Delay**: Use at least 100ms delay between scans to avoid overwhelming device
3. **Verify Values**: Cross-reference scan results with device documentation
4. **Test Read**: After generating config, test reading to ensure values are correct
5. **Document**: Keep notes about which addresses correspond to which physical sensors
