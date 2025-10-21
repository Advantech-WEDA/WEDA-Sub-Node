---
title: Device Lifecycle Management
category: device
order: 1
parent: null
related:
  - path: architecture_overview.md
    title: Architecture Overview
  - path: architecture_overview.md
    title: Cloud Integration
  - path: device_lifecycle.md
    title: Event System
author: Rain Hu
version: 0.0.1
date: 2025-10-15
description: Device Lifecycle Management - Understanding device initialization, operation, and shutdown
tags: [Device, Lifecycle, State Management, IDevice]
---

# Device Lifecycle Management

This document explains the complete lifecycle of a device in the Weda SubNode SDK, from initialization to shutdown.

## Device States

A device transitions through the following states:

```
Created → Initializing → Initialized → Starting → Running → Stopping → Stopped → Disposed
```

### State Descriptions

- **Created**: Device instance created but not initialized
- **Initializing**: Connecting to physical device and cloud
- **Initialized**: Ready to start but not running
- **Starting**: Background tasks are starting
- **Running**: Actively reading and sending telemetry
- **Stopping**: Gracefully shutting down tasks
- **Stopped**: All tasks stopped, connections closed
- **Disposed**: Resources released, cannot be reused

## Lifecycle Methods

### IDevice Interface

```csharp
public interface IDevice : IDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    DeviceStatus Status { get; }
    DeviceConfiguration Configuration { get; }
    string DeviceId { get; }
}
```

## Initialization Phase

The initialization phase prepares the device for operation.

### InitializeAsync Flow

```
InitializeAsync()
    │
    ├─> 1. Validate Configuration
    │       - Check required parameters
    │       - Validate sensor configurations
    │
    ├─> 2. Build Connections
    │       ├─> ICommunication.ConnectAsync()
    │       │     - Connect to physical device
    │       │     - Verify communication
    │       │
    │       └─> IWedaCloudService.ConnectAsync()
    │             - Connect to cloud platform
    │
    ├─> 3. Get or Register Device ID
    │       ├─> Load from local storage
    │       └─> Register with cloud if new
    │
    ├─> 4. Enrich Configuration
    │       - Generate ResourceIds for sensors
    │       - Validate data types
    │
    ├─> 5. Upload Configuration
    │       - Send device config to cloud
    │       - Update digital twin
    │
    └─> 6. Subscribe to Cloud Events
            ├─> Configuration updates
            └─> Remote commands
```

### Example: Manual Initialization

```csharp
var config = new DeviceConfiguration
{
    DeviceName = "My Device",
    DeviceType = "ModbusTCP",
    // ... configuration
};

var device = new TcpModbusDevice(
    host: "192.168.1.100",
    port: 502,
    slaveId: 1,
    deviceConfiguration: config,
    cloudService: cloudService
);

// Initialize the device
await device.InitializeAsync();

// Device is now ready to start
Console.WriteLine($"Device ID: {device.DeviceId}");
Console.WriteLine($"Status: {device.Status}");
```

## Start Phase

The start phase begins background operations.

### StartAsync Flow

```
StartAsync()
    │
    ├─> 1. Verify Initialized
    │       - Ensure InitializeAsync() was called
    │       - Check connection status
    │
    ├─> 2. Create Background Tasks
    │       ├─> Telemetry Reading Task
    │       │     - Runs every ReadTelemetry period
    │       │     - Reads sensors and processes data
    │       │
    │       ├─> Telemetry Sending Task
    │       │     - Runs every SendTelemetry period
    │       │     - Uploads data to cloud
    │       │
    │       └─> Health Reporting Task
    │             - Runs every ReportHealth period
    │             - Reports device status
    │
    └─> 3. Update Status to Running
```

### Example: Starting a Device

```csharp
// Start the device (after initialization)
await device.StartAsync();

// Device is now running
// Background tasks are actively collecting and sending data
Console.WriteLine("Device started successfully");

// Keep running
await Task.Delay(Timeout.Infinite);
```

## Running Phase

During the running phase, the device continuously:

### Telemetry Reading Loop

```csharp
while (!cancellationToken.IsCancellationRequested)
{
    try
    {
        // 1. Read from physical device
        var telemetry = await ReadTelemetryAsync(cancellationToken);

        // 2. Emit DataReceived event
        OnDataReceived(new DataReceivedEvent
        {
            DeviceId = DeviceId,
            Data = telemetry.Measures
        });

        // 3. Store for batch sending
        _telemetryBuffer.Add(telemetry);

        // 4. Wait for next period
        await Task.Delay(Configuration.Periods.ReadTelemetry, cancellationToken);
    }
    catch (Exception ex)
    {
        // Handle errors
        _logger.LogError(ex, "Error reading telemetry");
    }
}
```

### Telemetry Sending Loop

