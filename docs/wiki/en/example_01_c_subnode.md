# Quick Start: Custom Device Mode (subnode)

**Template**: `dotnet new subnode`
**Time to complete**: 15 minutes
**Difficulty**: *** Advanced

## What You'll Build

A fully custom Modbus device with:
- *> Custom device class inheriting from `ModbusDevice`
- *> Override lifecycle methods
- *> Event-driven architecture
- *> Custom data processing logic
- *> Full protocol control

**Perfect for**: Custom protocols, advanced event handling, specialized devices

---

## Step 1: Create Project

Create a new project using the `subnode` template:

```bash
# Create project
dotnet new subnode -n MyCustomDevice
cd MyCustomDevice

# Project structure
MyCustomDevice/
├── MyCustomDevice.cs          # Your custom device class
├── Program.cs                  # Application entry point
├── appsettings.json           # Device configuration
└── MyCustomDevice.csproj
```

---

## Step 2: Review the Generated Device Class

Open `MyCustomDevice.cs` - this is your custom device implementation:

```csharp
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.Core.Protocols.Modbus;

namespace MyCustomDevice;

public class MyCustomDevice : ModbusDevice
{
    private readonly ILogger<MyCustomDevice> _logger;

    public MyCustomDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        ICommunication communication,
        ILogger<MyCustomDevice> logger)
        : base(context, configuration, communication)
    {
        _logger = logger;

        // Subscribe to telemetry events
        TelemetryRead += OnTelemetryRead;
    }

    /// <summary>
    /// Called when telemetry data is read from the device
    /// </summary>
    private void OnTelemetryRead(object? sender, TelemetryReadEvent e)
    {
        _logger.LogDebug("MyCustomDevice: Telemetry read, Count={Count}", e.Measures.Count);

        foreach (var measure in e.Measures)
        {
            var sensor = Configuration.Sensors
                .FirstOrDefault(s => s.ResourceId == measure.ResourceId);

            if (sensor != null && measure.Value != null)
            {
                _logger.LogInformation("[DATA] {SensorName}: {Value}",
                    sensor.Name,
                    measure.Value);

                // Add your custom logic here
                HandleSensorData(sensor, measure);
            }
        }
    }

    /// <summary>
    /// Custom data handling logic
    /// </summary>
    private void HandleSensorData(SensorInfo sensor, TelemetryMeasure measure)
    {
        // Example: Check thresholds
        if (sensor.ResourceId == "temp" && measure.Value is double tempValue)
        {
            if (tempValue > 80.0)
            {
                _logger.LogWarning("! High temperature detected: {Temp}°C", tempValue);
                // Trigger alarm, send notification, etc.
            }
        }

        // Example: Aggregate data
        // Example: Store in local database
        // Example: Forward to other systems
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            TelemetryRead -= OnTelemetryRead;
        }

        base.Dispose(disposing);
    }
}
```

---

## Step 3: Review Program.cs

Open `Program.cs` - notice how we manually register the device:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Weda.SubNode.Host;
using MyCustomDevice;

var builder = WedaApplication.CreateBuilder(args);

// Register our custom device
builder.Services.RegisterDevice<MyCustomDevice.MyCustomDevice>("MyCustomDevice");

