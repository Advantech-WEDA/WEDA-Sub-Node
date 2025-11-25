# WedaApplicationContext

The `WedaApplicationContext` is the central configuration and service container for the Weda SubNode SDK. It manages framework-level services like cloud connections, logging, and device registry.

## Overview

`WedaApplicationContext` provides:
- **Cloud Service** - Connection to the Weda cloud platform
- **Logger Factory** - Centralized logging configuration
- **Connection Options** - Retry and timeout policies for connections
- **Device Options** - Feature flags (telemetry, commands, config updates)
- **Device Registry** - Discovery and management of all registered devices
- **Configuration** - Access to `IConfiguration` from appsettings.json
- **Configuration Cache** - Persistent storage for cloud-updated configurations

## Device Registry

The Device Registry is a key feature that enables cross-device communication. When you create a device with a context, the device is automatically registered.

### Getting Devices

```csharp
// Get device by name (throws if not found)
var device = context.GetDevice("MyDevice");

// Get device with type checking (throws if not found or wrong type)
var modbusDevice = context.GetDevice<TcpModbusDevice>("MyModbusDevice");

// Find device by name (returns null if not found)
var device = context.FindDevice("MyDevice");

// Find device with type checking (returns null if not found or wrong type)
var modbusDevice = context.FindDevice<TcpModbusDevice>("MyModbusDevice");
```

### Getting Sensors from Devices

```csharp
// Get sensor by name (throws if not found)
var sensor = device.GetSensor("Temperature");

// Find sensor by name (returns null if not found)
var sensor = device.FindSensor("Temperature");

// Get/Find sensor by ResourceId
var sensor = device.GetSensorByResourceId("temp-001");
var sensor = device.FindSensorByResourceId("temp-001");
```

## Usage Modes

### Default Singleton (Simplest)

For simple single-device scenarios, use the default singleton instance:

```csharp
// Simplest usage - no context management needed
var device = new TcpModbusDevice(WedaApplicationContext.Default, config);
await device.StartAsync();
```

The `Default` singleton:
- Auto-loads configuration from `appsettings.json`
- Thread-safe lazy initialization
- Shared across the application
- Should NOT be disposed manually (disposed on app exit)

### SubNode Mode (Manual Device Creation)

In SubNode mode, you create devices manually. **We recommend using the `WedaApplicationContext.Default` singleton** to ensure all devices share the same context:

```csharp
var context = WedaApplicationContext.Default;

var config = new DeviceConfiguration
{
    DeviceName = "MyDevice",
    // ... other configuration
};

var device = new MyFirstDevice(context, config);
await device.StartAsync();
```

**Important**: Only devices created with the same context instance can discover each other. If you create devices with different contexts, they won't be able to discover each other:

```csharp
// Anti-pattern - NOT recommended
using var context1 = new WedaApplicationContext();
using var context2 = new WedaApplicationContext();

var device1 = new MyDevice(context1, config1);
var device2 = new MyDevice(context2, config2);

// device1 cannot find device2 because they have different contexts
var found = context1.FindDevice("Device2"); // returns null
```

```csharp
// Correct approach - use the Default singleton
var context = WedaApplicationContext.Default;

var device1 = new MyDevice(context, config1);
var device2 = new MyDevice(context, config2);

// device1 can find device2
var found = context.FindDevice("Device2"); // success
```

### WedaBuilder Mode (Automatic Device Management)

In WedaBuilder mode, all devices share a single context automatically:

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .AddLogging()
    .AddTelemetry()
    .ConfigureNats(options => { /* ... */ });

// All devices share the same context
builder.AddDevice<MyFirstDevice>("MyFirstDeviceConfig");
builder.AddDevice<MySecondDevice>("MySecondDeviceConfig");

var app = builder.Build();

// Devices can discover each other
var device1 = app.Context.GetDevice<MyFirstDevice>("MyFirstDevice");
var device2 = app.Context.GetDevice<MySecondDevice>("MySecondDevice");

await app.RunAsync();
```

## Configuration Options

### Connection Policy

Configure retry and timeout behavior:

```csharp
builder.ConfigureConnectionPolicy(options =>
{
    options.MaxRetryAttempts = 5;        // Limit retries (-1 for unlimited)
    options.RetryDelayMs = 2000;          // Initial delay (exponential backoff)
    options.MaxRetryDelayMs = 30000;      // Maximum delay cap
    options.ConnectionTimeoutMs = 60000;  // Timeout per attempt
});
```

### Device Options

Enable/disable features:

```csharp
// Enable telemetry sending
builder.AddTelemetry();

