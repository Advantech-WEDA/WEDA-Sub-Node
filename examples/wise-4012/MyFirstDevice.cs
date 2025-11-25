using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Configuration;
using Weda.SubNode.Devices.Generic;

namespace Wise4012Example;

/// <summary>
/// MyFirstDevice - A custom device implementation
/// Inherits from TcpModbusDevice to get Modbus TCP protocol support with automatic communication setup.
///
/// Demonstrates UC9868: Configuration update from cloud.
///
/// The framework (DeviceBase) automatically handles:
/// - Base configuration updates (sensors, periods)
/// - Persisting configuration to cache (.device-config-cache.json)
///
/// This custom device only needs to:
/// - Report configuration update status back to cloud (updating/success/failed)
/// - Handle any device-specific configuration if needed
/// </summary>
public class MyFirstDevice : TcpModbusDevice
{
    private readonly string _deviceTypeName;

    /// <summary>
    /// Creates MyFirstDevice using ApplicationContext.
    /// Configuration is automatically retrieved from context.
    /// </summary>
    public MyFirstDevice(IWedaApplicationContext context)
        : base(context)
    {
        // Store device type name for configuration reports
        _deviceTypeName = "myFirstDevice";

        // Subscribe to DataReceived event to process telemetry
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Event handler for telemetry data received from device
    /// Prints all sensor values from configuration (channel.0~3)
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
                _logger.LogInformation("{SensorName}: {Value} (Enabled={Enabled})",
                    sensor.Name,
                    measure.Value,
                    sensor.Config.Enabled);
            }
        }
    }

    /// <summary>
    /// UC9868: Handle configuration update from cloud.
    ///
    /// Note: Base configuration (sensors, periods) is already applied and cached
    /// by the framework before this hook is called.
    ///
    /// This hook is used to:
    /// 1. Report status back to cloud (updating/success/failed)
    /// 2. Handle any device-specific custom configuration
    /// </summary>
    protected override async Task OnAfterConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct)
    {
        _logger.LogInformation("Configuration update received for device: {DeviceId}", DeviceId);

        // Extract the SubNodeConfigurationUpdateMessage from the event
        if (!e.Configuration.TryGetValue("message", out var messageObj) ||
            messageObj is not SubNodeConfigurationUpdateMessage message)
        {
            _logger.LogWarning("Configuration update event does not contain a valid message");
            return;
        }

        // Step 1: Validate the configuration
        if (!ConfigurationUpdateHelper.ValidateConfigurationUpdate(message, out var validationError))
        {
            _logger.LogWarning("Configuration update validation failed: {Error}", validationError);

            // Publish invalid report back to cloud
            var invalidReport = ConfigurationUpdateHelper.CreateInvalidReport(
                message, Configuration, _deviceTypeName, validationError!);
            await _context.CloudService.PublishConfigurationReportAsync(invalidReport, ct);
            return;
        }

        // Step 2: Publish "updating" status (desired + current reported state)
        var updatingReport = ConfigurationUpdateHelper.CreateUpdatingReport(
            message, Configuration, _deviceTypeName);
        await _context.CloudService.PublishConfigurationReportAsync(updatingReport, ct);
        _logger.LogInformation("Published 'updating' status report");

        try
        {
            // Step 3: Handle device-specific custom configuration here
            // Base sensors and periods are already applied by framework
            // Example: Apply custom properties specific to this device type
            // var customConfig = message.Data?.Cfg?.Desired?.CustomProperties;
            // ApplyCustomConfiguration(customConfig);

            // Step 4: Publish "success" status (desired + updated reported state)
            var successReport = ConfigurationUpdateHelper.CreateSuccessReport(
                message, Configuration, _deviceTypeName);
            await _context.CloudService.PublishConfigurationReportAsync(successReport, ct);

            _logger.LogInformation("Configuration update completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply custom configuration update");

            // Publish failure report back to cloud
            var failedReport = ConfigurationUpdateHelper.CreateFailedReport(
                message, Configuration, _deviceTypeName, ex.Message);
            await _context.CloudService.PublishConfigurationReportAsync(failedReport, ct);
        }
    }

    ~MyFirstDevice()
    {
        // Unsubscribe from events
        DataReceived -= OnDataReceived;
    }
}