var app = builder.Build();
await app.RunAsync();
```

**Key Points**:
- `RegisterDevice<T>` links the device name in config to your class
- Device name must match the key in `DeviceConfigs` in `appsettings.json`

---

## Step 4: Configure Your Device

Edit `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Weda.SubNode": "Debug",
      "MyCustomDevice": "Debug"
    }
  },

  "DeviceConfigs": {
    "MyCustomDevice": {
      "Id": "my-custom-001",
      "Name": "My Custom Device",
      "DeviceType": "ModbusTCP",
      "ConnectionSettings": {
        "Host": "192.168.1.100",
        "Port": 502,
        "SlaveId": 1
      },
      "PollingInterval": 1000,
      "Sensors": [
        {
          "ResourceId": "temp",
          "Name": "Temperature",
          "RegisterAddress": 0,
          "RegisterType": "HoldingRegister",
          "DataType": "Float32",
          "Config": {
            "Enabled": true,
            "Interval": 1000
          }
        },
        {
          "ResourceId": "pressure",
          "Name": "Pressure",
          "RegisterAddress": 2,
          "RegisterType": "HoldingRegister",
          "DataType": "Float32",
          "Config": {
            "Enabled": true,
            "Interval": 1000
          }
        }
      ]
    }
  },

  "Nats": {
    "Url": "nats://localhost:4222",
    "Enabled": true
  }
}
```

---

## Step 5: Override Lifecycle Methods

You can override device lifecycle methods for custom behavior:

```csharp
public class MyCustomDevice : ModbusDevice
{
    /// <summary>
    /// Called once during device initialization
    /// </summary>
    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing {DeviceName}...", Configuration.Name);

        // Custom initialization logic
        await LoadCalibrationDataAsync();

        await base.OnInitializeAsync(cancellationToken);
    }

    /// <summary>
    /// Called when device starts
    /// </summary>
    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting {DeviceName}...", Configuration.Name);

        // Custom start logic
        await ConnectToExternalSystemAsync();

        await base.OnStartAsync(cancellationToken);
    }

    /// <summary>
    /// Called when device stops
    /// </summary>
    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping {DeviceName}...", Configuration.Name);

        // Custom stop logic
        await FlushDataAsync();

        await base.OnStopAsync(cancellationToken);
    }

    /// <summary>
    /// Called when an error occurs
    /// </summary>
    protected override async Task OnErrorAsync(Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Device error occurred");

        // Custom error handling
        await LogErrorToExternalSystemAsync(exception);

        await base.OnErrorAsync(exception, cancellationToken);
    }
}
```

---

## Step 6: Subscribe to Device Events

The `ModbusDevice` base class provides several events you can subscribe to:

```csharp
public class MyCustomDevice : ModbusDevice
{
    public MyCustomDevice(...)
    {
        // Telemetry events
        TelemetryRead += OnTelemetryRead;
        TelemetrySent += OnTelemetrySent;

        // Connection events
        Connected += OnConnected;
        Disconnected += OnDisconnected;

        // State change events
        StateChanged += OnStateChanged;
    }

    private void OnTelemetryRead(object? sender, TelemetryReadEvent e)
    {
        _logger.LogDebug("Telemetry read: {Count} measures", e.Measures.Count);
        // Process raw telemetry before it's sent to cloud
    }

    private void OnTelemetrySent(object? sender, TelemetrySentEvent e)
    {
        _logger.LogDebug("Telemetry sent: {Count} measures", e.Measures.Count);
        // Confirm data was sent successfully
    }

    private void OnConnected(object? sender, EventArgs e)
    {
        _logger.LogInformation("Device connected to Modbus");
        // Handle connection established
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        _logger.LogWarning("Device disconnected from Modbus");
        // Handle connection lost
    }

    private void OnStateChanged(object? sender, StateChangedEvent e)
    {
        _logger.LogInformation("State changed: {From} -> {To}",
            e.PreviousState, e.CurrentState);
        // Track device state machine transitions
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from events
            TelemetryRead -= OnTelemetryRead;
            TelemetrySent -= OnTelemetrySent;
            Connected -= OnConnected;
            Disconnected -= OnDisconnected;
            StateChanged -= OnStateChanged;
        }

        base.Dispose(disposing);
    }
}
```

---

## Step 7: Custom Data Processing

Add custom data processing logic in your device:

```csharp
public class MyCustomDevice : ModbusDevice
{
    private readonly Queue<TelemetryMeasure> _dataBuffer = new();
    private const int BufferSize = 100;

    private void OnTelemetryRead(object? sender, TelemetryReadEvent e)
    {
        foreach (var measure in e.Measures)
        {
            // Buffer data
            BufferMeasure(measure);

            // Process with custom logic
            ProcessMeasure(measure);

            // Check thresholds
            CheckThresholds(measure);
        }
    }