// Enable command receiving from cloud
builder.AddCommands();

// Enable configuration updates from cloud
builder.AddConfigUpdates();
```

## Cross-Device Communication Example

```csharp
public class ControllerDevice : TcpModbusDevice
{
    public ControllerDevice(IWedaApplicationContext context, DeviceConfiguration config)
        : base(context, config)
    {
        DataReceived += OnDataReceived;
    }

    private async void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        // Find another device in the same context
        var actuator = _context.FindDevice<ActuatorDevice>("Actuator1");
        if (actuator != null)
        {
            // Get sensor from the actuator
            var valve = actuator.FindSensor("ValveControl");

            // Perform cross-device operation
            // ...
        }
    }
}
```

## Best Practices

1. **Share Context**: When devices need to communicate, ensure they share the same `WedaApplicationContext`.

2. **Use WedaBuilder for Multi-Device**: For applications with multiple devices, prefer WedaBuilder mode as it automatically handles context sharing.

3. **Dispose Properly**: Always dispose the context when done. Disposing the context will unregister all devices.

4. **Type-Safe Access**: Use generic methods (`GetDevice<T>`, `FindDevice<T>`) when you know the device type for type safety.

5. **Null Checks**: Use `FindDevice`/`FindSensor` with null checks when the device/sensor might not exist, rather than catching exceptions from `GetDevice`/`GetSensor`.

## Configuration Cache (UC9868)

The Configuration Cache enables cloud-driven configuration updates to persist across device restarts.

### How It Works

1. **Initial Load**: On startup, the framework checks for `.device-config-cache.json`
   - If cache exists: Use cached configuration (cloud-updated)
   - If cache not exists: Use `appsettings.json` (initial configuration)

2. **Cloud Updates**: When configuration is updated from cloud (UC9868):
   - Framework automatically applies base configuration (sensors, periods)
   - Framework saves updated configuration to cache
   - Custom devices can handle device-specific configuration in `OnAfterConfigUpdateAsync`

3. **Restart Persistence**: On next device restart:
   - Cached configuration is loaded instead of `appsettings.json`
   - Device continues with cloud-updated settings

### Cache File

The configuration cache is stored in `.device-config-cache.json` in the working directory (same pattern as `.device-registration.json`).

```
/your-app/
├── appsettings.json              # Initial/baseline configuration
├── .device-registration.json     # Device registration cache
└── .device-config-cache.json     # Cloud-updated configuration cache
```

### Configuration Priority

```
1. .device-config-cache.json (if exists)  → Cloud-updated configuration
2. appsettings.json                        → Initial/fallback configuration
```

### Manual Cache Management

```csharp
// Check if cache exists
if (await context.ConfigurationCache.ExistsAsync())
{
    // Get cached configuration
    var cachedConfig = await context.ConfigurationCache.GetConfigurationAsync();

    // Get last modified time
    var lastModified = await context.ConfigurationCache.GetLastModifiedAsync();
}

// Reset to appsettings.json (delete cache)
await context.ConfigurationCache.DeleteCacheAsync();

// Manually save configuration to cache
await context.ConfigurationCache.SaveConfigurationAsync(deviceConfig);
```

### Framework vs Custom Configuration

The framework automatically handles base configuration in `DeviceBase`:

| Handled by Framework | Custom Device Responsibility |
|---------------------|------------------------------|
| Sensor config (enabled, interval, thresholds) | Report status to cloud (updating/success/failed) |
| Background task periods | Device-specific properties |
| Persisting to cache | Custom validation logic |

### Example: Custom Device with Cloud Config

```csharp
public class MyDevice : TcpModbusDevice
{
    protected override async Task OnAfterConfigUpdateAsync(
        UpdateConfigurationEvent e, CancellationToken ct)
    {
        // Base configuration is already applied and cached by framework

        // 1. Report status to cloud
        var message = ExtractMessage(e);
        var report = ConfigurationUpdateHelper.CreateSuccessReport(
            message, Configuration, "myDevice");
        await _context.CloudService.PublishConfigurationReportAsync(report, ct);

        // 2. Handle custom properties (if any)
        // var customConfig = message.Data?.Cfg?.Desired?.CustomProperties;
        // ApplyCustomConfiguration(customConfig);
    }
}
```
