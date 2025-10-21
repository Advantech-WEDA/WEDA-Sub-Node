# How to Test Device in Sandbox Mode

Testing your device without physical hardware is essential during development. This guide shows you how to test devices in sandbox mode using simulators and mock services.

## Overview

Sandbox testing allows you to:
- Develop and test without physical devices
- Simulate various device scenarios and error conditions
- Test data processing pipelines
- Validate cloud integration
- Run automated tests

## Method 1: Using Modbus Simulator

### Step 1: Install Modbus Simulator

You can use several Modbus simulators:

**Option A: ModbusPal (Cross-platform, Java-based)**
```bash
# Download from: https://sourceforge.net/projects/modbuspal/
java -jar ModbusPal.jar
```

**Option B: pyModSlave (Python-based)**
```bash
pip install pymodslave
pymodslave --port 502
```

**Option C: Use the built-in simulator example**
```bash
cd /path/to/weda_subdevice/examples/ModbusSimulatorExample
dotnet run
```

### Step 2: Configure Simulator

For ModbusPal:
1. Add a new Modbus slave (Device ID: 1)
2. Add holding registers (e.g., addresses 0-10)
3. Set initial values or use automation to simulate changing values
4. Start the simulator on port 502

For the built-in simulator:
```csharp
// The ModbusSimulatorExample creates a simple Modbus server
// with predefined registers that change over time
```

### Step 3: Point Your Device to Simulator

Update `appsettings.json`:

```json
{
  "DeviceConfigs": {
    "MyDevice": {
      "ConnectionSettings": {
        "Host": "127.0.0.1",  // localhost
        "Port": 502,
        "SlaveId": 1
      }
    }
  }
}
```

### Step 4: Run and Test

```bash
dotnet run
```

## Method 2: Using Mock Cloud Service

The SDK includes a `NullCloudService` for testing without a real cloud backend.

### Step 1: Configure Mock Cloud

In your `Program.cs`, use the mock cloud service:

```csharp
using var context = new WedaApplicationContext(options =>
{
    options.LoggerFactory = loggerFactory;
    options.NatsUrl = natsUrl;
    options.UseMockCloud = true;  // Use mock cloud service
});
```

Or use the builder pattern for more control:

```csharp
var builder = WedaApplication.CreateBuilder(args);

// Use mock cloud service for testing
builder.UseCloudService<NullCloudService>();

var app = builder.Build();
await app.RunAsync();
```

### What NullCloudService Does

The `NullCloudService` provides a no-op implementation:
- Device registration always succeeds (returns mock IDs)
- Telemetry sending logs data but doesn't transmit
- Health reports are logged locally
- No network errors

Perfect for:
- Local development
- Unit testing
- CI/CD pipelines
- Debugging data processing logic

## Method 3: Using Modbus Scanner for Discovery

If you have access to a device but don't know its configuration, use the Modbus Scanner:

### Step 1: Create Scanner Configuration

```json
{
  "ModbusScan": {
    "Host": "192.168.1.100",
    "Port": 502,
    "SlaveId": 1,
    "RegisterRanges": [
      {
        "Type": "HoldingRegister",
        "StartAddress": 0,
        "EndAddress": 100
      }
    ]
  }
}
```

### Step 2: Run Scanner

```csharp
var scanner = new ModbusScanner(loggerFactory);
var result = await scanner.ScanAsync(scanConfig);

// Generate report
var report = scanner.GenerateMarkdownReport(result);
File.WriteAllText("scan_report.md", report);
```

Or use the built-in example:

```bash
cd examples/Wise4012Example
# Update appsettings.json with your device IP
dotnet run
# Check the generated modbus_scan_report_*.md
```

### Step 3: Use Generated Configuration

The scanner creates a complete device configuration you can copy into your `appsettings.json`:

```json
{
  "Sensors": [
    {
      "ResourceId": "sensor_0",
      "Name": "Holding Register 0",
      "RegisterAddress": 0,
      "RegisterType": "HoldingRegister",
      "DataType": "Int16",
      "Scale": 1.0,
      "Offset": 0.0
    }
    // ... more sensors
  ]
}
```

## Method 4: Using Test-Driven Development

### Step 1: Create Test Project

```bash
dotnet new xunit -n MyDevice.Tests
cd MyDevice.Tests
dotnet add reference ../MyDevice/MyDevice.csproj
dotnet add package Weda.SubNode.TestBase
```

### Step 2: Write Device Tests