```csharp
while (!cancellationToken.IsCancellationRequested)
{
    try
    {
        // 1. Get buffered telemetry
        var telemetryToSend = _telemetryBuffer.DequeueAll();

        if (telemetryToSend.Any())
        {
            // 2. Send to cloud
            await SendTelemetryAsync(telemetryToSend, cancellationToken);

            // 3. Emit TelemetrySent event
            OnTelemetrySent(new TelemetrySentEvent
            {
                DeviceId = DeviceId,
                MeasureCount = telemetryToSend.Count,
                Success = true
            });
        }

        // 4. Wait for next period
        await Task.Delay(Configuration.Periods.SendTelemetry, cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sending telemetry");
    }
}
```

## Stop Phase

The stop phase gracefully shuts down operations.

### StopAsync Flow

```
StopAsync()
    │
    ├─> 1. Cancel Background Tasks
    │       - Signal cancellation token
    │       - Wait for tasks to complete
    │
    ├─> 2. Flush Pending Data
    │       - Send any buffered telemetry
    │       - Report final health status
    │
    ├─> 3. Close Connections
    │       ├─> ICommunication.DisconnectAsync()
    │       └─> IWedaCloudService.DisconnectAsync()
    │
    └─> 4. Update Status to Stopped
```

### Example: Stopping a Device

```csharp
// Graceful shutdown
await device.StopAsync();

Console.WriteLine("Device stopped successfully");

// Dispose resources
device.Dispose();
```

## Error Handling

### Connection Failures

```csharp
device.ConnectionStateChanged += (sender, e) =>
{
    if (e.CurrentState == CommunicationState.Error)
    {
        _logger.LogError($"Connection failed: {e.ErrorMessage}");

        // Implement retry logic
        Task.Run(async () =>
        {
            await Task.Delay(5000);
            await device.InitializeAsync();
            await device.StartAsync();
        });
    }
};
```

### Telemetry Read Errors

```csharp
device.ErrorOccurred += (sender, e) =>
{
    _logger.LogError(e.Exception, $"Error in device: {e.DeviceId}");

    // Update error count
    _errorCount++;

    // Take action if too many errors
    if (_errorCount > 10)
    {
        _ = device.StopAsync();
    }
};
```

## Complete Lifecycle Example

```csharp
public class DeviceManager
{
    private readonly IDevice _device;
    private CancellationTokenSource _cts;

    public async Task RunAsync()
    {
        _cts = new CancellationTokenSource();

        try
        {
            // 1. Initialize
            Console.WriteLine("Initializing device...");
            await _device.InitializeAsync(_cts.Token);

            // 2. Subscribe to events
            SubscribeToEvents();

            // 3. Start
            Console.WriteLine("Starting device...");
            await _device.StartAsync(_cts.Token);

            Console.WriteLine("Device is running. Press Ctrl+C to stop...");

            // 4. Wait for cancellation
            await Task.Delay(Timeout.Infinite, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Shutdown requested");
        }
        finally
        {
            // 5. Stop
            Console.WriteLine("Stopping device...");
            await _device.StopAsync();

            // 6. Dispose
            _device.Dispose();

            Console.WriteLine("Device stopped successfully");
        }
    }

    private void SubscribeToEvents()
    {
        _device.DataReceived += (s, e) =>
        {
            Console.WriteLine($"Received {e.Data.Count} measures");
        };

        _device.TelemetrySent += (s, e) =>
        {
            Console.WriteLine($"Sent {e.MeasureCount} measures");
        };

        _device.DeviceStatusChanged += (s, e) =>
        {
            Console.WriteLine($"Status changed: {e.PreviousStatus} → {e.CurrentStatus}");
        };
    }

    public void Stop()
    {
        _cts?.Cancel();
    }
}

// Usage
var manager = new DeviceManager();

// Handle Ctrl+C
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    manager.Stop();
};

await manager.RunAsync();
```

## Best Practices

### Always Initialize Before Start

```csharp
// Correct
await device.InitializeAsync();
await device.StartAsync();

// Incorrect - will throw exception
await device.StartAsync(); // Error: Device not initialized
```

### Dispose Resources

```csharp
// Use using statement
await using var device = new TcpModbusDevice(...);
await device.InitializeAsync();
await device.StartAsync();

// Device automatically disposed when scope exits
```

### Handle Cancellation

```csharp
var cts = new CancellationTokenSource();

try
{
    await device.InitializeAsync(cts.Token);
    await device.StartAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // Clean shutdown
    await device.StopAsync();
}
```

### Monitor Status Changes

```csharp
device.DeviceStatusChanged += async (sender, e) =>
{
    switch (e.CurrentStatus)
    {
        case DeviceStatus.Error:
            // Attempt recovery
            await device.StopAsync();
            await Task.Delay(5000);
            await device.InitializeAsync();
            await device.StartAsync();
            break;

        case DeviceStatus.Running:
            _logger.LogInformation("Device running normally");
            break;
    }
};
```

## Summary

The device lifecycle follows a clear pattern:
1. Create device instance
2. Initialize connections and configuration
3. Start background operations
4. Run continuously until stopped
5. Stop gracefully
6. Dispose resources

Understanding this lifecycle is essential for:
- Proper resource management
- Error handling and recovery
- Clean shutdown procedures
- Integration with hosting frameworks

