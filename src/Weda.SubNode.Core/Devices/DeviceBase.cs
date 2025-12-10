using System.Collections.Concurrent;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Configuration;
using Weda.SubNode.Core.Devices.Lifecycle;
using Weda.SubNode.Core.Telemetry;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Ultra-lightweight device base (~200 lines vs original 538 lines).
/// All complexity delegated to DeviceOrchestrator and DeviceInitializer.
/// Communication is injected via constructor (Dependency Inversion Principle).
/// </summary>
public abstract class DeviceBase : IDevice, ILifecycleHooks
{
    protected readonly ILogger<DeviceBase> _logger;
    protected readonly IWedaCloudService _cloudService;
    protected readonly ICommunication _communication;
    protected readonly IWedaApplicationContext _context;
    protected readonly DeviceOrchestrator _orchestrator;
    protected readonly DeviceInitializer _initializer;

    private CancellationTokenSource? _runningCts;
    private Task? _configSyncTask;
    private Task? _batchSendTask;
    private readonly SemaphoreSlim _configUpdateLock = new(1, 1);

    /// <summary>
    /// Batch queue for telemetry measures that have been processed through Transform and Filter.
    /// Data is enqueued by derived classes via EnqueueTelemetryAsync and sent by the batch send task.
    /// </summary>
    protected readonly ConcurrentQueue<TelemetryMeasure> _telemetryBatch = new();

    public DeviceConfiguration Configuration { get; }
    public string DeviceId => _orchestrator.DeviceId;
    public string DeviceName => Configuration.DeviceInfo.DeviceName;
    public DeviceType DeviceType => Configuration.DeviceInfo.DeviceType;
    public DeviceInfo DeviceInfo => Configuration.DeviceInfo;
    public IReadOnlyDictionary<string, object> Properties => Configuration.Properties;
    public DeviceStatus Status => _orchestrator.StateMachine.CurrentStatus;
    public CommunicationState ConnectionState => _communication.State;

    /// <summary>
    /// Calculated telemetry batch send period (minimum of all enabled sensor intervals).
    /// Used by derived device classes for batch upload scheduling.
    /// </summary>
    protected int CalculatedSendTelemetryPeriod { get; private set; }

    protected DeviceBase(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        ICommunication communication)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = context.GetLogger<DeviceBase>();
        _cloudService = context.CloudService;

        // Validate sensor intervals and calculate send period
        ValidateSensorIntervals(configuration);
        CalculatedSendTelemetryPeriod = CalculateSendTelemetryPeriod(configuration);

        Configuration.LoadDtdl();

        // Single orchestrator manages all complexity
        _orchestrator = new DeviceOrchestrator(
            context,
            _communication,
            this, // ILifecycleHooks
            configuration, // Pass full configuration for sensor-level transform/filter support
            configuration.DeviceId); // Pass deviceId from configuration

        // Single initializer handles registration
        _initializer = new DeviceInitializer(
            context.CloudService,
            context.GetLogger<DeviceInitializer>());

        // Wire events
        WireEvents();