```csharp
using Weda.SubNode.TestBase;
using Weda.SubNode.Abstractions.Events;
using Xunit;

public class MyDeviceTests
{
    [Fact]
    public async Task Device_Should_Process_Data_Correctly()
    {
        // Arrange
        var context = new MockApplicationContext();
        var config = new DeviceConfigurationBuilder()
            .WithId("test-device")
            .WithSensor("temp", "Temperature", 0)
            .Build();

        var device = new MyFirstDevice(context, config);

        // Track received data
        List<DataReceivedEvent> receivedEvents = new();
        device.DataReceived += (s, e) => receivedEvents.Add(e);

        // Act
        await device.InitializeAsync();
        await device.StartAsync();

        // Simulate data arrival
        await Task.Delay(2000);

        await device.StopAsync();

        // Assert
        Assert.NotEmpty(receivedEvents);
        Assert.All(receivedEvents, e => Assert.NotEmpty(e.Data));
    }

    [Fact]
    public async Task Device_Should_Handle_Connection_Failure()
    {
        // Arrange
        var context = new MockApplicationContext();
        var config = new DeviceConfigurationBuilder()
            .WithConnectionSettings("invalid-host", 502, 1)
            .Build();

        var device = new MyFirstDevice(context, config);

        // Act & Assert
        var initialized = await device.InitializeAsync();
        Assert.False(initialized);
    }
}
```

### Step 3: Run Tests

```bash
dotnet test
```

## Sandbox Testing Best Practices

### 1. Use Configuration Profiles

Maintain separate configurations for different environments:

**appsettings.Development.json** (Sandbox):
```json
{
  "DeviceConfigs": {
    "MyDevice": {
      "ConnectionSettings": {
        "Host": "127.0.0.1",
        "Port": 502
      }
    }
  },
  "UseMockCloud": true
}
```

**appsettings.Production.json** (Real):
```json
{
  "DeviceConfigs": {
    "MyDevice": {
      "ConnectionSettings": {
        "Host": "192.168.1.100",
        "Port": 502
      }
    }
  },
  "UseMockCloud": false
}
```

### 2. Simulate Error Conditions

Test how your device handles errors:

```csharp
// Simulate network timeout
device.ConnectionTimeout = TimeSpan.FromSeconds(2);

// Simulate invalid data
// Use simulator to send corrupted Modbus responses

// Simulate device disconnect
// Stop simulator mid-operation
```

### 3. Validate Data Processing

Verify your data transformations work correctly:

```csharp
[Fact]
public void Temperature_Transform_Should_Convert_Celsius_To_Fahrenheit()
{
    var pipeline = new TelemetryTransformPipeline();
    pipeline.AddTransform(new UnitConversionTransform(
        fromUnit: "celsius",
        toUnit: "fahrenheit"
    ));

    var input = new TelemetryData { Value = 25.0 }; // 25°C
    var output = pipeline.Transform(input);

    Assert.Equal(77.0, output.Value, 1); // 77°F
}
```

### 4. Mock External Dependencies

```csharp
public class MockTelemetryClient : ITelemetryClient
{
    public List<TelemetryData> SentData { get; } = new();

    public async Task<bool> SendTelemetryAsync(TelemetryData data)
    {
        SentData.Add(data);
        return true;
    }
}

// Use in tests
var mockClient = new MockTelemetryClient();
context.UseTelemetryClient(mockClient);

// Verify telemetry was sent
Assert.Contains(mockClient.SentData, d => d.ResourceId == "temp");
```

## Common Sandbox Scenarios

### Scenario 1: Offline Development

```bash
# No network, no devices, just local testing
dotnet run --UseMockCloud=true --UseSimulator=true
```

### Scenario 2: Cloud Integration Testing

```bash
# Real cloud, simulated device
dotnet run --UseMockCloud=false --UseSimulator=true
```

### Scenario 3: End-to-End with Real Device

```bash
# Real everything
dotnet run --UseMockCloud=false --UseSimulator=false
```

## Next Steps

- [Adding Transformations](03_add_transformation.md) - Process and transform data
- [Adding DSP Filters](04_add_dsp_filters.md) - Apply signal processing
- [Using Hooks](05_using_hooks.md) - Intercept and extend functionality

## Troubleshooting

### Simulator Won't Start

**Error: Port 502 already in use**
```bash
# Find process using port 502
lsof -i :502
# Kill the process
kill -9 <PID>
```

### Device Can't Connect to Simulator

1. Check firewall settings
2. Verify simulator is listening: `netstat -an | grep 502`
3. Use `127.0.0.1` instead of `localhost`
4. Check simulator logs for errors

### Tests Are Flaky

1. Add delays for async operations
2. Use proper cancellation tokens
3. Clean up resources in test teardown
4. Avoid shared state between tests

## Summary

Sandbox testing is essential for:
- **Rapid development** without hardware dependencies
- **Reliable testing** with controlled scenarios
- **CI/CD integration** with automated tests
- **Error simulation** for robust error handling

The Weda SubNode SDK provides:
- Mock services for all external dependencies
- Built-in simulators for Modbus devices
- Test utilities and builders
- Configuration-based environment switching
