using System.Collections.Concurrent;
using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Configuration;
using Weda.SubNode.Core.Devices.Lifecycle;

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
    private readonly IProtocolParserCore? _protocolParser;

    private CancellationTokenSource? _runningCts;
    private CancellationTokenSource? _samplingCts;
    private Task? _batchSendTask;
    private Task? _healthTask;
    private readonly SemaphoreSlim _configUpdateLock = new(1, 1);

    /// <summary>
    /// Batch queue for telemetry measures that have been processed through Transform and Filter.
    /// Data is enqueued by derived classes via EnqueueTelemetryAsync and sent by the batch send task.
    /// </summary>
    protected readonly ConcurrentQueue<TelemetryMeasure> _telemetryBatch = new();
    protected readonly ConcurrentDictionary<string, List<TelemetryMeasure>> _lastTelemetryValues = new();

    public DeviceConfiguration Configuration { get; }
    public string SubNodeId => _orchestrator.SubNodeId;
    public string DeviceName => Configuration.DeviceInfo.DeviceName;
    public SubNodeType SubNodeType => Configuration.DeviceInfo.SubNodeType;
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
        IProtocolParserCore protocolParser)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _protocolParser = protocolParser ?? throw new ArgumentNullException(nameof(protocolParser));
        _communication = protocolParser.Communication ?? throw new ArgumentNullException(nameof(protocolParser.Communication));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = context.GetLogger<DeviceBase>();
        _cloudService = context.CloudService;

        // Auto-subscribe to TelemetryRecording event for local storage
        if (_context.RecordingService != null)
        {
            TelemetryRecording += OnTelemetryRecording;
        }

        // Auto-enrich: Attach SubNodeInfo from context if not already set
        // This enables AutoGenEnabled and provides Manufacturer/Model/SwVersion
        Configuration.SubNodeInfo ??= context.SubNodeInfo;

        // Validate configuration and calculate send period
        ValidateConfiguration(configuration);
        CalculatedSendTelemetryPeriod = CalculateSendTelemetryPeriod(configuration);

        // Initialize DTDL (auto-generates when AutoGenEnabled=true, or loads from file)
        Configuration.InitializeDtdl(basePath: null, _logger);

        // Single orchestrator manages all complexity
        _orchestrator = new DeviceOrchestrator(
            context,
            _communication,
            this, // ILifecycleHooks
            configuration, // Pass full configuration for sensor-level transform/filter support
            configuration.DeviceId); // Pass deviceId from configuration

        // Single initializer handles device configuration enrichment
        _initializer = new DeviceInitializer(
            context.SubNodeInfo,
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
        if (!_context.SubNodeManager.IsInitialized)
        {
            _logger.LogInformation("SubNodeManager not initialized, initializing now for standalone device testing.");
            var initialized = await _context.SubNodeManager.InitializeAsync(ct);
            if (!initialized)
            {
                _logger.LogError("Failed to initialize SubNodeMnager");
                return false;
            }
        }
        if (Status != DeviceStatus.Ready && !await InitializeAsync(ct))
            return false;
        return !(await _orchestrator.LifecycleManager.StartAsync(ct)).IsError;
    }

    public async Task<bool> StopAsync(CancellationToken ct = default)
        => !(await _orchestrator.LifecycleManager.StopAsync(ct)).IsError;

    // ===== ILifecycleHooks =====

    async Task<ErrorOr<Success>> ILifecycleHooks.OnInitializeAsync(CancellationToken ct)
    {
        // Step 1: Establish physical device connection only
        // Cloud connection is already handled by SubNodeManager at the SubNode level
        var connResult = await _orchestrator.ConnectionManager.EstablishPhysicalConnectionAsync(ct);
        if (connResult.IsError) return connResult.Errors;

        await OnBeforeInitializeAsync(ct);

        // Step 2: Verify SubNodeManager is initialized
        // SubNode is the aggregation root - the device initialization must be triggered by SubNode's initialization
        if (!_context.SubNodeManager.IsInitialized)
        {
            return Error.Failure("Device.InitializeFailed",
                "Failed to initialize Device. Suggest use SubNode.InitializeAsync() instead.");
        }

        // Step 3: Get SubNodeId from SubNodeManager
        var subNodeId = _context.SubNodeManager.SubNodeId
            ?? throw new InvalidOperationException("SubNodeManager initialized but SubNodeId is null. This should not happen.");

        // Step 4: Set the SubNode ID on orchestrator
        _orchestrator.SetSubNodeId(subNodeId);

        // Step 5: Enrich device configuration with SubNodeId and ResourceIds
        _initializer.EnrichConfiguration(Configuration, subNodeId);

        // Step 6: Register this device's event handlers with SubNodeManager for Hybrid routing
        _context.SubNodeManager.RegisterDeviceHandler(
            Configuration.DeviceName,
            HandleConfigurationUpdateAsync);

        await OnAfterInitializeAsync(ct);
        return Result.Success;
    }

    /// <summary>
    /// Handles configuration update events routed from SubNodeManager.
    /// Returns ConfigUpdateResult for SubNodeManager to publish report.
    /// </summary>
    private async Task<ConfigUpdateResult> HandleConfigurationUpdateAsync(UpdateConfigurationEvent e)
    {
        var deviceTypeName = Configuration.SubNodeType.ToString();

        try
        {
            // Pre-hook (for derived class validation/preparation)
            await OnBeforeConfigUpdateAsync(e, CancellationToken.None);

            // Raise event (for framework monitoring/logging)
            if (EnableConfigurationUpdateTracking)
                ConfigurationUpdateReceived?.Invoke(this, e);

            // Apply base DeviceConfiguration updates from cloud
            var result = await ApplyBaseConfigurationUpdateAsync(e, CancellationToken.None);

            // Post-hook (for derived class custom configuration handling)
            await OnAfterConfigUpdateAsync(e, CancellationToken.None);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling configuration update for device {DeviceName}", Configuration.DeviceName);
            return ConfigUpdateResult.Failed(Configuration, deviceTypeName, ex.Message);
        }
    }

    async Task<ErrorOr<Success>> ILifecycleHooks.OnStartAsync(CancellationToken ct)
    {
        // _runningCts is for lifecycle-level cancellation
        _runningCts = new CancellationTokenSource();

        // _samplingCts is for tasks that may need restart on config update (polling, sampling, batch send, health)
        _samplingCts = new CancellationTokenSource();

        // Start background tasks (polling/sampling + batch send + health)
        StartAllBackgroundTasks(_samplingCts.Token);

        return await Task.FromResult(Result.Success);
    }

    async Task<ErrorOr<Success>> ILifecycleHooks.OnStopAsync(CancellationToken ct)
    {
        // Stop all background tasks (polling/sampling + batch send + health) and await completion
        await StopBackgroundTasksAsync();

        // Cancel lifecycle-level tasks
        if (_runningCts != null)
        {
            await _runningCts.CancelAsync();
            _runningCts.Dispose();
            _runningCts = null;
        }

        await _orchestrator.ConnectionManager.DisconnectAsync(ct);
        return Result.Success;
    }

    async Task<ErrorOr<Success>> ILifecycleHooks.OnDisposeAsync(CancellationToken ct)
    {
        if (_communication is IDisposable disp) disp.Dispose();
        _samplingCts?.Dispose();
        _runningCts?.Dispose();
        return await Task.FromResult(Result.Success);
    }

    // ===== IDevice Operations =====

    public virtual Task<List<TelemetryMeasure>> ReadSensorTelemetryAsync(string sensorResourceId, CancellationToken ct = default)
    {
        _lastTelemetryValues.TryGetValue(sensorResourceId, out var measure);
        return Task.FromResult(measure ?? []);    
    }

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

            var success = !result.IsError;

            // Raise TelemetrySent event if tracking is enabled
            if (EnableTelemetrySentTracking)
            {
                RaiseTelemetrySentEvent(measures.Count, success, null);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send telemetry for device {SubNodeId} after all retries", SubNodeId);

            // Raise TelemetrySent event with error if tracking is enabled
            if (EnableTelemetrySentTracking)
            {
                RaiseTelemetrySentEvent(measures.Count, false, ex.Message);
            }

            return false;
        }
    }

    private void RaiseTelemetrySentEvent(int measureCount, bool success, string? error)
    {
        TelemetrySent?.Invoke(this, new TelemetrySentEvent(
            DeviceId: SubNodeId ?? "unknown",
            MeasureCount: measureCount,
            Success: success,
            Timestamp: DateTimeOffset.UtcNow)
        { Error = error });
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

        // Handle recording for each measure (independent of send mode)
        foreach (var processedMeasure in processedMeasures)
        {
            var sensor = Configuration.GetSensorById(processedMeasure.ResourceId);
            if (sensor?.Record.Enabled == true)
            {
                var interval = sensor.Record.Interval > 0
                    ? sensor.Record.Interval
                    : (int)sensor.Report.Interval;

                // Check if sensor uses MIME schema (JSON, images, etc.)
                var schemaType = SchemaTypeExtensions.ParseMimeSchema(sensor.Schema);
                if (schemaType != null && schemaType.Value.IsMimeType())
                {
                    // Use DynamicRecordStorage for MIME types
                    var dynamicStorage = _context.DynamicRecordStorage;
                    if (dynamicStorage != null)
                    {
                        // Convert value to byte[] - support byte[], string, and object (auto-serialize)
                        byte[]? payload = processedMeasure.Value switch
                        {
                            null => null,
                            byte[] bytes => bytes,
                            string str => System.Text.Encoding.UTF8.GetBytes(str),
                            // Fallback: auto-serialize objects to JSON for application/json schema
                            _ when schemaType.Value == SchemaType.ApplicationJson =>
                                JsonSerializer.SerializeToUtf8Bytes(processedMeasure.Value),
                            _ => null
                        };

                        if (payload != null)
                        {
                            _ = dynamicStorage.AppendAsync(
                                sensor.ShortId,
                                processedMeasure.Timestamp,
                                payload,
                                schemaType.Value,
                                CancellationToken.None);
                        }
                    }
                }
                else if (processedMeasure.Value is IConvertible)
                {
                    // Use RecordingService for primitive types
                    // Wrap in try-catch to prevent single sensor parsing errors from affecting the entire batch
                    try
                    {
                        var doubleValue = Convert.ToDouble(processedMeasure.Value);
                        RaiseTelemetryRecording(new TelemetryRecordingEvent(
                            Sensor: sensor,
                            Interval: interval,
                            Timestamp: processedMeasure.Timestamp,
                            Value: doubleValue));
                    }
                    catch (FormatException ex)
                    {
                        _logger.LogWarning(
                            "Failed to convert value '{Value}' to double for sensor '{SensorName}' (ResourceId: {ResourceId}): {Error}",
                            processedMeasure.Value, sensor.Name, sensor.ResourceId, ex.Message);
                    }
                }
            }
        }

        foreach (var group in processedMeasures.GroupBy(m => m.ResourceId))
        {
            _lastTelemetryValues[group.Key] = group.ToList();
        }

        // Raise DataProcessed event AFTER transform/filter processing
        // This allows subscribers (e.g., AggregatorCommunication) to receive processed data
        if (processedMeasures.Count > 0)
        {
            RaiseDataProcessed(processedMeasures);
        }

        // Send telemetry: immediate or batched based on configuration
        if (!Configuration.Periods.BatchSend)
        {
            // Immediate mode: send directly without batching
            _logger.LogTrace(
                "Sending {Count} measures immediately for device {SubNodeId}",
                processedMeasures.Count, SubNodeId);
            await SendTelemetryAsync(processedMeasures, ct);
        }
        else
        {
            // Batch mode: enqueue for periodic batch send
            foreach (var measure in processedMeasures)
            {
                _telemetryBatch.Enqueue(measure);
            }
            _logger.LogTrace(
                "Enqueued {Count} processed measures for device {SubNodeId}",
                processedMeasures.Count, SubNodeId);
        }
    }

    public async Task<DeviceHealth> GetHealthAsync(CancellationToken ct = default)
        => await _orchestrator.HealthMonitor.GetCurrentHealthAsync(ct);

    public async Task ReportHealthAsync(CancellationToken ct = default)
        => await _cloudService.ReportHealthAsync(SubNodeId ?? "unknown", await GetHealthAsync(ct), ct);

    public async Task<string?> RegisterAsync(CancellationToken ct = default)
        => await _cloudService.GetOrRegisterDeviceIdAsync(DeviceInfo, ct);

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

    /// <summary>
    /// Internal lifecycle hook for stopping device-specific background tasks.
    /// Derived classes (RequestResponseDeviceBase, StreamingDeviceBase, PubSubDeviceBase)
    /// should override this to await their own polling/streaming/subscription tasks.
    /// Called by StopBackgroundTasksAsync before configuration changes are applied.
    /// </summary>
    internal virtual Task StopDeviceTasksAsync() => Task.CompletedTask;

    // ===== Interval Group Processing (Template Method Pattern) =====

    /// <summary>
    /// Result of reading sensors for an interval group.
    /// Contains the telemetry measures and the duration of the read operation.
    /// </summary>
    /// <param name="Measures">The telemetry measures read from sensors</param>
    /// <param name="Duration">The duration of the read operation (TimeSpan.Zero for cache-based reads)</param>
    protected readonly record struct IntervalGroupReadResult(
        List<TelemetryMeasure> Measures,
        TimeSpan Duration);

    /// <summary>
    /// Template method for processing an interval group.
    /// Handles the common flow: read sensors → record health → raise events → enqueue telemetry.
    /// Each device type only needs to implement ReadSensorsForIntervalGroupAsync for data collection.
    /// </summary>
    /// <param name="sensors">Sensors in this interval group</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected async Task ProcessIntervalGroupAsync(List<Sensor> sensors, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            var sensorResourceIds = sensors.Select(s => s.ResourceId).ToList();

            // Step 1: Read sensors (abstract - each device type implements its own data collection)
            var result = await ReadSensorsForIntervalGroupAsync(sensorResourceIds, cancellationToken);

            if (result.Measures.Count == 0)
                return;

            // Step 2: Record health metrics
            _orchestrator.HealthMonitor.RecordTelemetryReadDuration(result.Duration);

            // Step 3: Raise raw data event (before transform/filter)
            RaiseDataReceived(result.Measures);

            // Step 4: Transform, Filter, and Enqueue (handled by DeviceBase)
            // RaiseDataProcessed fires in EnqueueTelemetryAsync after transform/filter
            await EnqueueTelemetryAsync(result.Measures, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing interval group with {SensorCount} sensors", sensors.Count);
        }
    }

    /// <summary>
    /// Reads sensors for an interval group. Each device type implements its own data collection logic.
    /// - StreamingDeviceBase/PubSubDeviceBase: Reads from SensorCache (Duration = TimeSpan.Zero)
    /// - RequestResponseDeviceBase: Reads from parser with timing (Duration = actual read time)
    /// </summary>
    /// <param name="sensorResourceIds">ResourceIds of sensors to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>IntervalGroupReadResult containing measures and read duration</returns>
    protected abstract Task<IntervalGroupReadResult> ReadSensorsForIntervalGroupAsync(
        List<string> sensorResourceIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Groups enabled sensors by their configured interval.
    /// Used by all device types to create interval-based polling/sampling tasks.
    /// </summary>
    /// <returns>List of (interval in ms, sensors in that group)</returns>
    protected List<(int IntervalMs, List<Sensor> Sensors)> GroupSensorsByInterval()
    {
        return Configuration.Sensors
            .Where(s => s.IsEffectivelyEnabled)
            .GroupBy(s => (int)s.Report.Interval)
            .Select(g => (IntervalMs: g.Key, Sensors: g.ToList()))
            .ToList();
    }

    /// <summary>
    /// Runs a polling/sampling loop for an interval group using PeriodicTimer.
    /// </summary>
    /// <param name="sensors">Sensors in this interval group</param>
    /// <param name="intervalMs">Polling/sampling interval in milliseconds</param>
    /// <param name="initialDelay">Whether to wait for initial interval before first iteration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected async Task RunIntervalLoopAsync(
        List<Sensor> sensors,
        int intervalMs,
        bool initialDelay,
        CancellationToken cancellationToken)
    {
        // Optional initial delay (for cache-based devices to allow data to arrive)
        if (initialDelay)
        {
            try
            {
                await Task.Delay(intervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        // Use PeriodicTimer to avoid drift and prevent task accumulation
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));

        try
        {
            // Execute first iteration immediately (for non-initialDelay case, this is the first read)
            // For initialDelay case, we already waited above
            if (!initialDelay)
            {
                await ProcessIntervalGroupAsync(sensors, cancellationToken);
            }

            // Then wait for subsequent ticks
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await ProcessIntervalGroupAsync(sensors, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Interval loop cancelled for group with {SensorCount} sensors", sensors.Count);
        }
    }

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
    ///     SubNodeConfigUpdateMessage message)
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
        SubNodeConfigUpdateMessage message)
    {
        return ConfigurationUpdateHelper.ValidateDeviceConfiguration(message, Configuration, ConfigUpdateOptions);
    }

    /// <summary>
    /// Applies configuration updates from cloud.
    /// NOTE: SubNodeManager (Aggregate Root) now handles SystemConfig and CustomConfig directly.
    /// DeviceBase only handles DeviceConfig updates dispatched from SubNodeManager.
    /// Returns ConfigUpdateResult for SubNodeManager to publish report.
    /// </summary>
    private async Task<ConfigUpdateResult> ApplyBaseConfigurationUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct)
    {
        var deviceTypeName = Configuration.SubNodeType.ToString();

        // Use semaphore for thread-safe configuration updates
        await _configUpdateLock.WaitAsync(ct);

        try
        {
            // Get the strongly-typed message from the event
            var message = e.Message;
            if (message?.Data?.Cfg?.Desired == null)
            {
                _logger.LogDebug("Configuration update event does not contain valid desired configuration, skipping update");
                return ConfigUpdateResult.NoUpdateRequired(Configuration, deviceTypeName);
            }

            // SubNodeManager is the Aggregate Root - it routes only DeviceConfig to devices
            // SystemConfig and CustomConfig are handled by SubNodeManager directly
            if (e.ConfigType == SubscriptionTypes.DeviceConfig)
            {
                return await ApplyDeviceConfigurationUpdateAsync(e, message, ct);
            }
            else
            {
                // This should not happen - SubNodeManager should not route non-DeviceConfig to devices
                _logger.LogWarning(
                    "Received unexpected ConfigType: {ConfigType}. SubNodeManager should handle this.",
                    e.ConfigType.Value);
                return ConfigUpdateResult.NoUpdateRequired(Configuration, deviceTypeName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in configuration update workflow for device {SubNodeId}, type {ConfigType}",
                SubNodeId, e.ConfigType.Value);
            // Don't rethrow - allow OnAfterConfigUpdateAsync to still execute
            return ConfigUpdateResult.Failed(Configuration, deviceTypeName, ex.Message);
        }
        finally
        {
            _configUpdateLock.Release();
        }
    }

    /// <summary>
    /// Applies device-config updates (sensors, periods, etc.) from cloud.
    /// This handles standard DeviceConfiguration fields at the framework level.
    /// Returns ConfigUpdateResult for SubNodeManager to publish report.
    /// </summary>
    private async Task<ConfigUpdateResult> ApplyDeviceConfigurationUpdateAsync(
        UpdateConfigurationEvent e,
        SubNodeConfigUpdateMessage message,
        CancellationToken ct)
    {
        var hasDtmiDelta = false;
        var deviceTypeName = Configuration.SubNodeType.ToString();

        // Step 1: Validate the configuration update using virtual method
        _logger.LogInformation("Validating configuration update for device: {DeviceName}", Configuration.DeviceName);

        var validationResult = ValidateConfigurationUpdate(message);
        if (!validationResult.IsValid)
        {
            _logger.LogError("Configuration update validation failed: {Error}", validationResult.ErrorMessage);
            return ConfigUpdateResult.Invalid(Configuration, deviceTypeName, validationResult.ErrorMessage ?? "Unknown validation error");
        }

        // Check if no update is required (empty desired config)
        if (validationResult.NoUpdateRequired)
        {
            _logger.LogDebug("No configuration update required - desired config is empty");
            return ConfigUpdateResult.NoUpdateRequired(Configuration, deviceTypeName);
        }

        _logger.LogInformation("Configuration update validation passed");

        // Step 2: Create backup before applying changes
        var backup = ConfigurationUpdateHelper.CreateBackup(Configuration);
        _logger.LogDebug("Configuration backup created");

        // Step 3: Find the device config for this device
        var deviceConfigs = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs;
        if (deviceConfigs == null)
        {
            _logger.LogDebug("No device configurations in desired state");
            return ConfigUpdateResult.NoUpdateRequired(Configuration, deviceTypeName);
        }

        // Find matching device config by DeviceName (using dictionary key)
        if (!deviceConfigs.TryGetValue(Configuration.DeviceName, out var desiredConfig))
        {
            _logger.LogDebug("No matching device configuration found for device: {DeviceName}", Configuration.DeviceName);
            return ConfigUpdateResult.NoUpdateRequired(Configuration, deviceTypeName);
        }

        _logger.LogInformation("Applying configuration update for device: {DeviceName}", Configuration.DeviceName);

        // Apply device-level Enabled flag and propagate to all sensors
        Configuration.Enabled = desiredConfig.Enabled;

        // Always propagate device-level Enabled flag to all sensors
        foreach (var sensor in Configuration.Sensors)
        {
            sensor.DeviceEnabled = desiredConfig.Enabled;
        }

        // Record pre-update state for detecting interval/period changes
        var previousIntervalGroups = Configuration.Sensors
            .Where(s => s.IsEffectivelyEnabled)
            .GroupBy(s => (int)s.Report.Interval)
            .ToDictionary(g => g.Key, g => g.Select(s => s.ResourceId).ToHashSet());
        var previousHealthPeriod = Configuration.Periods.ReportHealth;

        // Step 4: Stop background tasks BEFORE modifying configuration to prevent race conditions.
        // Background tasks read Configuration concurrently; modifying it while tasks are
        // running can cause CollectionModified exceptions or inconsistent state.
        await StopBackgroundTasksAsync();

        try
        {
            // Apply sensor configuration updates (PATCH semantics - only update provided fields)
            var updatedSensors = ConfigurationUpdateHelper.ApplysensorReportUpdates(
                Configuration, desiredConfig.Sensors);

            if (updatedSensors.Count > 0)
            {
                _logger.LogInformation("Updated {Count} sensors: {SensorNames}",
                    updatedSensors.Count,
                    string.Join(", ", updatedSensors));

                // Flush recording buffers for updated sensors to avoid mixing data from different intervals
                if (_context.RecordingService != null)
                {
                    foreach (var sensorName in updatedSensors)
                    {
                        var sensor = Configuration.Sensors.FirstOrDefault(s => s.Name == sensorName);
                        if (sensor != null)
                        {
                            await _context.RecordingService.FlushSensorAsync(sensor.ShortId, ct);
                        }
                    }
                }
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

            // Check for DTMI delta - SubNodeManager will trigger re-upload if needed
            if (ConfigurationUpdateHelper.HasDtmiDelta(Configuration, desiredConfig.Sensors))
            {
                hasDtmiDelta = true;

                var changes = ConfigurationUpdateHelper.GetDtmiChanges(Configuration, desiredConfig.Sensors);
                _logger.LogInformation("DTMI delta detected: {Count} changes", changes.Count);

                foreach (var (sensorName, oldDtmi, newDtmi) in changes)
                {
                    _logger.LogDebug("  Sensor '{Sensor}': '{OldDtmi}' -> '{NewDtmi}'", sensorName, oldDtmi, newDtmi);
                }

                // Add new sensors
                var addedSensors = ConfigurationUpdateHelper.ApplyNewSensors(Configuration, desiredConfig.Sensors!, Configuration.DeviceId!);
                if (addedSensors.Count > 0)
                {
                    _logger.LogInformation("Added {Count} new sensors: {SensorNames}",
                        addedSensors.Count, string.Join(", ", addedSensors));
                }

                // Re-initialize DTDL (auto-generate when AutoGenEnabled=true, or loads from file)
                Configuration.InitializeDtdl(null, logger: _logger);
                _logger.LogDebug("DTDL re-initialized");
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

            // Refresh parser metadata and restart background tasks with updated configuration
            _protocolParser?.RefreshSensorMetadata();
            _samplingCts = new CancellationTokenSource();
            StartAllBackgroundTasks(_samplingCts.Token);

            // Step 5: Persist raw cloud message to cache for restart persistence
            // By storing the raw message, we preserve original JSON structure and data types
            await _context.ConfigurationCache.SaveRawConfigurationAsync(e.ConfigType, message, ct);
            _logger.LogInformation("Configuration cached to: {CachePath}",
                _context.ConfigurationCache.GetCacheFilePath(e.ConfigType));

            _logger.LogInformation("Configuration update completed successfully for device: {DeviceName}", Configuration.DeviceName);

            return ConfigUpdateResult.Success(Configuration, deviceTypeName, hasDtmiDelta);
        }
        catch (Exception updateEx)
        {
            _logger.LogError(updateEx, "Error applying device configuration update, rolling back changes");

            // Rollback on failure
            ConfigurationUpdateHelper.RestoreBackup(Configuration, backup);
            CalculatedSendTelemetryPeriod = CalculateSendTelemetryPeriod(Configuration);
            _logger.LogInformation("Configuration rolled back to previous state");

            // Restart background tasks with rolled-back configuration to resume operation
            _protocolParser?.RefreshSensorMetadata();
            _samplingCts = new CancellationTokenSource();
            StartAllBackgroundTasks(_samplingCts.Token);

            return ConfigUpdateResult.Failed(Configuration, deviceTypeName, updateEx.Message);
        }
    }


    // NOTE: ApplySystemConfigurationUpdateAsync and ApplyCustomConfigurationUpdateAsync
    // have been removed. SubNodeManager (Aggregate Root) now handles SystemConfig and
    // CustomConfig updates directly. Only DeviceConfig is dispatched to devices.

    // ===== Two-Phase Configuration Update (for Transaction Semantics) =====

    /// <summary>
    /// Phase 1: Validates configuration update without modifying state.
    /// Called by SubNodeManager to validate all devices before applying any.
    /// </summary>
    /// <param name="message">The configuration update message</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result with status and optional error message</returns>
    public virtual async Task<ConfigUpdateValidationResult> ValidateConfigurationUpdateAsync(
        SubNodeConfigUpdateMessage message,
        CancellationToken ct)
    {
        var deviceTypeName = Configuration.SubNodeType.ToString();

        await _configUpdateLock.WaitAsync(ct);
        try
        {
            // Pre-hook for derived class validation
            await OnBeforeConfigUpdateAsync(
                new UpdateConfigurationEvent(
                    SubNodeId ?? "unknown",
                    SubscriptionTypes.DeviceConfig,
                    message,
                    DateTimeOffset.UtcNow), ct);

            // Check for valid message structure
            if (message?.Data?.Cfg?.Desired == null)
            {
                return ConfigUpdateValidationResult.Skipped(deviceTypeName);
            }

            // Find device config for this device
            var deviceConfigs = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs;
            if (deviceConfigs == null || !deviceConfigs.TryGetValue(Configuration.DeviceName, out var desiredConfig))
            {
                return ConfigUpdateValidationResult.Skipped(deviceTypeName);
            }

            // Validate using the existing validation method
            var validationResult = ValidateConfigurationUpdate(message);
            if (!validationResult.IsValid)
            {
                return ConfigUpdateValidationResult.Invalid(deviceTypeName, validationResult.ErrorMessage ?? "Validation failed");
            }

            if (validationResult.NoUpdateRequired)
            {
                return ConfigUpdateValidationResult.Skipped(deviceTypeName);
            }

            // Check for DTMI delta
            var hasDtmiDelta = ConfigurationUpdateHelper.HasDtmiDelta(Configuration, desiredConfig.Sensors);

            return ConfigUpdateValidationResult.Valid(deviceTypeName, hasDtmiDelta);
        }
        finally
        {
            _configUpdateLock.Release();
        }
    }

    /// <summary>
    /// Phase 2: Applies a validated configuration update.
    /// Only called after all devices pass validation.
    /// </summary>
    /// <param name="message">The configuration update message</param>
    /// <param name="backup">The backup created before apply phase</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of applying the configuration</returns>
    public virtual async Task<ConfigUpdateResult> ApplyValidatedConfigurationAsync(
        SubNodeConfigUpdateMessage message,
        DeviceConfigurationBackup backup,
        CancellationToken ct)
    {
        var deviceTypeName = Configuration.SubNodeType.ToString();

        await _configUpdateLock.WaitAsync(ct);
        try
        {
            var deviceConfigs = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs;
            if (deviceConfigs == null || !deviceConfigs.TryGetValue(Configuration.DeviceName, out var desiredConfig))
            {
                return ConfigUpdateResult.NoUpdateRequired(Configuration, deviceTypeName);
            }

            // Apply device-level Enabled flag
            Configuration.Enabled = desiredConfig.Enabled;

            // Record pre-update state for detecting interval/period changes
            var previousIntervalGroups = Configuration.Sensors
                .Where(s => s.IsEffectivelyEnabled)
                .GroupBy(s => (int)s.Report.Interval)
                .ToDictionary(g => g.Key, g => g.Select(s => s.ResourceId).ToHashSet());
            var previousHealthPeriod = Configuration.Periods.ReportHealth;

            // Stop background tasks BEFORE modifying configuration to prevent race conditions
            await StopBackgroundTasksAsync();

            try
            {
                // Apply sensor replacement (REPLACE mode - desired sensors become the new list)
                var sensorResult = ConfigurationUpdateHelper.ReplaceSensors(
                    Configuration, desiredConfig.Sensors, Configuration.DeviceId!);

                if (sensorResult.HasChanges)
                {
                    if (sensorResult.AddedSensors.Count > 0)
                        _logger.LogInformation("Added {Count} sensors: {Names}",
                            sensorResult.AddedSensors.Count, string.Join(", ", sensorResult.AddedSensors));

                    if (sensorResult.RemovedSensors.Count > 0)
                        _logger.LogInformation("Removed {Count} sensors: {Names}",
                            sensorResult.RemovedSensors.Count, string.Join(", ", sensorResult.RemovedSensors));

                    if (sensorResult.UpdatedSensors.Count > 0)
                    {
                        _logger.LogInformation("Updated {Count} sensors: {Names}",
                            sensorResult.UpdatedSensors.Count, string.Join(", ", sensorResult.UpdatedSensors));

                        // Flush recording buffers for updated sensors to avoid mixing data from different intervals
                        if (_context.RecordingService != null)
                        {
                            foreach (var sensorName in sensorResult.UpdatedSensors)
                            {
                                var sensor = Configuration.Sensors.FirstOrDefault(s => s.Name == sensorName);
                                if (sensor != null)
                                {
                                    await _context.RecordingService.FlushSensorAsync(sensor.ShortId, ct);
                                }
                            }
                        }
                    }
                }

                // Apply pipeline updates for existing sensors
                var pipelineUpdateResult = ConfigurationUpdateHelper.ApplyAllPipelineUpdates(
                    Configuration, desiredConfig.Sensors);

                if (pipelineUpdateResult.IsError)
                {
                    throw new InvalidOperationException($"Pipeline update failed: {pipelineUpdateResult.FirstError.Description}");
                }

                // Apply Dtdl.AutoGenEnabled if changed
                var hasDtmiDelta = sensorResult.RequiresDtdlRegeneration;
                if (desiredConfig.Dtdl != null)
                {
                    var previousAutoGenEnabled = Configuration.Dtdl.AutoGenEnabled;
                    var desiredAutoGenEnabled = desiredConfig.Dtdl.AutoGenEnabled;

                    if (previousAutoGenEnabled != desiredAutoGenEnabled)
                    {
                        Configuration.Dtdl.AutoGenEnabled = desiredAutoGenEnabled;
                        _logger.LogInformation(
                            "Dtdl.AutoGenEnabled changed from {Previous} to {Desired}",
                            previousAutoGenEnabled, desiredAutoGenEnabled);

                        // If enabling auto-generation, reset DtdlInterface to trigger regeneration
                        if (desiredAutoGenEnabled)
                        {
                            Configuration.DtdlInterface = null;
                            hasDtmiDelta = true;
                        }
                    }
                }

                // Regenerate DTDL if needed
                if (hasDtmiDelta)
                {
                    Configuration.InitializeDtdl(null, logger: _logger);
                }

                // Apply periods
                if (desiredConfig.Periods != null)
                {
                    if (desiredConfig.Periods.ReportHealth > 0)
                        Configuration.Periods.ReportHealth = desiredConfig.Periods.ReportHealth;
                    if (desiredConfig.Periods.ReportConfiguration >= 0)
                        Configuration.Periods.ReportConfiguration = desiredConfig.Periods.ReportConfiguration;
                }

                // Recalculate send telemetry period
                CalculatedSendTelemetryPeriod = CalculateSendTelemetryPeriod(Configuration);

                // Propagate device-level Enabled flag to all sensors (after ReplaceSensors)
                foreach (var sensor in Configuration.Sensors)
                {
                    sensor.DeviceEnabled = Configuration.Enabled;
                }

                // Refresh parser metadata and restart background tasks with updated configuration
                _protocolParser?.RefreshSensorMetadata();
                _samplingCts = new CancellationTokenSource();
                StartAllBackgroundTasks(_samplingCts.Token);

                // Cache configuration
                var e = new UpdateConfigurationEvent(
                    SubNodeId ?? "unknown",
                    SubscriptionTypes.DeviceConfig,
                    message,
                    DateTimeOffset.UtcNow);
                await _context.ConfigurationCache.SaveRawConfigurationAsync(e.ConfigType, message, ct);

                // Post-hook
                await OnAfterConfigUpdateAsync(e, ct);

                return ConfigUpdateResult.Success(Configuration, deviceTypeName, hasDtmiDelta);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying validated configuration, rolling back changes");

                // Rollback on failure
                ConfigurationUpdateHelper.RestoreBackup(Configuration, backup);
                CalculatedSendTelemetryPeriod = CalculateSendTelemetryPeriod(Configuration);
                _logger.LogInformation("Configuration rolled back to previous state");

                // Restart background tasks with rolled-back configuration to resume operation
                _protocolParser?.RefreshSensorMetadata();
                _samplingCts = new CancellationTokenSource();
                StartAllBackgroundTasks(_samplingCts.Token);

                return ConfigUpdateResult.Failed(Configuration, deviceTypeName, ex.Message);
            }
        }
        finally
        {
            _configUpdateLock.Release();
        }
    }


    /// <summary>
    /// Rollback configuration to a backup state.
    /// Called when any device fails during the apply phase.
    /// </summary>
    /// <param name="backup">The backup to restore from</param>
    /// <param name="ct">Cancellation token</param>
    public virtual async Task RollbackConfigurationAsync(
        DeviceConfigurationBackup backup,
        CancellationToken ct)
    {
        await _configUpdateLock.WaitAsync(ct);
        try
        {
            _logger.LogInformation("Rolling back configuration for device: {DeviceName}", Configuration.DeviceName);
            ConfigurationUpdateHelper.RestoreBackup(Configuration, backup);

            // Recalculate send telemetry period after rollback
            CalculatedSendTelemetryPeriod = CalculateSendTelemetryPeriod(Configuration);

            // Restart background tasks with rolled-back configuration
            await RestartBackgroundTasksAsync();

            _logger.LogInformation("Configuration rollback completed for device: {DeviceName}", Configuration.DeviceName);
        }
        finally
        {
            _configUpdateLock.Release();
        }
    }

    /// <summary>
    /// Creates a backup of the current configuration.
    /// Called by SubNodeManager before the apply phase.
    /// </summary>
    /// <returns>A backup that can be used for rollback</returns>
    public DeviceConfigurationBackup CreateConfigurationBackup()
    {
        return ConfigurationUpdateHelper.CreateBackup(Configuration);
    }

    /// <summary>
    /// Hook: Called after configuration update is applied.
    /// Use this to handle custom/device-specific configuration changes.
    /// Base configuration (sensors, periods) is already applied and cached by the framework.
    /// </summary>
    protected virtual Task OnAfterConfigUpdateAsync(UpdateConfigurationEvent e, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Triggers DataReceived event. Derived classes can call this to raise the event.
    /// Only fires if EnableDataReceivedTracking is true.
    /// This event fires with RAW data before transform/filter processing.
    /// </summary>
    protected void RaiseDataReceived(List<TelemetryMeasure> measures)
    {
        if (!EnableDataReceivedTracking) return;

        DataReceived?.Invoke(this, new DataReceivedEvent(
            SubNodeId: SubNodeId ?? "unknown",
            SubNodeType: SubNodeType,
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
            SubNodeId: SubNodeId ?? "unknown",
            SubNodeType: SubNodeType,
            Data: measures,
            Timestamp: DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Triggers TelemetryRecording event for local storage.
    /// Only fires if EnableTelemetryRecordingTracking is true.
    /// </summary>
    private void RaiseTelemetryRecording(TelemetryRecordingEvent @event)
    {
        TelemetryRecording?.Invoke(this, @event);
    }

    private void OnTelemetryRecording(object? sender, TelemetryRecordingEvent @event)
    {
        _ = _context.RecordingService!.RecordAsync(
            @event.Sensor.ShortId,
            @event.Interval,
            @event.Timestamp,
            @event.Value);
    }

    // ===== Events & Tracking Flags =====

    public event EventHandler<DataReceivedEvent>? DataReceived;
    public event EventHandler<DataProcessedEvent>? DataProcessed;
    public event EventHandler<TelemetryRecordingEvent>? TelemetryRecording;
    public event EventHandler<ConnectionStateChangedEvent>? ConnectionStateChanged;
    public event EventHandler<DeviceStatusChangedEvent>? DeviceStatusChanged;
    public event EventHandler<TelemetrySentEvent>? TelemetrySent;
    public event EventHandler<UpdateConfigurationEvent>? ConfigurationUpdateReceived;
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
                DeviceStatusChanged?.Invoke(this, new(SubNodeId ?? "unknown", SubNodeType, e.FromStatus, e.ToStatus, e.Timestamp));
        };

        // Forward pipeline value change events to device level
        _orchestrator.TelemetryPipeline.ValueChanged += (s, e) => ValueChanged?.Invoke(this, e);

        // Note: Configuration updates and commands are now handled through SubNodeManager's
        // Hybrid routing via RegisterDeviceHandler() in OnInitializeAsync.
        // The handlers are: HandleConfigurationUpdateAsync and HandleCommandReceivedAsync
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
            _logger.LogError(ex, "Failed to send command response");
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

    // ===== Configuration Validation =====

    /// <summary>
    /// Validates the device configuration at startup.
    /// Throws InvalidOperationException for any invalid configuration.
    /// </summary>
    /// <param name="configuration">Device configuration to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    private static void ValidateConfiguration(DeviceConfiguration configuration)
    {
        ValidateSensorIntervals(configuration);
        ValidateBackgroundTaskPeriods(configuration);
    }

    /// <summary>
    /// Validates that all enabled sensors have a valid Interval configured (greater than 0).
    /// Throws InvalidOperationException if any enabled sensor has Interval less than or equal to 0.
    /// </summary>
    /// <param name="configuration">Device configuration to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when any enabled sensor has invalid Interval</exception>
    private static void ValidateSensorIntervals(DeviceConfiguration configuration)
    {
        var invalidSensors = configuration.Sensors
            .Where(s => s.IsEffectivelyEnabled && s.Report.Interval <= 0)
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
    /// Validates background task period settings.
    /// Throws InvalidOperationException for any invalid period configuration.
    /// </summary>
    /// <param name="configuration">Device configuration to validate</param>
    /// <exception cref="InvalidOperationException">Thrown when period configuration is invalid</exception>
    private static void ValidateBackgroundTaskPeriods(DeviceConfiguration configuration)
    {
        var periods = configuration.Periods;

        // ReportConfiguration is a REQUIRED feature and cannot be disabled
        // Minimum value is 1 minute (60000ms) to prevent excessive network traffic
        if (periods.ReportConfiguration < BackgroundTaskPeriods.MinReportConfigurationPeriod)
        {
            throw new InvalidOperationException(
                $"Periods.ReportConfiguration must be at least {BackgroundTaskPeriods.MinReportConfigurationPeriod}ms (1 minute). " +
                $"Current value: {periods.ReportConfiguration}ms. " +
                $"This is a required feature for Digital Twin synchronization and cannot be disabled. " +
                $"Please set a value >= {BackgroundTaskPeriods.MinReportConfigurationPeriod}ms in appsettings.json.");
        }

        // ReportHealth should also have a reasonable minimum (optional, but warn if too low)
        if (periods.ReportHealth > 0 && periods.ReportHealth < 1000)
        {
            throw new InvalidOperationException(
                $"Periods.ReportHealth must be at least 1000ms (1 second) if enabled. " +
                $"Current value: {periods.ReportHealth}ms. " +
                $"Please set a value >= 1000ms in appsettings.json.");
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
            .Where(s => s.IsEffectivelyEnabled && s.Report.Interval > 0)
            .Select(s => (int)s.Report.Interval)
            .ToList();

        // This should never happen after validation, but provide safe default
        if (enabledIntervals.Count == 0)
        {
            return 5000; // Default fallback
        }

        return enabledIntervals.Min();
    }

    // ===== Background Task Management (for dynamic restart on config update) =====

    /// <summary>
    /// Starts all background tasks that may need to be restarted on config update.
    /// This includes: device-specific tasks (polling/sampling), batch send task, and health task.
    /// Note: Device-level Enabled flag is applied at sensor level via Sensor.IsEffectivelyEnabled.
    /// </summary>
    private void StartAllBackgroundTasks(CancellationToken ct)
    {
        // Start device-specific background tasks (polling, subscription, etc.)
        _ = StartBackgroundTasksAsync(ct);

        // Start batch send task only if not using immediate send mode
        if (Configuration.Periods.BatchSend)
        {
            StartBatchSendTask(ct);
        }
        else
        {
            _logger.LogDebug("Batch send task skipped - ImmediateSend is enabled for device {SubNodeId}", SubNodeId);
        }

        // Start health task
        StartHealthTask(ct);
    }

    /// <summary>
    /// Starts the batch send task with current CalculatedSendTelemetryPeriod.
    /// </summary>
    private void StartBatchSendTask(CancellationToken ct)
    {
        var sendPeriod = CalculatedSendTelemetryPeriod;
        _batchSendTask = Task.Run(async () =>
        {
            _logger.LogDebug("Starting batch send task with period {Period}ms for device {SubNodeId}", sendPeriod, SubNodeId);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(sendPeriod, ct);

                    var batch = new List<TelemetryMeasure>();
                    while (_telemetryBatch.TryDequeue(out var measure))
                    {
                        batch.Add(measure);
                    }

                    if (batch.Count > 0)
                    {
                        _logger.LogDebug(
                            "Sending telemetry batch: {Count} measures for device {SubNodeId}",
                            batch.Count, SubNodeId);
                        await SendTelemetryAsync(batch, ct);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Send remaining data before exit
                var remaining = new List<TelemetryMeasure>();
                while (_telemetryBatch.TryDequeue(out var measure))
                {
                    remaining.Add(measure);
                }
                if (remaining.Count > 0)
                {
                    _logger.LogDebug("Sending remaining {Count} measures before shutdown for device {SubNodeId}", remaining.Count, SubNodeId);
                    await SendTelemetryAsync(remaining, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch send task for device {SubNodeId}", SubNodeId);
            }
        }, ct);
    }

    /// <summary>
    /// Starts the health reporting task with current ReportHealth period.
    /// </summary>
    private void StartHealthTask(CancellationToken ct)
    {
        var healthPeriod = Configuration.Periods.ReportHealth;

        _healthTask = Task.Run(async () =>
        {
            _logger.LogDebug("Starting health reporting task with period {Period}ms for device {SubNodeId}", healthPeriod, SubNodeId);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await ReportHealthAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in health reporting task for device {SubNodeId}", SubNodeId);
                    }

                    await Task.Delay(healthPeriod, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogDebug("Health reporting task cancelled for device {SubNodeId}", SubNodeId);
            }
        }, ct);
    }

    /// <summary>
    /// Stops all background tasks (device-specific + batch send + health).
    /// Cancels the sampling CancellationTokenSource and awaits task completion.
    /// Must be called before modifying Configuration to avoid race conditions.
    /// </summary>
    protected async Task StopBackgroundTasksAsync()
    {
        _logger.LogInformation("Stopping background tasks for device {SubNodeId}", SubNodeId);

        // Step 1: Cancel all tasks via CancellationToken
        if (_samplingCts != null)
        {
            await _samplingCts.CancelAsync();
            _samplingCts.Dispose();
            _samplingCts = null;
        }

        // Step 2: Await device-specific tasks (polling/streaming/subscription) from derived classes
        await StopDeviceTasksAsync();

        // Step 3: Await batch send and health tasks
        var tasksToAwait = new List<Task>();
        if (_batchSendTask is { IsCompleted: false })
            tasksToAwait.Add(_batchSendTask);
        if (_healthTask is { IsCompleted: false })
            tasksToAwait.Add(_healthTask);

        if (tasksToAwait.Count > 0)
        {
            try
            {
                await Task.WhenAll(tasksToAwait).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timed out waiting for background tasks to stop for device {SubNodeId}", SubNodeId);
            }
            catch (OperationCanceledException)
            {
                // Expected when tasks are cancelled
            }
        }

        _batchSendTask = null;
        _healthTask = null;

        _logger.LogInformation("Background tasks stopped for device {SubNodeId}", SubNodeId);
    }

    /// <summary>
    /// Restarts background tasks when sensor intervals or periods change.
    /// Stops all tasks, refreshes parser metadata, then starts new tasks.
    /// </summary>
    protected virtual async Task RestartBackgroundTasksAsync()
    {
        _logger.LogInformation("Restarting background tasks due to config changes for device {SubNodeId}", SubNodeId);

        // Step 1: Stop all background tasks and await completion
        await StopBackgroundTasksAsync();

        // Step 2: Refresh parser's sensor metadata to reflect configuration changes
        // This ensures new sensors are recognized by the parser
        _protocolParser?.RefreshSensorMetadata();

        // Step 3: Create new CancellationTokenSource and start tasks
        _samplingCts = new CancellationTokenSource();
        StartAllBackgroundTasks(_samplingCts.Token);

        _logger.LogInformation("Background tasks restarted successfully for device {SubNodeId}", SubNodeId);
    }
}
