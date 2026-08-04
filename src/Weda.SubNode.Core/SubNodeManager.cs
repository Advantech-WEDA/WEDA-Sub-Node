using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Configuration.Validators;
using Weda.SubNode.Core.Commands;
using Weda.SubNode.Core.Configuration;
using Weda.SubNode.Core.Configuration.Validators;
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
    private readonly IRecordingService? _recordingService;
    private readonly CommandDispatcher? _commandDispatcher;
    private readonly IConfigurationCache? _configurationCache;
    private readonly ILogger<SubNodeManager> _logger;

    private readonly ResiliencePipeline<bool> _pipeline;
    private readonly ResiliencePipeline<bool> _uploadPipeline;

    private readonly ConcurrentDictionary<string, DeviceHandlers> _deviceHandlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IDisposable? _configSubscription;
    private IDisposable? _commandSubscription;
    private CancellationTokenSource? _configSyncCts;
    private Task? _configSyncTask;
    private bool _isInitialized;
    private string? _subNodeId;

    // Track last config update status for periodic reports
    private string _lastConfigUpdateStatus = ConfigUpdateStatus.Success;
    private string? _lastConfigUpdateError;

    public SubNodeManager(
        IWedaCloudService cloudService,
        SubNodeInfo subNodeInfo,
        ConnectionOptions connectionOptions,
        IDeviceRegistry deviceRegistry,
        ILogger<SubNodeManager> logger,
        CommandDispatcher? commandDispatcher = null,
        IRecordingService? recordingService = null,
        IConfigurationCache? configurationCache = null)
    {
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _subNodeInfo = subNodeInfo ?? throw new ArgumentNullException(nameof(subNodeInfo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _recordingService = recordingService;
        _commandDispatcher = commandDispatcher;
        _configurationCache = configurationCache;
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
            _subNodeInfo.DeviceId = _subNodeId;

            // Step 3: Subscribe to cloud events (once for all devices)
            await SubscribeToCloudEventsAsync(_subNodeId, ct);
            _logger.LogInformation("Subscribed to cloud events");

            // Step 4: Start configuration sync background task
            StartConfigSyncTask();

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
        Func<UpdateConfigurationEvent, Task<ConfigUpdateResult>> configHandler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentNullException.ThrowIfNull(configHandler);

        var handlers = new DeviceHandlers(configHandler);
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
    /// Handles SystemConfig updates (Serilog, WedaNode, Record settings).
    /// SubNodeManager handles this directly - no dispatch to devices.
    /// </summary>
    private Task HandleSystemConfigUpdateAsync(UpdateConfigurationEvent e)
    {
        _logger.LogInformation("Handling SystemConfig update: SeqId={SeqId}", e.Message?.SeqId);

        var systemCfg = e.Message?.Data?.Cfg?.Desired?.SystemCfg;
        if (systemCfg == null)
        {
            _logger.LogDebug("No SystemCfg in desired state");
            return Task.CompletedTask;
        }

        // Validate system configuration using registry
        var validatorRegistry = SystemConfigValidatorRegistry.CreateDefault();
        var validationContext = new SystemConfigValidationContext { DesiredConfig = systemCfg };
        var validationResult = validatorRegistry.ValidateAll(validationContext);

        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "Invalid SystemConfig, skipping update: {ErrorMessage}",
                validationResult.ErrorMessage);
            return Task.CompletedTask;
        }

        // Apply Record settings if present and recording service is available
        if (systemCfg.Record != null && _recordingService != null)
        {
            _recordingService.SetEnabled(systemCfg.Record.Enabled);
            _recordingService.UpdateBatchSettings(
                systemCfg.Record.BatchEnabled,
                systemCfg.Record.BatchMaxSamples);

            _logger.LogInformation(
                "Updated recording settings: Enabled={Enabled}, BatchEnabled={BatchEnabled}, BatchMaxSamples={BatchMaxSamples}",
                systemCfg.Record.Enabled,
                systemCfg.Record.BatchEnabled,
                systemCfg.Record.BatchMaxSamples);
        }

        // Note: Serilog settings require restart - not applied at runtime

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

        // Explicit devicecfg null (or DeviceConfigs null) means "reset to base configuration".
        // An absent devicecfg key or an empty object keeps the existing no-update behavior.
        if (message.Data?.Cfg?.Desired?.IsDeviceCfgReset == true)
        {
            await HandleDeviceConfigResetAsync(e, message);
            return;
        }

        await ProcessDeviceConfigTransactionAsync(e, applyMessage: message, reportMessage: message);
    }

    /// <summary>
    /// Outcome of a device-config transaction, used by the reset flow to decide
    /// whether the cached cloud configuration should be deleted.
    /// </summary>
    private enum DeviceConfigTransactionOutcome
    {
        NoTargets,
        AllSkipped,
        ValidationFailed,
        ApplyFailed,
        Success
    }

    /// <summary>
    /// Handles a config reset request (desired devicecfg explicitly set to null).
    /// Restores the pristine base configuration (devicecfg.json captured at startup)
    /// by running it through the normal transaction pipeline, then deletes the
    /// device-config cache so the base config also survives restarts.
    /// </summary>
    private async Task HandleDeviceConfigResetAsync(UpdateConfigurationEvent e, SubNodeConfigUpdateMessage message)
    {
        _logger.LogInformation("DeviceConfig reset requested (desired devicecfg is null): SeqId={SeqId}", message.SeqId);

        // Load the pristine base configuration directly from devicecfg.json
        var baseRaw = LoadBaseDeviceCfgJson();

        SubNodeDeviceCfgDto? baseDeviceCfg = null;
        string? error = null;

        if (!baseRaw.HasValue)
        {
            error = "Config reset requested but base configuration (devicecfg.json) is unavailable";
        }
        else
        {
            try
            {
                baseDeviceCfg = JsonSerializer.Deserialize<SubNodeDeviceCfgDto>(baseRaw.Value.GetRawText());
            }
            catch (JsonException ex)
            {
                error = $"Config reset failed: base configuration could not be parsed ({ex.Message})";
            }

            if (error == null && (baseDeviceCfg?.DeviceConfigs == null || baseDeviceCfg.DeviceConfigs.Count == 0))
            {
                error = "Config reset failed: base configuration contains no device configurations";
            }
        }

        if (error != null)
        {
            _logger.LogError("{Error}", error);
            var failedValidations = _deviceRegistry.GetAllDevices().ToDictionary(
                d => d.Configuration.DeviceName,
                d => ConfigUpdateValidationResult.Invalid(d.Configuration.SubNodeType.ToString(), error),
                StringComparer.OrdinalIgnoreCase);
            await PublishAggregatedReportAsync(e, message, failedValidations, null, ConfigUpdateStatus.Failed);
            return;
        }

        // Build a synthetic update message carrying the base configuration and run it
        // through the normal transaction pipeline (validate -> backup -> apply -> report).
        // The original message is used for reporting so the cloud sees its own desired
        // state (devicecfg null) echoed back together with the restored configuration.
        var applyMessage = new SubNodeConfigUpdateMessage
        {
            ProtoVer = message.ProtoVer,
            DeviceId = message.DeviceId,
            GroupId = message.GroupId,
            Cmd = message.Cmd,
            SeqId = message.SeqId,
            ReqSeqId = message.ReqSeqId,
            Timestamp = message.Timestamp,
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState
                {
                    Desired = new SubNodeDesiredConfigSections
                    {
                        DeviceCfg = baseDeviceCfg,
                        RawDeviceCfg = baseRaw!.Value.Clone()
                    }
                }
            }
        };

        var outcome = await ProcessDeviceConfigTransactionAsync(e, applyMessage, reportMessage: message);

        // The devicecfg cache represents the latest desired state from cloud. A reset
        // means that desired state no longer exists, so the cache must be deleted.
        // The incoming null is never persisted (reset is intercepted before dispatch),
        // and the synthetic base message saved during apply is removed here, so after
        // a successful reset the cache does not exist and the next startup loads
        // devicecfg.json directly.
        if (outcome is DeviceConfigTransactionOutcome.Success or DeviceConfigTransactionOutcome.AllSkipped)
        {
            await DeleteDeviceConfigCacheAsync();
            _logger.LogInformation("DeviceConfig reset completed: SeqId={SeqId}", message.SeqId);
        }
        else
        {
            _logger.LogWarning("DeviceConfig reset did not complete (outcome: {Outcome}); cache left intact", outcome);
        }
    }

    /// <summary>
    /// Deletes the device-config cache after a successful reset.
    /// </summary>
    private async Task DeleteDeviceConfigCacheAsync()
    {
        if (_configurationCache == null)
        {
            _logger.LogWarning("Configuration cache not available; cached devicecfg cannot be deleted after reset");
            return;
        }

        try
        {
            await _configurationCache.DeleteCacheAsync(SubscriptionTypes.DeviceConfig);
            _logger.LogInformation("Deleted device configuration cache: {CachePath}",
                _configurationCache.GetCacheFilePath(SubscriptionTypes.DeviceConfig));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete device configuration cache after reset");
        }
    }

    /// <summary>
    /// Path of the base device configuration file used to restore configuration on reset.
    /// Defaults to devicecfg.json in the working directory (same file the Host loads at
    /// startup). Settable for testing.
    /// </summary>
    public string BaseDeviceCfgPath { get; set; } =
        Path.Combine(Directory.GetCurrentDirectory(), "devicecfg.json");

    /// <summary>
    /// Loads the pristine base configuration from devicecfg.json.
    /// Returns null when the file is missing or unreadable.
    /// </summary>
    private JsonElement? LoadBaseDeviceCfgJson()
    {
        try
        {
            if (!File.Exists(BaseDeviceCfgPath))
            {
                _logger.LogWarning("Base configuration file not found: {Path}", BaseDeviceCfgPath);
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(BaseDeviceCfgPath));
            return doc.RootElement.Clone();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load base configuration from {Path}", BaseDeviceCfgPath);
            return null;
        }
    }

    /// <summary>
    /// Runs the device-config update transaction.
    /// Validation and apply use <paramref name="applyMessage"/>; aggregated reports are
    /// built from <paramref name="reportMessage"/> so its desired state is echoed back.
    /// For normal updates both are the same message; for resets the apply message is a
    /// synthetic message carrying the base configuration.
    /// </summary>
    private async Task<DeviceConfigTransactionOutcome> ProcessDeviceConfigTransactionAsync(
        UpdateConfigurationEvent e,
        SubNodeConfigUpdateMessage applyMessage,
        SubNodeConfigUpdateMessage reportMessage)
    {
        var message = applyMessage;

        // Determine which devices to update
        var deviceConfigs = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs;
        var targetDeviceNames = GetTargetDeviceNames(deviceConfigs);

        if (targetDeviceNames.Count == 0)
        {
            _logger.LogDebug("No target devices for config update");
            return DeviceConfigTransactionOutcome.NoTargets;
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
            await PublishAggregatedReportAsync(e, reportMessage, validationResults, null, ConfigUpdateStatus.Invalid);
            return DeviceConfigTransactionOutcome.ValidationFailed;
        }

        // Check if all devices are skipped (no update required)
        if (validationResults.Values.All(r => r.IsSkipped))
        {
            _logger.LogDebug("All devices skipped - no update required");
            return DeviceConfigTransactionOutcome.AllSkipped;
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

            await PublishAggregatedReportAsync(e, reportMessage, validationResults, applyResults, ConfigUpdateStatus.Failed);
            return DeviceConfigTransactionOutcome.ApplyFailed;
        }

        // ===== Phase 4: Update RawDeviceCfgJson and Publish Aggregated Success Report =====
        // Update RawDeviceCfgJson with the desired config from the message
        // This ensures Report content reflects the last applied configuration
        UpdateRawDeviceCfgJson(message, appliedDevices);

        var requiresCapsReupload = validationResults.Values.Any(r => r.RequiresCapsReupload) ||
                                   applyResults.Values.Any(r => r.RequiresCapsReupload);

        // New sensors require re-uploading DeviceCaps so the cloud sees SubNode-assigned DTMIs.
        // Upload BEFORE publishing the aggregated report so ShadowAgent's DB is
        // updated before the devicecfg.doc notification fires (null-DTDL fix).
        if (requiresCapsReupload)
        {
            _logger.LogInformation("New sensors detected, triggering DeviceCaps re-upload");
            var devices = _deviceRegistry.GetAllDevices().ToList();
            var configurations = new DeviceConfigurations(devices);
            await UploadDeviceConfigurationsAsync(configurations, default);
        }
        await PublishAggregatedReportAsync(e, reportMessage, validationResults, applyResults, ConfigUpdateStatus.Success);

        _logger.LogInformation("DeviceConfig update transaction completed successfully for {Count} device(s)",
            devicesToUpdate.Count);

        return DeviceConfigTransactionOutcome.Success;
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
        // Capture error message for periodic reports
        string? errorMessage = null;
        foreach (var (deviceName, validationResult) in validationResults)
        {
            if (errorMessage != null) break;

            if (!validationResult.IsValid)
            {
                errorMessage = $"Device '{deviceName}': {validationResult.ErrorMessage}";
            }
            else if (applyResults?.TryGetValue(deviceName, out var applyResult) == true &&
                     applyResult.Status == DeviceConfigUpdateStatus.Failed)
            {
                errorMessage = $"Device '{deviceName}': {applyResult.ErrorMessage}";
            }
        }

        // Update last config update status for periodic reports
        _lastConfigUpdateStatus = overallStatus;
        _lastConfigUpdateError = errorMessage;

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
    /// Routes command events to the appropriate CommandHandler via CommandDispatcher.
    /// </summary>
    private async Task RouteCommandAsync(ExecuteCommandEvent e)
    {
        _logger.LogDebug("Routing command: {Command}", e.Command?.DeviceCmd);

        if (_commandDispatcher == null || e.Command?.DeviceCmd == null)
        {
            _logger.LogWarning("Command cannot be routed: CommandDispatcher={HasDispatcher}, DeviceCmd={DeviceCmd}",
                _commandDispatcher != null, e.Command?.DeviceCmd);
            return;
        }

        var message = CreateCommandMessage(e);
        var result = await _commandDispatcher.DispatchAsync(message);

        if (result.IsError)
        {
            _logger.LogWarning("Command '{Command}' dispatch failed: {Error}",
                e.Command.DeviceCmd, result.FirstError.Description);
        }

                // Fire general event for external subscribers (all config types)
        if (CommandReceived != null)
        {
            try
            {
                await CommandReceived(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in general CommandUpdateReceived handler");
            }
        }
    }

    /// <summary>
    /// Creates a CommandMessage from an ExecuteCommandEvent.
    /// </summary>
    private static CommandMessage CreateCommandMessage(ExecuteCommandEvent e)
    {
        return new CommandMessage
        {
            Cmd = "deviceCmd",
            SeqId = e.Command!.SeqId,
            ReqSeqId = e.Command.ReqSeqId,
            Timestamp = e.Timestamp.ToUnixTimeMilliseconds(),
            Data = e.Command.RawData
        };
    }

    /// <summary>
    /// Starts the background task for periodic configuration sync.
    /// Reports aggregated configuration of all devices to cloud at the minimum ReportConfiguration period.
    /// </summary>
    private void StartConfigSyncTask()
    {
        // Get minimum ReportConfiguration period from all devices
        var devices = _deviceRegistry.GetAllDevices();
        var minPeriod = devices
            .Select(d => d.Configuration.Periods.ReportConfiguration)
            .Where(p => p > 0)
            .DefaultIfEmpty(0)
            .Min();

        if (minPeriod <= 0)
        {
            _logger.LogDebug("Configuration sync disabled (all devices have ReportConfiguration period = 0)");
            return;
        }

        _configSyncCts = new CancellationTokenSource();
        var ct = _configSyncCts.Token;

        _configSyncTask = Task.Run(async () =>
        {
            _logger.LogDebug("Starting configuration sync task with period {Period}ms", minPeriod);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (string.IsNullOrEmpty(_subNodeId))
                        continue;

                    var report = ConfigurationUpdateHelper.CreatePeriodicAggregatedReport(
                        _subNodeId,
                        _deviceRegistry,
                        _lastConfigUpdateStatus,
                        _lastConfigUpdateError);

                    await _cloudService.PublishConfigurationReportAsync(
                        SubscriptionTypes.DeviceConfig,
                        report,
                        ct);

                    _logger.LogDebug("Configuration sync completed for SubNode {SubNodeId}", _subNodeId);

                    await Task.Delay(minPeriod, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Configuration sync task cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in configuration sync task");
                    await Task.Delay(minPeriod, ct);
                }
            }
        }, ct);

        _logger.LogInformation("Configuration sync task started with period {Period}ms", minPeriod);
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Disposing SubNodeManager");

        // Cancel and wait for config sync task
        if (_configSyncCts != null)
        {
            await _configSyncCts.CancelAsync();
            _configSyncCts.Dispose();
            _configSyncCts = null;
        }

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
        Func<UpdateConfigurationEvent, Task<ConfigUpdateResult>> ConfigHandler);
}
