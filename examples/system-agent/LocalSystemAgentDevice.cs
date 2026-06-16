using Microsoft.Extensions.Logging;
using SystemAgentExample.Communication;
using SystemAgentExample.Devices;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Communication.Common;

namespace SystemAgentExample;

/// <summary>
/// Pre-configured system agent device specialized for local system resource collection.
/// This concrete implementation automatically creates and manages the LocalSystemCommunication
/// instance for accessing local OS APIs.
/// </summary>
/// <remarks>
/// Carries <c>[DeviceType("system-monitor")]</c> so the host loader recognises
/// <c>AddDevice&lt;LocalSystemAgentDevice&gt;("...")</c> as a typed device, routing
/// its sensors onto the strong-typed dispatch path via
/// <see cref="SystemAgentExample.Sensors.LocalSystemMonitorConfiguration"/>.
/// </remarks>
[DeviceType(SystemAgentExample.Sensors.SystemMonitor.DeviceTypeName)]
public class LocalSystemAgentDevice : SystemAgentDeviceBase
{
    /// <summary>
    /// Creates a local system agent device with config key.
    /// Automatically retrieves configuration from context.DeviceConfigs[configKey].
    /// </summary>
    /// <param name="context">The application context</param>
    /// <param name="configKey">The configuration key from appsettings.json DeviceConfigs section</param>
    public LocalSystemAgentDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey])
    {
    }

    /// <summary>
    /// Creates a local system agent device with explicit configuration.
    /// Automatically creates LocalSystemCommunication for local OS API access.
    /// </summary>
    public LocalSystemAgentDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateLocalCommunication(context, configuration))
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Creates the LocalSystemCommunication for accessing local system APIs.
    /// </summary>
    private static LocalSystemCommunication CreateLocalCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var connectionSettings = configuration.ConnectionSettings ?? new ConnectionSettings();
        var logger = context.LoggerFactory.CreateLogger<CommunicationBase>();
        
        try
        {
            return new LocalSystemCommunication(connectionSettings, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create LocalSystemCommunication. This should not happen as LocalSystemResourceCollector handles hardware failures gracefully.");
            throw; // Re-throw as this is a critical initialization error
        }
    }

    /// <summary>
    /// Event handler for telemetry data received from device.
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        // Telemetry is sent to cloud, not logged to reduce log volume
    }

    /// <summary>
    /// Validates configuration updates before applying them.
    /// Override this method to add custom validation logic.
    /// </summary>
    protected override ConfigurationValidationResult ValidateConfigurationUpdate(
        SubNodeConfigUpdateMessage message)
    {
        // Call base validation first (handles sensor intervals, thresholds, etc.)
        var baseResult = base.ValidateConfigurationUpdate(message);
        if (!baseResult.IsValid)
            return baseResult;

        // Add custom validation for system-agent specific requirements
        var deviceConfigs = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs;
        if (deviceConfigs != null && deviceConfigs.TryGetValue(Configuration.DeviceName, out var desiredConfig))
        {
            // Validate that at least one sensor is enabled (business rule)
            if (desiredConfig.Sensors != null && desiredConfig.Sensors.Count > 0)
            {
                var enabledSensors = desiredConfig.Sensors
                    .Where(s => s.Report?.Enabled == true)
                    .ToList();

                if (enabledSensors.Count == 0)
                {
                    _logger.LogWarning("No sensors enabled in configuration update, system monitoring will be inactive");
                    // Note: This is a warning, not an error - we allow it but log the concern
                }
            }
        }

        return ConfigurationValidationResult.Success;
    }

    /// <summary>
    /// Validates configuration before device initialization.
    /// Implements AC: Configuration validation at startup with clear error messages.
    /// </summary>
    protected override async Task OnBeforeInitializeAsync(CancellationToken ct)
    {
        _logger.LogInformation("Validating device configuration before initialization...");

        // Validate DeviceName is configured
        if (string.IsNullOrWhiteSpace(Configuration.DeviceName))
        {
            throw new InvalidOperationException("DeviceName is required but not configured");
        }

        // Validate sensors are configured
        if (Configuration.Sensors.Count == 0)
        {
            _logger.LogWarning("No sensors configured for device '{DeviceName}', system monitoring will be inactive", 
                Configuration.DeviceName);
        }
        else
        {
            var enabledSensors = Configuration.Sensors
                .Where(s => s.Report?.Enabled == true)
                .ToList();

            if (enabledSensors.Count == 0)
            {
                _logger.LogWarning("No sensors enabled for device '{DeviceName}', system monitoring will be inactive", 
                    Configuration.DeviceName);
            }
            else
            {
                _logger.LogInformation("Configuration validation passed: {EnabledCount} sensors enabled for device '{DeviceName}'",
                    enabledSensors.Count, Configuration.DeviceName);
            }
        }

        await base.OnBeforeInitializeAsync(ct);
    }

    /// <summary>
    /// Validates configuration before applying config updates.
    /// Implements AC: Configuration validation during dynamic updates.
    /// When configuration is updated at runtime, this validates the new configuration
    /// before applying it to prevent invalid configurations from breaking the device.
    /// 
    /// Flow: Cloud ??NATS ??WedaCloudService ??DeviceBase.ApplyBaseConfigurationUpdateAsync ??OnBeforeConfigUpdateAsync
    /// </summary>
    protected override async Task OnBeforeConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct)
    {
        _logger.LogInformation("===========================================");
        _logger.LogInformation("[HOT-RELOAD] Configuration update received at {Timestamp}", DateTimeOffset.UtcNow.ToString("HH:mm:ss.fff"));
        _logger.LogInformation("    DeviceId: {DeviceId}", e.DeviceId);
        _logger.LogInformation("    SeqId: {SeqId}", e.Message?.SeqId.ToString() ?? "N/A");
        _logger.LogInformation("    Cmd: {Cmd}", e.Message?.Cmd ?? "N/A");
        _logger.LogInformation("===========================================");
        
        _logger.LogInformation("[HOT-RELOAD] Validating configuration update before applying...");

        // Extract the desired configuration from the update message
        var message = e.Message;
        if (message?.Data?.Cfg?.Desired != null)
        {
            // Validate using the overridden ValidateConfigurationUpdate method
            var validationResult = ValidateConfigurationUpdate(message);
            
            if (!validationResult.IsValid)
            {
                var error = $"[HOT-RELOAD] Configuration update validation failed: {validationResult.ErrorMessage}";
                _logger.LogError(error);
                _logger.LogError("Exit Code: 4 (InvalidConfigValue)");
                throw new InvalidOperationException(validationResult.ErrorMessage);
            }

            _logger.LogInformation("[HOT-RELOAD] Configuration update validation passed");
        }

        await base.OnBeforeConfigUpdateAsync(e, ct);
    }
}
