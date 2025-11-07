using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Devices.Generic;

namespace WedaSubNode;

/// <summary>
/// MyFirstDevice - A custom device implementation
/// Inherits from TcpModbusDevice to get Modbus TCP protocol support with automatic communication setup
///
/// Demonstrates:
/// - Downlink hooks for configuration updates and commands
/// - ExecuteCommandAsync implementation for command handling
/// </summary>
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // Subscribe to DataReceived event to process telemetry
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Event handler for telemetry data received from device
    /// Prints all sensor values from configuration
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("MyFirstDevice: Data received, Count={Count}", e.Data.Count);

        // Print all sensor values based on configuration
        foreach (var sensor in Configuration.Sensors)
        {
            var measure = e.Data.FirstOrDefault(m => m.ResourceId == sensor.ResourceId);
            if (measure?.Value != null)
            {
                _logger.LogInformation("{SensorName}: {Value}",
                    sensor.Name,
                    measure.Value);
            }
        }

        // Add your custom logic here
        // Example: Apply business rules, trigger alerts, etc.
    }

    // ===== Downlink Hooks =====

    /// <summary>
    /// Called before configuration update is applied
    /// Use this to validate or prepare for configuration changes
    /// </summary>
    protected override async Task OnBeforeConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct)
    {
        _logger.LogInformation("Device {DeviceId} about to receive configuration update with {Count} items",
            e.DeviceId, e.Configuration.Count);

        // Example: Validate configuration before applying
        // if (!IsValidConfig(e.Configuration))
        //     throw new InvalidOperationException("Invalid configuration");
    }

    /// <summary>
    /// Called after configuration update is applied
    /// Use this to reload settings, restart components, etc.
    /// </summary>
    protected override async Task OnAfterConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct)
    {
        _logger.LogInformation("Configuration update completed at {Timestamp}", e.Timestamp);

        // Example: Reload settings, restart telemetry pipeline
        // await ReloadSettingsAsync();
    }

    /// <summary>
    /// Called before command execution
    /// Use this for logging, validation, or preparation
    /// </summary>
    protected override async Task OnBeforeCommandAsync(ExecuteCommandEvent e, CancellationToken ct)
    {
        _logger.LogInformation("Device {DeviceId} about to execute command '{Command}' at {Timestamp}",
            e.DeviceId, e.Command.DeviceCmd, e.Timestamp);
    }

    /// <summary>
    /// Called after command execution
    /// Use this for cleanup, logging, or follow-up actions
    /// </summary>
    protected override async Task OnAfterCommandAsync(ExecuteCommandEvent e, bool success, CancellationToken ct)
    {
        _logger.LogInformation("Command '{Command}' execution {Result}",
            e.Command.DeviceCmd, success ? "succeeded" : "failed");

        // Example: Send notification, update status
        // if (!success) await SendAlertAsync($"Command {e.Command.DeviceCmd} failed");
    }

    /// <summary>
    /// Execute command on device
    /// This is automatically called by the framework when a command is received
    /// </summary>
    public override async Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken ct = default)
    {
        _logger.LogInformation("Executing command: {CommandName}", command.DeviceCmd);

        try
        {
            switch (command.DeviceCmd)
            {
                case "Start":
                    // Start device operation
                    _logger.LogInformation("Starting device operation...");
                    return true;

                case "Stop":
                    // Stop device operation
                    _logger.LogInformation("Stopping device operation...");
                    return true;

                case "SetParameter":
                    // Extract parameter from command.Parameters
                    if (command.Parameters.TryGetValue("name", out var nameObj) &&
                        command.Parameters.TryGetValue("value", out var valueObj))
                    {
                        var paramName = nameObj?.ToString() ?? "unknown";
                        _logger.LogInformation("Setting parameter {Name} = {Value}", paramName, valueObj);
                        return true;
                    }
                    _logger.LogWarning("SetParameter command missing 'name' or 'value' parameter");
                    return false;

                default:
                    _logger.LogWarning("Unknown command: {CommandName}", command.DeviceCmd);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {CommandName}", command.DeviceCmd);
            return false;
        }
    }

    ~MyFirstDevice()
    {
        // Unsubscribe from events
        DataReceived -= OnDataReceived;
    }
}