    private void BufferMeasure(TelemetryMeasure measure)
    {
        _dataBuffer.Enqueue(measure);

        if (_dataBuffer.Count > BufferSize)
        {
            _dataBuffer.Dequeue();
        }

        _logger.LogDebug("Buffer size: {Size}", _dataBuffer.Count);
    }

    private void ProcessMeasure(TelemetryMeasure measure)
    {
        if (measure.Value is not double value)
            return;

        // Custom processing
        var processed = value * 1.05 - 2.0;

        _logger.LogDebug("Processed {ResourceId}: {Original} -> {Processed}",
            measure.ResourceId, value, processed);
    }

    private void CheckThresholds(TelemetryMeasure measure)
    {
        var sensor = Configuration.Sensors
            .FirstOrDefault(s => s.ResourceId == measure.ResourceId);

        if (sensor == null || measure.Value is not double value)
            return;

        // Define thresholds
        var thresholds = new Dictionary<string, (double Min, double Max)>
        {
            ["temp"] = (-10.0, 80.0),
            ["pressure"] = (0.0, 1000.0),
            ["humidity"] = (0.0, 100.0)
        };

        if (thresholds.TryGetValue(sensor.ResourceId, out var range))
        {
            if (value < range.Min || value > range.Max)
            {
                _logger.LogWarning("! {Sensor} out of range: {Value} (expected {Min}-{Max})",
                    sensor.Name, value, range.Min, range.Max);

                TriggerAlarm(sensor, value, range);
            }
        }
    }

    private void TriggerAlarm(SensorInfo sensor, double value, (double Min, double Max) range)
    {
        _logger.LogError("[ALARM] ALARM: {Sensor} = {Value} (range: {Min}-{Max})",
            sensor.Name, value, range.Min, range.Max);

        // Send notification, write to database, etc.
    }
}
```

---

## Step 8: Add Dependency Injection

Inject services into your custom device:

### Create a Service

Create `Services/AlertService.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace MyCustomDevice.Services;

public interface IAlertService
{
    Task SendAlertAsync(string message, CancellationToken ct = default);
}

public class AlertService : IAlertService
{
    private readonly ILogger<AlertService> _logger;

    public AlertService(ILogger<AlertService> logger)
    {
        _logger = logger;
    }

    public async Task SendAlertAsync(string message, CancellationToken ct = default)
    {
        _logger.LogWarning("Sending alert: {Message}", message);

        // Send email, SMS, push notification, etc.
        await Task.Delay(100, ct); // Simulate async operation

        _logger.LogInformation("Alert sent successfully");
    }
}
```

### Inject Service into Device

Update `MyCustomDevice.cs`:

```csharp
using MyCustomDevice.Services;

public class MyCustomDevice : ModbusDevice
{
    private readonly ILogger<MyCustomDevice> _logger;
    private readonly IAlertService _alertService;

    public MyCustomDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        ICommunication communication,
        ILogger<MyCustomDevice> logger,
        IAlertService alertService)  // Inject service
        : base(context, configuration, communication)
    {
        _logger = logger;
        _alertService = alertService;

        TelemetryRead += OnTelemetryRead;
    }

    private void OnTelemetryRead(object? sender, TelemetryReadEvent e)
    {
        foreach (var measure in e.Measures)
        {
            if (measure.Value is double value && value > 80.0)
            {
                // Use injected service
                _ = _alertService.SendAlertAsync(
                    $"High temperature: {value}°C");
            }
        }
    }
}
```

### Register Service

Update `Program.cs`:

```csharp
using MyCustomDevice.Services;

var builder = WedaApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<IAlertService, AlertService>();

// Register device
builder.Services.RegisterDevice<MyCustomDevice.MyCustomDevice>("MyCustomDevice");

