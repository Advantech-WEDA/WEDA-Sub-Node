# Basic Modbus (Builder Pattern) Example

This example demonstrates how to build a Modbus TCP device application using the **Builder pattern** (wedabuilder style). This approach is similar to ASP.NET Core's `WebApplication.CreateBuilder()` and is recommended for production environments with multiple devices.

## 📋 Overview

- **Template Style**: Builder pattern (like ASP.NET Core)
- **Connection**: Real TCP connection (requires Modbus TCP server)
- **Cloud**: Weda EdgeSync Cloud (configure in appsettings.json)
- **Device Type**: TcpModbusDevice
- **Use Case**: Production-ready multi-device management

## 🔄 Comparison with basic-modbus

| Feature | basic-modbus | basic-modbus-builder |
|---------|--------------|----------------------|
| Pattern | Manual Context | Builder Pattern |
| DI Container | ❌ No | ✅ Microsoft.Extensions.DI |
| Hosted Services | ❌ Manual lifecycle | ✅ Automatic lifecycle |
| Configuration | Manual loading | Automatic from appsettings.json |
| Code Style | Explicit control | Fluent API |
| Best For | Learning, debugging | Production, multiple devices |

## 🚀 Quick Start

### Prerequisites

- .NET 9.0 SDK
- Modbus TCP server running on `127.0.0.1:5020`
  - You can use the internal Modbus simulator
  - Or any Modbus TCP device/simulator

### Run the Example

```bash
# From this directory
dotnet run

# Output:
# [16:45:12.345 INF] Device started successfully
# [16:45:13.123 INF] temperature.sensor: 25.3 celsius
# [16:45:13.124 INF] voltage.sensor: 220.5 volt
# [16:45:13.125 INF] current.sensor: 2.1 ampere
```

## 📝 Key Code Highlights

### Program.cs - Builder Pattern

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .AddLogging()           // Serilog from appsettings.json
    .AddTelemetry()         // Enable telemetry upload
    .AddHealthReporting()   // Enable health reporting
    .AddCommands()          // Enable command receiving
    .AddConfigUpdates();    // Enable config updates

// Load device from appsettings.json "DeviceConfigs:MyModbusDevice"
builder.AddDevice<MyModbusDevice>("MyModbusDevice");

var app = builder.Build();
await app.RunAsync();
```

### MyModbusDevice.cs - Event Handling

```csharp
public class MyModbusDevice : TcpModbusDevice
{
    public MyModbusDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration)
    {
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        // Process telemetry data
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            _logger.LogInformation("{SensorName}: {Value} {Unit}",
                sensor.Name, measure.Value, sensor.Config.Unit);
        }
    }
}
```

## ⚙️ Configuration

Edit `appsettings.json` to configure:

1. **Cloud Connection** (Nats section):
   ```json
   "Nats": {
     "Url": "nats://your-cloud-server:4224"
   }
   ```

2. **Device Settings** (DeviceConfigs section):
   ```json
   "DeviceConfigs": {
     "MyModbusDevice": {
       "DeviceName": "Your Device Name",
       "Communication": {
         "Host": "192.168.1.100",  // Your Modbus TCP server
         "Port": 502,
         "SlaveId": 1
       }
     }
   }
   ```

3. **Sensors**: Add or modify sensors in the `Sensors` array

## 📚 Related Examples

- **[basic-modbus](../basic-modbus/)** - Manual Context pattern (for learning)
- **[wise-4012](../wise-4012/)** - Real WISE-4012 hardware integration
- **[wise-4012-isensing](../wise-4012-isensing/)** - iSensing feature demonstration

## 📖 Documentation

- [Weda SubNode SDK Wiki](../../docs/wiki/en/README.md)
- [Quick Start Guide](../../docs/wiki/en/01_quick_start/02_wedabuilder_basic.md)
- [Builder Pattern API Reference](../../docs/wiki/en/02_core_concepts/wedaapplication_builder.md)

## 🎯 Next Steps

1. **Add Transform Pipeline**: See [tutorials/01-transform-dsp-config](../../tutorials/01-transform-dsp-config/)
2. **Add DSP Filters**: See [tutorials/02-transform-dsp-programmatic](../../tutorials/02-transform-dsp-programmatic/)
3. **Multiple Devices**: Add more devices using `builder.AddDevice<T>()`
