using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Polly;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Configuration;
using Weda.SubNode.Core.Context;
using Weda.SubNode.Core.Policies;

namespace Weda.SubNode.Core;

/// <summary>
/// Manages SubNode-level operations that should only happen once for all devices.
/// Implements cloud connection, SubNode registration, event subscription, and hybrid event routing.
/// </summary>
public sealed class SubNodeManager : ISubNodeManager, IAsyncDisposable
{
    private readonly IWedaCloudService _cloudService;
    private readonly SubNodeInfo _subNodeInfo;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly ILogger<SubNodeManager> _logger;

    private readonly ResiliencePipeline<bool> _pipeline;
    private readonly ResiliencePipeline<bool> _uploadPipeline;

    private readonly ConcurrentDictionary<string, DeviceHandlers> _deviceHandlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IDisposable? _configSubscription;
    private IDisposable? _commandSubscription;
    private bool _isInitialized;
    private string? _subNodeId;


    public SubNodeManager(
        IWedaCloudService cloudService,
        SubNodeInfo subNodeInfo,
        ConnectionOptions connectionOptions,
        IDeviceRegistry deviceRegistry,
        ILogger<SubNodeManager> logger)
    {
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _subNodeInfo = subNodeInfo ?? throw new ArgumentNullException(nameof(subNodeInfo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var policyOptions = ConnectionPolicyOptions.FromConnectionOptions(connectionOptions);
        _pipeline = ConnectionPolicies.CreateDeviceConnectionPipeline(_logger, policyOptions);
        _deviceRegistry = deviceRegistry;

        // Unlimited retry pipeline for upload (handles Device.NotFound with exponential backoff)
        _uploadPipeline = RetryPolicyFactory.CreateAlwaysRetryBool(
            logger: _logger,
            initialDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromSeconds(30));
    }

    /// <inheritdoc />
    public bool IsInitialized => _isInitialized;

    /// <inheritdoc />
    public string? SubNodeId => _subNodeId;

    /// <inheritdoc />
    public event Func<UpdateConfigurationEvent, Task>? ConfigurationUpdateReceived;

    /// <inheritdoc />
    public event Func<ExecuteCommandEvent, Task>? CommandReceived;

    /// <inheritdoc />
    public async Task<bool> InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized)
        {
            _logger.LogDebug("SubNodeManager already initialized");
            return true;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_isInitialized) return true;

            _logger.LogInformation("Initializing SubNodeManager for SubNode: {SubNodeName}", _subNodeInfo.Name);

            // Step 1: Connect to cloud service
            var connected = await _pipeline.ExecuteAsync(
              async token => await _cloudService.ConnectAsync(token),
              ct);

            if (!connected)
            {
                _logger.LogError("Failed to connect to WedaNode");
                return false;
            }

            // Step 2: Register SubNode with cloud
            _subNodeId = await EnsureSubNodeRegisteredAsync(ct);
            if (string.IsNullOrEmpty(_subNodeId))
            {
                _logger.LogError("Failed to register SubNode with cloud");
                return false;
            }
            _logger.LogInformation("SubNode registered with ID: {SubNodeId}", _subNodeId);

            // Step 3: Subscribe to cloud events (once for all devices)
            await SubscribeToCloudEventsAsync(_subNodeId, ct);
            _logger.LogInformation("Subscribed to cloud events");

            _isInitialized = true;
            _logger.LogInformation("SubNodeManager initialization completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SubNodeManager");
            return false;
        }
        finally
        {
            _initLock.Release();
        }
    }
    /// <inheritdoc />
    public async Task<bool> UploadDeviceConfigurationsAsync(DeviceConfigurations configurations, CancellationToken cancellationToken)
    {
        if (!IsInitialized || string.IsNullOrEmpty(_subNodeId))
        {
            _logger.LogError("Cannot upload configurations: SubNodeManager has not been initialized");
            throw new InvalidOperationException("SubNodeManager must be initialized before uploading configuration");
        }

        if (configurations.Count == 0)
        {
            _logger.LogWarning("No device configurations to upload");
            return true;
        }

        _logger.LogInformation(
            "Uploading {Count} device configuration(s) to cloud",
            configurations.Count);

        // Use retry pipeline with Device.NotFound handling
        // This handles the case where cloud has reset and lost the device registration
        var success = await _uploadPipeline.ExecuteAsync(async token =>
        {
            token.ThrowIfCancellationRequested();

            try
            {
                var result = await _cloudService.UploadDeviceConfigurationsAsync(configurations, token);

                if (!result.IsError)
                {
                    _logger.LogInformation("Device configurations uploaded successfully");
                    return true;
                }

                // Check if device not found (need to re-register and retry)
                var isNotFound = result.Errors.Any(e =>
                    e.Type == ErrorOr.ErrorType.NotFound ||
                    string.Equals(e.Code, "Device.NotFound", StringComparison.OrdinalIgnoreCase));

                var errorsText = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));

                if (isNotFound)
                {
                    _logger.LogWarning(
                        "Device not found on cloud; resetting registration and retrying. SubNodeId={SubNodeId}, Errors={Errors}",
                        _subNodeId,
                        errorsText);

                    // Reset registration cache and re-register
                    await _cloudService.ResetRegistrationAsync(token);

                    // Re-register SubNode
                    var newSubNodeId = await EnsureSubNodeRegisteredAsync(token);
                    if (!string.IsNullOrEmpty(newSubNodeId))
                    {
                        _subNodeId = newSubNodeId;
                        _subNodeInfo.DeviceId = newSubNodeId;

                        // Re-enrich all configurations with new SubNodeId
                        ReEnrichConfigurations(configurations, newSubNodeId);

                        _logger.LogInformation("SubNode re-registered with new ID: {SubNodeId}", newSubNodeId);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Upload attempt failed; will retry. Errors={Errors}",
                        errorsText);
                }

                // Return false to trigger retry
                return false;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Upload attempt failed due to exception; will retry");
                return false;
            }
        }, cancellationToken);

        return success;
    }

    /// <summary>
    /// Re-enriches all device configurations with a new SubNodeId.
    /// Called when cloud registration is reset and a new SubNodeId is obtained.
    /// </summary>
    private void ReEnrichConfigurations(DeviceConfigurations configurations, string newSubNodeId)
    {
        foreach (var (deviceName, config) in configurations)
        {
            config.DeviceId = newSubNodeId;

            foreach (var sensor in config.Sensors)
            {
                sensor.ResourceId = Utilities.ResourceIdGenerator.GenerateSensorResourceId(
                    newSubNodeId,
                    deviceName,
                    sensor.Name,
                    groupId: "weda");
                sensor.DeviceResourceId = newSubNodeId;
            }
        }

        _logger.LogDebug("Re-enriched {Count} device configurations with new SubNodeId: {SubNodeId}",
            configurations.Count, newSubNodeId);
    }

    /// <inheritdoc />
    public void RegisterDeviceHandler(
        string deviceName,
        Func<UpdateConfigurationEvent, Task<ConfigUpdateResult>> configHandler,
        Func<ExecuteCommandEvent, Task>? commandHandler = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentNullException.ThrowIfNull(configHandler);

        var handlers = new DeviceHandlers(configHandler, commandHandler);
        _deviceHandlers.AddOrUpdate(deviceName, handlers, (_, _) => handlers);

        _logger.LogDebug("Registered device handler for: {DeviceName}", deviceName);
    }

    /// <inheritdoc />
    public void UnregisterDeviceHandler(string deviceName)
    {
        if (_deviceHandlers.TryRemove(deviceName, out _))
        {
            _logger.LogDebug("Unregistered device handler for: {DeviceName}", deviceName);
        }
    }

    /// <summary>
    /// Ensures SubNode is registered with cloud service.
    /// </summary>
    private async Task<string?> EnsureSubNodeRegisteredAsync(CancellationToken ct)
    {
        _logger.LogDebug("Ensuring SubNode '{SubNodeName}' is registered", _subNodeInfo.Name);

        var subNodeDeviceInfo = new DeviceInfo
        {
            DeviceName = _subNodeInfo.Name,
            SubNodeType = _subNodeInfo.SubNodeType,
            Manufacturer = _subNodeInfo.Manufacturer,
            Model = $"{_subNodeInfo.Model} v{_subNodeInfo.SwVersion}",
            DeviceId = _subNodeInfo.DeviceId
        };

        return await _cloudService.GetOrRegisterDeviceIdAsync(subNodeDeviceInfo, ct);
    }

    /// <summary>
    /// Subscribes to cloud events (configuration updates and commands).
    /// </summary>
    private async Task SubscribeToCloudEventsAsync(string subNodeId, CancellationToken ct)
    {
        _logger.LogDebug("Subscribing to cloud events for SubNode: {SubNodeId}", subNodeId);

        // Subscribe to configuration updates
        _configSubscription = await _cloudService.SubscribeConfigurationUpdatesAsync(
            subNodeId,
            RouteConfigurationUpdateAsync,
            ct);

        // Subscribe to commands
        _commandSubscription = await _cloudService.SubscribeCommandsAsync(
            subNodeId,
            RouteCommandAsync,
            ct);
    }

    /// <summary>
    /// Routes configuration update events.
    /// SubNode is the Aggregate Root - all config updates are handled here first:
    /// - SystemConfig: SubNodeManager handles directly (Serilog, WedaNode settings)
    /// - CustomConfig: SubNodeManager handles directly + fires event for external subscribers
    /// - DeviceConfig: SubNodeManager dispatches to individual devices
    /// </summary>
    private async Task RouteConfigurationUpdateAsync(UpdateConfigurationEvent e)
    {
        _logger.LogDebug("Routing configuration update: ConfigType={ConfigType}, SeqId={SeqId}",
            e.ConfigType.Value, e.Message?.SeqId);

        // Route based on ConfigType - SubNode is the Aggregate Root
        if (e.ConfigType == SubscriptionTypes.SystemConfig)
        {
            await HandleSystemConfigUpdateAsync(e);
        }
        else if (e.ConfigType == SubscriptionTypes.CustomConfig)
        {
            await HandleCustomConfigUpdateAsync(e);
        }
        else if (e.ConfigType == SubscriptionTypes.DeviceConfig)
        {
            await HandleDeviceConfigUpdateAsync(e);
        }
        else
        {
            _logger.LogWarning("Unknown ConfigType: {ConfigType}", e.ConfigType.Value);
        }

        // Fire general event for external subscribers (all config types)
        if (ConfigurationUpdateReceived != null)
        {
            try
            {
                await ConfigurationUpdateReceived(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in general ConfigurationUpdateReceived handler");
            }
        }
    }

    /// <summary>
    /// Handles SystemConfig updates (Serilog, WedaNode settings).
    /// SubNodeManager handles this directly - no dispatch to devices.
    /// </summary>
    private Task HandleSystemConfigUpdateAsync(UpdateConfigurationEvent e)
    {
        _logger.LogInformation("Handling SystemConfig update: SeqId={SeqId}", e.Message?.SeqId);

        // TODO: Apply system configuration changes
        // - Serilog settings
        // - WedaNode settings
        // - Other SubNode-level settings

        _logger.LogDebug("SystemConfig update processed");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles CustomConfig updates (user-defined settings).
    /// SubNodeManager handles this directly - no dispatch to devices.
    /// </summary>
    private Task HandleCustomConfigUpdateAsync(UpdateConfigurationEvent e)
    {
        _logger.LogInformation("Handling CustomConfig update: SeqId={SeqId}", e.Message?.SeqId);

        // TODO: Apply custom configuration changes
        // Custom config is SubNode-level, handled here
        // External subscribers can listen via ConfigurationUpdateReceived event

        _logger.LogDebug("CustomConfig update processed");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles DeviceConfig updates with transaction semantics.
    /// SubNode is the Aggregation Root - all devices must succeed or all rollback.
    /// Publishes a single aggregated report containing all device configurations.
    /// </summary>
    private async Task HandleDeviceConfigUpdateAsync(UpdateConfigurationEvent e)
    {
        _logger.LogInformation("Handling DeviceConfig update: SeqId={SeqId}", e.Message?.SeqId);

        var message = e.Message;
        if (message == null)
        {
            _logger.LogWarning("DeviceConfig update message is null");
            return;
        }

        // Determine which devices to update
        var deviceConfigs = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs;
        var targetDeviceNames = GetTargetDeviceNames(deviceConfigs);

        if (targetDeviceNames.Count == 0)
        {
            _logger.LogDebug("No target devices for config update");
            return;
        }

        _logger.LogInformation("Processing config update for {Count} device(s): {Devices}",
            targetDeviceNames.Count, string.Join(", ", targetDeviceNames));

        // ===== Phase 1: Validate All =====
        var validationResults = new Dictionary<string, ConfigUpdateValidationResult>();
        var hasValidationFailure = false;

        foreach (var deviceName in targetDeviceNames)
        {
            var device = _deviceRegistry.FindDevice(deviceName);
            if (device == null)
            {
                _logger.LogWarning("Device {DeviceName} not found in registry", deviceName);
                continue;
            }

            var validationResult = await device.ValidateConfigurationUpdateAsync(message, default);
            validationResults[deviceName] = validationResult;

            if (!validationResult.IsValid)
            {
                hasValidationFailure = true;
                _logger.LogWarning("Validation failed for device {DeviceName}: {Error}",
                    deviceName, validationResult.ErrorMessage);
            }
        }

        // If any validation failed, publish aggregated failed report and return
        if (hasValidationFailure)
        {
            _logger.LogError("Transaction aborted: validation failed for one or more devices");
            await PublishAggregatedReportAsync(e, message, validationResults, null, ConfigUpdateStatus.Invalid);
            return;
        }

        // Check if all devices are skipped (no update required)
        if (validationResults.Values.All(r => r.IsSkipped))
        {
            _logger.LogDebug("All devices skipped - no update required");
            return;
        }

        // ===== Phase 2: Create Backups =====
        var backups = new Dictionary<string, DeviceConfigurationBackup>();
        var devicesToUpdate = validationResults
            .Where(kvp => !kvp.Value.IsSkipped)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var deviceName in devicesToUpdate)
        {
            var device = _deviceRegistry.FindDevice(deviceName);
            if (device != null)
            {
                backups[deviceName] = device.CreateConfigurationBackup();
            }
        }

        _logger.LogDebug("Created backups for {Count} device(s)", backups.Count);

        // ===== Phase 3: Apply All (with rollback on failure) =====
        var applyResults = new Dictionary<string, ConfigUpdateResult>();
        var appliedDevices = new List<string>();
        var hasApplyFailure = false;
        string? failedDeviceName = null;

        foreach (var deviceName in devicesToUpdate)
        {
            var device = _deviceRegistry.FindDevice(deviceName);
            if (device == null)
                continue;

            var backup = backups[deviceName];
            var applyResult = await device.ApplyValidatedConfigurationAsync(message, backup, default);
            applyResults[deviceName] = applyResult;

            if (applyResult.Status == DeviceConfigUpdateStatus.Failed)
            {
                hasApplyFailure = true;
                failedDeviceName = deviceName;
                _logger.LogError("Apply failed for device {DeviceName}: {Error}",
                    deviceName, applyResult.ErrorMessage);
                break;
            }

            appliedDevices.Add(deviceName);
            _logger.LogDebug("Successfully applied config to device {DeviceName}", deviceName);
        }

        // Rollback if any apply failed
        if (hasApplyFailure)
        {
            // Include the failed device in rollback (it may have partial changes)
            var devicesToRollback = new List<string>(appliedDevices);
            if (failedDeviceName != null && !devicesToRollback.Contains(failedDeviceName))
            {
                devicesToRollback.Add(failedDeviceName);
            }

            _logger.LogWarning("Transaction failed at device {DeviceName}, rolling back {Count} device(s)",
                failedDeviceName, devicesToRollback.Count);

            foreach (var deviceName in devicesToRollback)
            {
                var device = _deviceRegistry.FindDevice(deviceName);
                if (device != null && backups.TryGetValue(deviceName, out var backup))
                {
                    await device.RollbackConfigurationAsync(backup, default);
                    _logger.LogInformation("Rolled back device {DeviceName}", deviceName);
                }
            }

            await PublishAggregatedReportAsync(e, message, validationResults, applyResults, ConfigUpdateStatus.Failed);
            return;
        }

        // ===== Phase 4: Update RawDeviceCfgJson and Publish Aggregated Success Report =====
        // Update RawDeviceCfgJson with the desired config from the message
        // This ensures Report content reflects the last applied configuration
        UpdateRawDeviceCfgJson(message, appliedDevices);

        var hasDtmiDelta = validationResults.Values.Any(r => r.HasDtmiDelta) ||
                          applyResults.Values.Any(r => r.HasDtmiDelta);

        await PublishAggregatedReportAsync(e, message, validationResults, applyResults, ConfigUpdateStatus.Success);

        // Re-upload configurations if DTMI delta detected
        if (hasDtmiDelta)
        {
            _logger.LogInformation("DTMI delta detected, triggering configuration re-upload");
            var devices = _deviceRegistry.GetAllDevices().ToList();
            var configurations = new DeviceConfigurations(devices);
            await UploadDeviceConfigurationsAsync(configurations, default);
        }

        _logger.LogInformation("DeviceConfig update transaction completed successfully for {Count} device(s)",
            devicesToUpdate.Count);
    }

    /// <summary>
    /// Gets the list of device names to update from the message.
    /// </summary>
    private List<string> GetTargetDeviceNames(Dictionary<string, SubNodeDeviceConfigDto>? deviceConfigs)
    {
        if (deviceConfigs == null || deviceConfigs.Count == 0)
        {
            // Broadcast to all registered devices
            return _deviceHandlers.Keys.ToList();
        }

        // Only update devices specified in the message
        return deviceConfigs.Keys
            .Where(name => _deviceHandlers.ContainsKey(name))
            .ToList();
    }

    /// <summary>
    /// Publishes an aggregated configuration report containing all device configurations.
    /// </summary>
    private async Task PublishAggregatedReportAsync(
        UpdateConfigurationEvent e,
        SubNodeConfigUpdateMessage message,
        Dictionary<string, ConfigUpdateValidationResult> validationResults,
        Dictionary<string, ConfigUpdateResult>? applyResults,
        string overallStatus)
    {
        var report = ConfigurationUpdateHelper.CreateAggregatedReport(
            message,
            _deviceRegistry,
            validationResults,
            applyResults,
            overallStatus);

        _logger.LogInformation("Publishing aggregated {Status} report for {Count} device(s)",
            overallStatus, validationResults.Count);

        await _cloudService.PublishConfigurationReportAsync(e.ConfigType, report, default);
    }

    /// <summary>
    /// Updates RawDeviceCfgJson on each successfully applied device with the desired configuration.
    /// This ensures Report content reflects the last applied cloud configuration.
    /// </summary>
    private void UpdateRawDeviceCfgJson(
        SubNodeConfigUpdateMessage message,
        List<string> appliedDevices)
    {
        // Use RawDeviceCfg (raw JSON) to preserve original structure (e.g., SensorInfo)
        var rawDeviceCfg = message.Data?.Cfg?.Desired?.RawDeviceCfg;
        if (!rawDeviceCfg.HasValue)
            return;

        try
        {
            var rawJson = rawDeviceCfg.Value.Clone();

            // Update each applied device's RawDeviceCfgJson
            foreach (var deviceName in appliedDevices)
            {
                var device = _deviceRegistry.FindDevice(deviceName);
                if (device != null)
                {
                    device.Configuration.RawDeviceCfgJson = rawJson;
                    _logger.LogDebug("Updated RawDeviceCfgJson for device {DeviceName}", deviceName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update RawDeviceCfgJson for applied devices");
        }
    }

    /// <summary>
    /// Routes command events to appropriate device handlers.
    /// </summary>
    private async Task RouteCommandAsync(ExecuteCommandEvent e)
    {
        _logger.LogDebug("Routing command: {Command}", e.Command?.DeviceCmd);

        // Commands are typically broadcast to all devices that can handle them
        // The device determines if it should handle based on command type
        foreach (var (deviceName, handlers) in _deviceHandlers)
        {
            if (handlers.CommandHandler != null)
            {
                try
                {
                    await handlers.CommandHandler(e);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in command handler for device: {DeviceName}", deviceName);
                }
            }
        }

        // Fire general event
        if (CommandReceived != null)
        {
            try
            {
                await CommandReceived(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in general CommandReceived handler");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Disposing SubNodeManager");

        _configSubscription?.Dispose();
        _commandSubscription?.Dispose();

        await _cloudService.DisconnectAsync();

        _initLock.Dispose();
        _deviceHandlers.Clear();

        _logger.LogInformation("SubNodeManager disposed");
    }

    /// <summary>
    /// Container for device-specific event handlers.
    /// </summary>
    private sealed record DeviceHandlers(
        Func<UpdateConfigurationEvent, Task<ConfigUpdateResult>> ConfigHandler,
        Func<ExecuteCommandEvent, Task>? CommandHandler);
}