var app = builder.Build();
await app.RunAsync();
```

---

## Step 9: Run Your Application

### Build and Run

```bash
dotnet build
dotnet run
```

### Expected Output

```
[12:34:56 INF] Starting Weda SubNode application...
[12:34:56 INF] Initializing My Custom Device...
[12:34:56 INF] Starting My Custom Device...
[12:34:56 DBG] Device configuration loaded: MyCustomDevice
[12:34:56 INF] Device started successfully. Press Ctrl+C to stop...
[12:34:57 INF] Device connected to Modbus
[12:34:57 DBG] State changed: Initializing -> Running
[12:34:57 DBG] Telemetry read: 2 measures
[12:34:57 INF] [DATA] Temperature: 25.5°C
[12:34:57 INF] [DATA] Pressure: 1013.25 Pa
[12:34:57 DBG] Buffer size: 2
[12:34:57 DBG] Processed temp: 25.5 -> 24.775
[12:34:57 DBG] Telemetry sent: 2 measures
```

---

## Advanced Scenarios

### Scenario 1: Custom Modbus Commands

```csharp
public class MyCustomDevice : ModbusDevice
{
    public async Task<bool> WriteCoilAsync(int address, bool value)
    {
        try
        {
            // Access communication layer
            var result = await Communication.SendAsync(
                BuildWriteCoilCommand(address, value));

            return result != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write coil");
            return false;
        }
    }
}
```

### Scenario 2: Multiple Device Types in One Application

Create different device classes for different types:

```csharp
// Temperature device
public class TemperatureDevice : ModbusDevice { /* ... */ }

// Pressure device
public class PressureDevice : ModbusDevice { /* ... */ }

// Flow meter device
public class FlowMeterDevice : ModbusDevice { /* ... */ }
```

Register all in `Program.cs`:

```csharp
var builder = WedaApplication.CreateBuilder(args);

builder.Services.RegisterDevice<TemperatureDevice>("TempSensor");
builder.Services.RegisterDevice<PressureDevice>("PressureSensor");
builder.Services.RegisterDevice<FlowMeterDevice>("FlowMeter");

var app = builder.Build();
await app.RunAsync();
```

Configure in `appsettings.json`:

```json
{
  "DeviceConfigs": {
    "TempSensor": { /* ... */ },
    "PressureSensor": { /* ... */ },
    "FlowMeter": { /* ... */ }
  }
}
```

---

## When to Use subnode vs wedaapi vs wedaapi-c?

### Stick with `subnode` (Custom Device) if:
- *> You need custom device lifecycle logic
- *> Want to override event handlers
- *> Custom protocol implementation required
- *> Maximum flexibility needed

### Downgrade to `wedaapi-c` (Advanced API) when:
- ! You don't need custom device classes
- ! Configuration-driven approach is sufficient
- ! Just need custom services, not custom devices

### Downgrade to `wedaapi` (Simple API) when:
- ! Standard behavior is sufficient
- ! No custom code needed
- ! Prefer simplicity over flexibility

---

## Next Steps

Now that you have a custom device, continue learning:

1. **[02. Sandbox Testing](02_sandbox_testing.md)** - Test without hardware
2. **[03. Data Transformations](03_add_transformation.md)** - Advanced data processing
3. **[04. DSP Filters](04_add_dsp_filters.md)** - Signal processing techniques
4. **[05. Hooks & Events](05_using_hooks.md)** - Lifecycle event handling

---

## Summary

**Custom Device Mode (`subnode`)** gives you:
- [TARGET] **Maximum control** - Override any device behavior
- [TOOL] **Flexible architecture** - Inherit from base classes
- [SIGNAL] **Event-driven** - Subscribe to all device events
- [POWER] **Powerful** - Full access to communication layer

**Trade-offs**:
- ! More code to write and maintain
- ! Need to understand device lifecycle
- ! Requires OOP knowledge (inheritance, events)

**Perfect for**: Custom protocols, advanced event handling, specialized devices with unique requirements.