        // Register device with context's device registry
        _context.DeviceRegistry.Register(this);
    }

    // ===== IDevice Lifecycle =====

    public async Task<bool> InitializeAsync(CancellationToken ct = default)
        => !(await _orchestrator.LifecycleManager.InitializeAsync(ct)).IsError;

    public async Task<bool> StartAsync(CancellationToken ct = default)
    {
        if (Status != DeviceStatus.Ready && !await InitializeAsync(ct))
            return false;
        return !(await _orchestrator.LifecycleManager.StartAsync(ct)).IsError;
    }

    public async Task<bool> StopAsync(CancellationToken ct = default)
        => !(await _orchestrator.LifecycleManager.StopAsync(ct)).IsError;

    // ===== ILifecycleHooks =====

    async Task<ErrorOr<Success>> ILifecycleHooks.OnInitializeAsync(CancellationToken ct)
    {
        var connResult = await _orchestrator.ConnectionManager.EstablishConnectionsAsync(ct);
        if (connResult.IsError) return connResult.Errors;

        await OnBeforeInitializeAsync(ct);

        var deviceId = await _initializer.InitializeDeviceAsync(Configuration, ct);

        // Set the device ID after registration
        _orchestrator.SetDeviceId(deviceId);

        // Subscribe to cloud events based on DeviceOptions (Application Layer)
        var deviceOptions = _context.DeviceOptions;
        var subResult = await _orchestrator.ConnectionManager.SubscribeToCloudEventsAsync(
            deviceId,
            deviceOptions.EnableConfigUpdates,
            deviceOptions.EnableCommands,
            ct);
        if (subResult.IsError) return subResult.Errors;

        await OnAfterInitializeAsync(ct);
        return Result.Success;
    }

    async Task<ErrorOr<Success>> ILifecycleHooks.OnStartAsync(CancellationToken ct)
    {
        _runningCts = new CancellationTokenSource();
        var cts = _runningCts.Token;

        // Start device-specific background tasks (polling, subscription, etc.)
        _ = StartBackgroundTasksAsync(cts);

        // Start batch send task - sends collected telemetry at CalculatedSendTelemetryPeriod interval
        var sendPeriod = CalculatedSendTelemetryPeriod;
        _batchSendTask = Task.Run(async () =>
        {
            _logger.LogDebug("Starting batch send task with period {Period}ms for device {DeviceId}", sendPeriod, DeviceId);

            try
            {
                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(sendPeriod, cts);

                    var batch = new List<TelemetryMeasure>();
                    while (_telemetryBatch.TryDequeue(out var measure))
                    {
                        batch.Add(measure);
                    }

                    if (batch.Count > 0)
                    {
                        _logger.LogDebug(
                            "Sending telemetry batch: {Count} measures for device {DeviceId}",
                            batch.Count, DeviceId);
                        await SendTelemetryAsync(batch, cts);
                    }
                }
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Send remaining data before exit
                var remaining = new List<TelemetryMeasure>();
                while (_telemetryBatch.TryDequeue(out var measure))
                {
                    remaining.Add(measure);
                }
                if (remaining.Count > 0)
                {
                    _logger.LogDebug("Sending remaining {Count} measures before shutdown for device {DeviceId}", remaining.Count, DeviceId);
                    await SendTelemetryAsync(remaining, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch send task for device {DeviceId}", DeviceId);
            }
        }, cts);

        // Start config sync task (core functionality in DeviceBase)
        var configSyncPeriod = Configuration.Periods.ReportConfiguration;
        if (configSyncPeriod > 0)
        {
            _configSyncTask = Task.Run(async () =>
            {
                _logger.LogDebug("Starting configuration sync task with period {Period}ms", configSyncPeriod);

                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        await ReportConfigurationAsync(cts);
                        _logger.LogDebug("Configuration sync completed for device {DeviceId}", DeviceId);
                    }
                    catch (OperationCanceledException) when (cts.IsCancellationRequested)
                    {
                        _logger.LogInformation("Configuration sync task cancelled for device {DeviceId}", DeviceId);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in configuration sync task for device {DeviceId}", DeviceId);
                    }

                    await Task.Delay(configSyncPeriod, cts);
                }
            }, cts);
        }
        else
        {
            _logger.LogDebug("Configuration sync task disabled (ReportConfiguration period = 0)");
        }

        return await Task.FromResult(Result.Success);
    }

    async Task<ErrorOr<Success>> ILifecycleHooks.OnStopAsync(CancellationToken ct)
    {
        if (_runningCts != null)
        {
            await _runningCts.CancelAsync();
            _runningCts = null;
        }
        await _orchestrator.ConnectionManager.DisconnectAsync(ct);
        return Result.Success;
    }

    async Task<ErrorOr<Success>> ILifecycleHooks.OnDisposeAsync(CancellationToken ct)
    {
        if (_communication is IDisposable disp) disp.Dispose();
        _runningCts?.Dispose();
        return await Task.FromResult(Result.Success);
    }

    // ===== IDevice Operations =====

    public abstract Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken ct = default);

    public async Task<bool> SendTelemetryAsync(List<TelemetryMeasure> measures, CancellationToken ct = default)
    {
        try
        {
            // Use Polly pipeline for resilient telemetry sending
            // SendAsync only sends to cloud, no Transform/Filter (already done before enqueue)
            var result = await _orchestrator.OperationPipeline.ExecuteAsync(
                async c => await _orchestrator.TelemetryPipeline.SendAsync(measures, c),
                ct);

            return !result.IsError;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send telemetry for device {DeviceId} after all retries", DeviceId);
            return false;
        }
    }

    public async Task SendTelemetryAsync(IAsyncEnumerable<TelemetryMeasure> data, CancellationToken ct = default, params IDspFilter[] filters)
    {
        var list = new List<TelemetryMeasure>();
        await foreach (var m in data.WithCancellation(ct)) list.Add(m);
        await SendTelemetryAsync(list, ct);
    }

    /// <summary>
    /// Processes telemetry through Transform and Filter pipeline, then enqueues to batch buffer.
    /// This is the standard method for derived classes to submit collected telemetry data.
    /// The batch send task will periodically dequeue and send to cloud.
    /// </summary>
    /// <param name="measures">Raw telemetry measures to process and enqueue.</param>
    /// <param name="ct">Cancellation token.</param>
    protected async Task EnqueueTelemetryAsync(List<TelemetryMeasure> measures, CancellationToken ct = default)
    {
        if (measures == null || measures.Count == 0)
            return;

        var processResult = await _orchestrator.TelemetryPipeline.TransformAndFilterAsync(measures, ct);

        if (processResult.IsError)
        {
            _logger.LogWarning(
                "Transform/Filter failed: {Error}",
                string.Join(", ", processResult.Errors.Select(e => e.Description)));
            return;
        }

        var processedMeasures = processResult.Value;

        foreach (var processedMeasure in processedMeasures)
        {
            _telemetryBatch.Enqueue(processedMeasure);
        }

        // Raise DataProcessed event AFTER transform/filter processing
        // This allows subscribers (e.g., AggregatorCommunication) to receive processed data
        if (processedMeasures.Count > 0)
        {
            RaiseDataProcessed(processedMeasures);
        }

        _logger.LogTrace(
            "Enqueued {Count} processed measures for device {DeviceId}",
            processedMeasures.Count, DeviceId);
    }

    public async Task<DeviceHealth> GetHealthAsync(CancellationToken ct = default)
        => await _orchestrator.HealthMonitor.GetCurrentHealthAsync(ct);

    public async Task ReportHealthAsync(CancellationToken ct = default)
        => await _cloudService.ReportHealthAsync(DeviceId ?? "unknown", await GetHealthAsync(ct), ct);

    public async Task<string?> RegisterAsync(CancellationToken ct = default)
        => await _cloudService.GetOrRegisterDeviceIdAsync(DeviceInfo, ct);

    public async Task<DeviceConfiguration?> GetCurrentConfigurationAsync(CancellationToken ct = default)
        => await _cloudService.GetDeviceConfigurationAsync(DeviceId ?? "unknown", ct);

    /// <summary>
    /// Reports current device configuration to cloud.
    /// Used for periodic sync to ensure reported state is synchronized
    /// even if update response fails due to disconnection.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if report was successfully published</returns>
    public async Task<bool> ReportConfigurationAsync(CancellationToken ct = default)
    {
        var deviceId = DeviceId ?? "unknown";
        var deviceTypeName = Configuration.DeviceTypeName ?? Configuration.DeviceType.ToString();

        // Use default groupId for periodic reports (groupId is mainly for multi-tenant scenarios)
        var groupId = "default";

        var report = ConfigurationUpdateHelper.CreatePeriodicReport(
            deviceId,
            groupId,
            Configuration,
            deviceTypeName);

        _logger.LogDebug("Reporting configuration for device {DeviceId}", deviceId);
        return await _cloudService.PublishConfigurationReportAsync(report, ct);
    }

    public abstract Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken ct = default);

    // ===== Lifecycle Hooks =====

    protected virtual Task OnBeforeInitializeAsync(CancellationToken ct) => Task.CompletedTask;
    protected virtual Task OnAfterInitializeAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Internal lifecycle hook for starting device background tasks.
    /// This method is sealed and can only be overridden by intermediate framework base classes
    /// (RequestResponseDeviceBase, StreamingDeviceBase, PubSubDeviceBase).
    /// High-level custom devices should NOT override this method directly.
    /// Instead, inherit from one of the framework base classes.
    /// </summary>
    internal virtual Task StartBackgroundTasksAsync(CancellationToken ct) => Task.CompletedTask;

    // ===== Downlink Hooks =====

    /// <summary>
    /// Hook: Called before configuration update is applied.
    /// Use this to validate or prepare for configuration changes.
    /// </summary>
    protected virtual Task OnBeforeConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Gets the configuration update validation options.
    /// Override this property to customize which validations are enabled for this device.
    /// </summary>
    /// <example>
    /// <code>
    /// // Disable threshold validation for custom device
    /// protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Default with
    /// {
    ///     ValidateThresholds = false
    /// };
    /// </code>
    /// </example>
    protected virtual ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Default;

    /// <summary>
    /// Validates the configuration update message.
    /// Override this method to implement custom validation logic for your device.
    /// </summary>
    /// <param name="message">The configuration update message to validate</param>
    /// <returns>Validation result indicating success or failure with error message</returns>
    /// <example>
    /// <code>
    /// protected override ConfigurationValidationResult ValidateConfigurationUpdate(
    ///     SubNodeConfigurationUpdateMessage message)
    /// {
    ///     // Call base validation first
    ///     var baseResult = base.ValidateConfigurationUpdate(message);
    ///     if (!baseResult.IsValid)
    ///         return baseResult;
    ///
    ///     // Add custom validation
    ///     var desiredConfig = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs?.Values.FirstOrDefault();
    ///     if (desiredConfig?.Communication?.ContainsKey("CustomField") == false)
    ///         return ConfigurationValidationResult.Failure("CustomField is required");
    ///
    ///     return ConfigurationValidationResult.Success;
    /// }
    /// </code>
    /// </example>
    protected virtual ConfigurationValidationResult ValidateConfigurationUpdate(
        SubNodeConfigurationUpdateMessage message)
    {
        return ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, Configuration, ConfigUpdateOptions);
    }

    /// <summary>
    /// Applies base DeviceConfiguration updates from cloud and persists to cache.
    /// This handles standard configuration fields (sensors, periods, etc.) at the framework level.
    /// Implements proper validation, acknowledgment, update, and response workflow.
    /// Derived classes should use OnAfterConfigUpdateAsync for custom configuration handling.
    /// </summary>
    private async Task ApplyBaseConfigurationUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct)
    {
        // Use semaphore for thread-safe configuration updates
        await _configUpdateLock.WaitAsync(ct);

        try
        {
            // Get the strongly-typed message from the event
            var message = e.Message;
            if (message?.Data?.Cfg?.Desired == null)
            {
                _logger.LogDebug("Configuration update event does not contain valid desired configuration, skipping base update");
                return;
            }

            // Determine device type name for reporting (use DeviceTypeName or DeviceType.ToString())
            var deviceTypeName = Configuration.DeviceTypeName ?? Configuration.DeviceType.ToString();

            // Step 1: Validate the configuration update using virtual method
            _logger.LogInformation("Validating configuration update for device: {DeviceName}", Configuration.DeviceName);

            var validationResult = ValidateConfigurationUpdate(message);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Configuration update validation failed: {Error}", validationResult.ErrorMessage);

                // Send invalid status response
                var invalidReport = ConfigurationUpdateHelper.CreateInvalidReport(
                    message, Configuration, deviceTypeName, validationResult.ErrorMessage ?? "Unknown validation error");
                await _cloudService.PublishConfigurationReportAsync(invalidReport, ct);

                return;
            }

            _logger.LogInformation("Configuration update validation passed");

            // Step 2: Send "message received" acknowledgment (updating status)
            _logger.LogInformation("Sending 'message received' acknowledgment for device: {DeviceName}", Configuration.DeviceName);
            var updatingReport = ConfigurationUpdateHelper.CreateUpdatingReport(
                message, Configuration, deviceTypeName);
            await _cloudService.PublishConfigurationReportAsync(updatingReport, ct);

            // Step 3: Create backup before applying changes
            var backup = ConfigurationUpdateHelper.CreateBackup(Configuration);
            _logger.LogDebug("Configuration backup created");

            // Step 4: Apply configuration updates
            try
            {
                // Find the device config for this device
                var deviceConfigs = message.Data.Cfg.Desired.SubNodeDeviceConfig?.DeviceConfigs;
                if (deviceConfigs == null)
                {
                    _logger.LogDebug("No device configurations in desired state");
                    return;
                }

                // Find matching device config by DeviceName
                SubNodeDeviceConfigDto? desiredConfig = null;
                foreach (var (key, config) in deviceConfigs)
                {
                    if (string.Equals(config.DeviceName, Configuration.DeviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        desiredConfig = config;
                        break;
                    }
                }

                if (desiredConfig == null)
                {
                    _logger.LogDebug("No matching device configuration found for device: {DeviceName}", Configuration.DeviceName);
                    return;
                }

                _logger.LogInformation("Applying configuration update for device: {DeviceName}", Configuration.DeviceName);

                // Apply sensor configuration updates (PATCH semantics - only update provided fields)
                var updatedSensors = ConfigurationUpdateHelper.ApplySensorConfigUpdates(
                    Configuration, desiredConfig.Sensors);

                if (updatedSensors.Count > 0)
                {
                    _logger.LogInformation("Updated {Count} sensors: {SensorNames}",
                        updatedSensors.Count,
                        string.Join(", ", updatedSensors));
                }

                // Apply pipeline updates (Transform and DSP filters)
                var pipelineUpdateResult = ConfigurationUpdateHelper.ApplyAllPipelineUpdates(
                    Configuration, desiredConfig.Sensors);

                if (pipelineUpdateResult.IsError)
                {
                    var error = pipelineUpdateResult.FirstError;
                    _logger.LogError("Pipeline update validation failed: {Error}", error.Description);
                    throw new InvalidOperationException($"Pipeline update failed: {error.Description}");
                }

                var pipelineSummary = pipelineUpdateResult.Value;
                if (pipelineSummary.TotalDspSensorsUpdated > 0 || pipelineSummary.TotalTransformSensorsUpdated > 0)
                {
                    _logger.LogInformation(
                        "Updated pipelines - DSP: {DspCount} sensors, Transform: {TransformCount} sensors",
                        pipelineSummary.TotalDspSensorsUpdated,
                        pipelineSummary.TotalTransformSensorsUpdated);

                    // Log detailed results
                    foreach (var (sensorName, dspResult) in pipelineSummary.DspResults)
                    {
                        if (dspResult.TotalUpdated > 0)
                        {
                            _logger.LogDebug("Sensor '{Sensor}' DSP updates: {Updated} filters updated",
                                sensorName, dspResult.TotalUpdated);
                        }
                    }

                    foreach (var (sensorName, transformResult) in pipelineSummary.TransformResults)
                    {
                        if (transformResult.TotalUpdated > 0)
                        {
                            _logger.LogDebug("Sensor '{Sensor}' Transform updates: {Updated} transforms updated",
                                sensorName, transformResult.TotalUpdated);
                        }
                    }
                }

                // Apply background task periods if provided (PATCH semantics)
                if (desiredConfig.Periods != null)
                {
                    if (desiredConfig.Periods.ReportHealth > 0)
                    {
                        _logger.LogDebug("Updating ReportHealth period: {Old} -> {New}",
                            Configuration.Periods.ReportHealth, desiredConfig.Periods.ReportHealth);
                        Configuration.Periods.ReportHealth = desiredConfig.Periods.ReportHealth;
                    }
                    // ReportConfiguration can be 0 (disabled) or > 0 (enabled), so always update if provided
                    if (desiredConfig.Periods.ReportConfiguration >= 0)
                    {
                        _logger.LogDebug("Updating ReportConfiguration period: {Old} -> {New}",
                            Configuration.Periods.ReportConfiguration, desiredConfig.Periods.ReportConfiguration);
                        Configuration.Periods.ReportConfiguration = desiredConfig.Periods.ReportConfiguration;
                    }

                    _logger.LogInformation("Updated background task periods");
                }

                // Recalculate send telemetry period if sensor intervals changed
                CalculatedSendTelemetryPeriod = CalculateSendTelemetryPeriod(Configuration);
                _logger.LogDebug("Recalculated SendTelemetry period: {Period}ms", CalculatedSendTelemetryPeriod);

                // Step 5: Persist configuration to cache for restart persistence
                await _context.ConfigurationCache.SaveConfigurationAsync(Configuration, ct);
                var cacheFilePath = _context.ConfigurationCache.GetCacheFilePath(Configuration.DeviceName);
                _logger.LogInformation("Configuration cached to: {CachePath}", cacheFilePath);

                // Step 6: Send success response with updated configuration
                _logger.LogInformation("Configuration update successful, sending success response");
                var successReport = ConfigurationUpdateHelper.CreateSuccessReport(
                    message, Configuration, deviceTypeName);
                await _cloudService.PublishConfigurationReportAsync(successReport, ct);

                _logger.LogInformation("Configuration update completed successfully for device: {DeviceName}", Configuration.DeviceName);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Error applying configuration update, rolling back changes");

                // Step 7: Rollback on failure
                ConfigurationUpdateHelper.RestoreBackup(Configuration, backup);
                _logger.LogInformation("Configuration rolled back to previous state");

                // Send failure response
                var failureReport = ConfigurationUpdateHelper.CreateFailedReport(
                    message, Configuration, deviceTypeName, updateEx.Message);
                await _cloudService.PublishConfigurationReportAsync(failureReport, ct);

                throw; // Re-throw to let OnAfterConfigUpdateAsync know there was an error
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in configuration update workflow for device {DeviceId}", DeviceId);
            // Don't rethrow - allow OnAfterConfigUpdateAsync to still execute
        }
        finally
        {
            _configUpdateLock.Release();
        }
    }

    /// <summary>
    /// Hook: Called after configuration update is applied.
    /// Use this to handle custom/device-specific configuration changes.
    /// Base configuration (sensors, periods) is already applied and cached by the framework.
    /// </summary>
    protected virtual Task OnAfterConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Hook: Called before command execution.
    /// Use this for logging, validation, or preparation.
    /// </summary>
    protected virtual Task OnBeforeCommandAsync(ExecuteCommandEvent e, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Hook: Called after command execution.
    /// Use this for cleanup, logging, or follow-up actions.
    /// </summary>
    /// <param name="e">The command event</param>
    /// <param name="success">Whether the command executed successfully</param>
    /// <param name="ct">Cancellation token</param>
    protected virtual Task OnAfterCommandAsync(ExecuteCommandEvent e, bool success, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Triggers DataReceived event. Derived classes can call this to raise the event.
    /// Only fires if EnableDataReceivedTracking is true.
    /// This event fires with RAW data before transform/filter processing.
    /// </summary>
    protected void RaiseDataReceived(List<TelemetryMeasure> measures)
    {
        if (!EnableDataReceivedTracking) return;

        DataReceived?.Invoke(this, new DataReceivedEvent(
            DeviceId: DeviceId ?? "unknown",
            DeviceType: DeviceType,
            Data: measures,
            Timestamp: DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Triggers DataProcessed event. Called after transform/filter pipeline processing.
    /// Only fires if EnableDataProcessedTracking is true.
    /// This event fires with PROCESSED data after transform/filter processing.
    /// </summary>
    protected void RaiseDataProcessed(List<TelemetryMeasure> measures)
    {
        if (!EnableDataProcessedTracking) return;

        DataProcessed?.Invoke(this, new DataProcessedEvent(
            DeviceId: DeviceId ?? "unknown",
            DeviceType: DeviceType,
            Data: measures,
            Timestamp: DateTimeOffset.UtcNow));
    }

    // ===== Events & Tracking Flags =====

    public event EventHandler<DataReceivedEvent>? DataReceived;
    public event EventHandler<DataProcessedEvent>? DataProcessed;
    public event EventHandler<ConnectionStateChangedEvent>? ConnectionStateChanged;
    public event EventHandler<DeviceStatusChangedEvent>? DeviceStatusChanged;
    public event EventHandler<TelemetrySentEvent>? TelemetrySent;
    public event EventHandler<UpdateConfigurationEvent>? ConfigurationUpdateReceived;
    public event EventHandler<ExecuteCommandEvent>? CommandReceived;
    public event EventHandler<TelemetryValueChangedEvent>? ValueChanged;

    /// <inheritdoc />
    public bool EnableDataReceivedTracking { get; set; }

    /// <inheritdoc />
    public bool EnableDataProcessedTracking { get; set; }

    /// <inheritdoc />
    public bool EnableConnectionStateTracking { get; set; }

    /// <inheritdoc />
    public bool EnableDeviceStatusTracking { get; set; }

    /// <inheritdoc />
    public bool EnableTelemetrySentTracking { get; set; }

    /// <inheritdoc />
    public bool EnableConfigurationUpdateTracking { get; set; }

    /// <inheritdoc />
    public bool EnableCommandReceivedTracking { get; set; }

    /// <inheritdoc />
    public bool EnableValueChangeTracking
    {
        get => _orchestrator.TelemetryPipeline.EnableValueChangeTracking;
        set => _orchestrator.TelemetryPipeline.EnableValueChangeTracking = value;
    }

    private void WireEvents()
    {
        // Connection state changes
        _communication.StateChanged += (s, e) =>
        {
            if (EnableConnectionStateTracking)
                ConnectionStateChanged?.Invoke(this, e);
        };

        // Device status changes
        _orchestrator.StatusChanged += (s, e) =>
        {
            if (EnableDeviceStatusTracking)
                DeviceStatusChanged?.Invoke(this, new(DeviceId ?? "unknown", DeviceType, e.FromStatus, e.ToStatus, e.Timestamp));
        };

        // Forward pipeline value change events to device level
        _orchestrator.TelemetryPipeline.ValueChanged += (s, e) => ValueChanged?.Invoke(this, e);

        // Configuration Update: Auto-invoke Pre/Post hooks
        _orchestrator.ConnectionManager.ConfigurationUpdateReceived += async e =>
        {
            try
            {
                // Pre-hook (for derived class validation/preparation)
                await OnBeforeConfigUpdateAsync(e, CancellationToken.None);

                // Raise event (for framework monitoring/logging)
                if (EnableConfigurationUpdateTracking)
                    ConfigurationUpdateReceived?.Invoke(this, e);

                // Apply base DeviceConfiguration updates from cloud
                await ApplyBaseConfigurationUpdateAsync(e, CancellationToken.None);

                // Post-hook (for derived class custom configuration handling)
                await OnAfterConfigUpdateAsync(e, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling configuration update for device {DeviceId}", DeviceId);
            }
        };

        // Command: Auto-invoke Pre -> Execute -> Post hooks with Command Response
        _orchestrator.ConnectionManager.CommandReceived += async e =>
        {
            var success = false;
            string? errorCode = null;
            string? errorMessage = null;

            try
            {
                // Pre-hook (can be used for validation)
                await OnBeforeCommandAsync(e, CancellationToken.None);

                // Raise event (for framework monitoring/logging)
                if (EnableCommandReceivedTracking)
                    CommandReceived?.Invoke(this, e);

                // Send "received" response immediately after validation passes
                await SendCommandResponseAsync(
                    e.Command.RespTopic,
                    CommandResponse.Received(DeviceId!, e.Command.DeviceCmd));

                // Execute command on device
                _logger.LogInformation("Executing command: {CommandName}", e.Command.DeviceCmd);
                success = await ExecuteCommandAsync(e.Command);
                _logger.LogInformation("Command execution {Result}: {CommandName}",
                    success ? "succeeded" : "failed",
                    e.Command.DeviceCmd);

                if (!success)
                {
                    errorCode = "Command.ExecutionFailed";
                    errorMessage = $"Command '{e.Command.DeviceCmd}' execution returned false";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing command: {CommandName}", e.Command.DeviceCmd);
                errorCode = "Command.Exception";
                errorMessage = ex.Message;
            }
            finally
            {
                // Send final response (success or failed)
                try
                {
                    if (success)
                    {
                        await SendCommandResponseAsync(
                            e.Command.RespTopic,
                            CommandResponse.Success(DeviceId!, e.Command.DeviceCmd));
                    }
                    else
                    {
                        await SendCommandResponseAsync(
                            e.Command.RespTopic,
                            CommandResponse.Failed(DeviceId!, e.Command.DeviceCmd,
                                errorCode ?? "Command.Unknown",
                                errorMessage ?? "Unknown error"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending command response for {CommandName}", e.Command.DeviceCmd);
                }

                // Post-hook (always called, even on failure)
                try
                {
                    await OnAfterCommandAsync(e, success, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OnAfterCommandAsync hook for command {CommandName}", e.Command.DeviceCmd);
                }
            }
        };
    }

    /// <summary>
    /// Send command response to cloud (if RespTopic is provided)
    /// </summary>
    private async Task SendCommandResponseAsync(string? responseTopic, CommandResponse response)
    {
        if (string.IsNullOrEmpty(responseTopic))
        {
            _logger.LogDebug("No response topic provided, skipping command response");
            return;
        }

        try
        {
            await _cloudService.SendCommandResponseAsync(
                responseTopic, response, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command response: {Status}", response.Status);
        }
    }

    // ===== Sensor Access =====

    /// <inheritdoc />
    public Sensor GetSensor(string sensorName)
    {
        return FindSensor(sensorName)
            ?? throw new KeyNotFoundException($"Sensor '{sensorName}' not found in device '{DeviceName}'");
    }

    /// <inheritdoc />
    public Sensor? FindSensor(string sensorName)
    {
        if (string.IsNullOrWhiteSpace(sensorName))
            return null;

        return Configuration.Sensors.FirstOrDefault(s =>
            string.Equals(s.Name, sensorName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public Sensor GetSensorByResourceId(string resourceId)
    {
        return FindSensorByResourceId(resourceId)
            ?? throw new KeyNotFoundException($"Sensor with ResourceId '{resourceId}' not found in device '{DeviceName}'");
    }

    /// <inheritdoc />
    public Sensor? FindSensorByResourceId(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            return null;

        return Configuration.Sensors.FirstOrDefault(s =>
            string.Equals(s.ResourceId, resourceId, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        // Unregister device from context's device registry
        _context.DeviceRegistry.Unregister(this);

        _orchestrator.Dispose();
        _configUpdateLock.Dispose();
        GC.SuppressFinalize(this);
    }

    // ===== Sensor Interval Validation & Calculation =====

    /// <summary>
    /// Validates that all enabled sensors have a valid Interval configured (> 0).
    /// Throws InvalidOperationException if any enabled sensor has Interval <= 0.
    /// </summary>
    /// <param name="configuration">Device configuration to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when any enabled sensor has invalid Interval</exception>
    private static void ValidateSensorIntervals(DeviceConfiguration configuration)
    {
        var invalidSensors = configuration.Sensors
            .Where(s => s.Config.Enabled && s.Config.Interval <= 0)
            .Select(s => s.Name)
            .ToList();

        if (invalidSensors.Count > 0)
        {
            throw new InvalidOperationException(
                $"All enabled sensors must have Config.Interval > 0. " +
                $"Invalid sensors: [{string.Join(", ", invalidSensors)}]. " +
                $"Please configure the Interval property for each sensor in appsettings.json.");
        }
    }

    /// <summary>
    /// Calculates the telemetry batch send period as the minimum of all enabled sensor intervals.
    /// </summary>
    /// <param name="configuration">Device configuration</param>
    /// <returns>Minimum interval in milliseconds</returns>
    private static int CalculateSendTelemetryPeriod(DeviceConfiguration configuration)
    {
        var enabledIntervals = configuration.Sensors
            .Where(s => s.Config.Enabled && s.Config.Interval > 0)
            .Select(s => (int)s.Config.Interval)
            .ToList();

        // This should never happen after validation, but provide safe default
        if (enabledIntervals.Count == 0)
        {
            return 5000; // Default fallback
        }

        return enabledIntervals.Min();
    }
}
