using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Core device interface defining lifecycle, telemetry, and cloud interaction
/// </summary>
public interface IDevice : IDisposable
{
    // ===== Identity =====

    /// <summary>
    /// SubNode identifier shared by all devices in this SubNode (e.g., "74fe488d5d54-ffff").
    /// All devices registered under the same SubNode share this identifier.
    /// </summary>
    string SubNodeId { get; }

    /// <summary>
    /// Device Name
    /// </summary>
    string DeviceName { get; }
    /// <summary>
    /// Device type (e.g., "adamEthernet", "modbusRTU")
    /// </summary>
    SubNodeType SubNodeType { get; }

    /// <summary>
    /// Device configuration
    /// </summary>
    DeviceConfiguration Configuration { get; }

    /// <summary>
    /// Custom device properties (e.g., TargetDeviceId, Location)
    /// </summary>
    IReadOnlyDictionary<string, object> Properties { get; }

    /// <summary>
    /// Current device status
    /// </summary>
    DeviceStatus Status { get; }

    /// <summary>
    /// Current communication state
    /// </summary>
    CommunicationState ConnectionState { get; }

    // ===== Lifecycle =====

    /// <summary>
    /// Initialize device: establish connections, register with cloud
    /// </summary>
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start background tasks: telemetry reading, health reporting, command handling
    /// </summary>
    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop background tasks and release resources safely
    /// </summary>
    Task<bool> StopAsync(CancellationToken cancellationToken = default);

    // ===== Local Operations (SubNode ↔ Device) =====

    /// <summary>
    /// Read telemetry from specific sensor value of device (well-parsed physical values)
    /// </summary>
    Task<List<TelemetryMeasure>> ReadSensorTelemetryAsync(string sensorResourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read telemetry from device (well-parsed physical values)
    /// </summary>
    Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get device health status
    /// </summary>
    Task<DeviceHealth> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute command on physical device (called internally from CommandReceived event handler)
    /// </summary>
    Task<int> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default);

    // ===== Cloud Interactions (SubNode → Cloud) =====

    /// <summary>
    /// Register device with DMA and get device ID
    /// Returns device ID if successful, null otherwise
    /// </summary>
    Task<string?> RegisterAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Send telemetry to cloud with optional runtime DSP filters
    /// </summary>
    Task SendTelemetryAsync(
        IAsyncEnumerable<TelemetryMeasure> data,
        CancellationToken cancellationToken = default,
        params IDspFilter[] runtimeFilters);

    /// <summary>
    /// Report health to cloud
    /// </summary>
    Task ReportHealthAsync(CancellationToken cancellationToken = default);

    // ===== Events & Tracking Flags =====
    // All tracking flags default to false for better performance.
    // Enable only the events you need to monitor.

    /// <summary>
    /// Event: Telemetry data received from device (Device → SubNode).
    /// Only fires when EnableDataReceivedTracking is true.
    /// </summary>
    event EventHandler<DataReceivedEvent>? DataReceived;

    /// <summary>
    /// Gets or sets whether DataReceived events are emitted.
    /// Default is false.
    /// </summary>
    bool EnableDataReceivedTracking { get; set; }

    /// <summary>
    /// Event: Telemetry data processed through pipeline (Device → SubNode).
    /// Fired after transformation and DSP filtering, before sending to cloud.
    /// Only fires when EnableDataProcessedTracking is true.
    /// </summary>
    event EventHandler<DataProcessedEvent>? DataProcessed;

    /// <summary>
    /// Gets or sets whether DataProcessed events are emitted.
    /// Default is false.
    /// </summary>
    bool EnableDataProcessedTracking { get; set; }

    /// <summary>
    /// Event: Connection state changed (Device → SubNode).
    /// Only fires when EnableConnectionStateTracking is true.
    /// </summary>
    event EventHandler<ConnectionStateChangedEvent>? ConnectionStateChanged;

    /// <summary>
    /// Gets or sets whether ConnectionStateChanged events are emitted.
    /// Default is false.
    /// </summary>
    bool EnableConnectionStateTracking { get; set; }

    /// <summary>
    /// Event: Device status changed (Device → SubNode).
    /// Only fires when EnableDeviceStatusTracking is true.
    /// </summary>
    event EventHandler<DeviceStatusChangedEvent>? DeviceStatusChanged;

    /// <summary>
    /// Gets or sets whether DeviceStatusChanged events are emitted.
    /// Default is false.
    /// </summary>
    bool EnableDeviceStatusTracking { get; set; }

    /// <summary>
    /// Event: Telemetry sent to cloud (SubNode internal).
    /// Only fires when EnableTelemetrySentTracking is true.
    /// </summary>
    event EventHandler<TelemetrySentEvent>? TelemetrySent;

    /// <summary>
    /// Gets or sets whether TelemetrySent events are emitted.
    /// Default is false.
    /// </summary>
    bool EnableTelemetrySentTracking { get; set; }

    /// <summary>
    /// Event: Configuration update received from cloud (Cloud → SubNode).
    /// Only fires when EnableConfigurationUpdateTracking is true.
    /// NOTE: For internal framework use only. Use OnBeforeConfigUpdateAsync/OnAfterConfigUpdateAsync hooks instead.
    /// </summary>
    event EventHandler<UpdateConfigurationEvent>? ConfigurationUpdateReceived;

    /// <summary>
    /// Gets or sets whether ConfigurationUpdateReceived events are emitted.
    /// Default is false.
    /// </summary>
    bool EnableConfigurationUpdateTracking { get; set; }

    /// <summary>
    /// Event: Command received from cloud (Cloud → SubNode).
    /// Only fires when EnableCommandReceivedTracking is true.
    /// NOTE: For internal framework use only. Command execution is automatic.
    /// Use OnBeforeCommandAsync/OnAfterCommandAsync hooks for custom logic.
    /// </summary>
    event EventHandler<ExecuteCommandEvent>? CommandReceived;

    /// <summary>
    /// Gets or sets whether CommandReceived events are emitted.
    /// Default is false.
    /// </summary>
    bool EnableCommandReceivedTracking { get; set; }

    /// <summary>
    /// Event: Telemetry values changed through transform or filter pipeline.
    /// Provides detailed value-level monitoring including input/output values.
    /// Only fires when EnableValueChangeTracking is true.
    /// </summary>
    event EventHandler<TelemetryValueChangedEvent>? ValueChanged;

    /// <summary>
    /// Gets or sets whether ValueChanged events are emitted.
    /// When enabled, events are emitted for each transform and filter stage.
    /// Default is false.
    /// </summary>
    bool EnableValueChangeTracking { get; set; }

    // ===== Sensor Access =====

    /// <summary>
    /// Gets a sensor by name. Throws if not found.
    /// </summary>
    /// <param name="sensorName">The sensor name to search for</param>
    /// <returns>The sensor instance</returns>
    /// <exception cref="KeyNotFoundException">Thrown when sensor is not found</exception>
    Sensor GetSensor(string sensorName);

    /// <summary>
    /// Finds a sensor by name. Returns null if not found.
    /// </summary>
    /// <param name="sensorName">The sensor name to search for</param>
    /// <returns>The sensor instance or null</returns>
    Sensor? FindSensor(string sensorName);

    /// <summary>
    /// Gets a sensor by ResourceId. Throws if not found.
    /// </summary>
    /// <param name="resourceId">The sensor ResourceId to search for</param>
    /// <returns>The sensor instance</returns>
    /// <exception cref="KeyNotFoundException">Thrown when sensor is not found</exception>
    Sensor GetSensorByResourceId(string resourceId);

    /// <summary>
    /// Finds a sensor by ResourceId. Returns null if not found.
    /// </summary>
    /// <param name="resourceId">The sensor ResourceId to search for</param>
    /// <returns>The sensor instance or null</returns>
    Sensor? FindSensorByResourceId(string resourceId);

    // ===== Two-Phase Configuration Update (Transaction Semantics) =====

    /// <summary>
    /// Phase 1: Validates configuration update without modifying state.
    /// Called by SubNodeManager to validate all devices before applying any.
    /// </summary>
    /// <param name="message">The configuration update message</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result with status and optional error message</returns>
    Task<ConfigUpdateValidationResult> ValidateConfigurationUpdateAsync(
        SubNodeConfigUpdateMessage message,
        CancellationToken ct);

    /// <summary>
    /// Phase 2: Applies a validated configuration update.
    /// Only called after all devices pass validation.
    /// </summary>
    /// <param name="message">The configuration update message</param>
    /// <param name="backup">The backup created before apply phase</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of applying the configuration</returns>
    Task<ConfigUpdateResult> ApplyValidatedConfigurationAsync(
        SubNodeConfigUpdateMessage message,
        DeviceConfigurationBackup backup,
        CancellationToken ct);

    /// <summary>
    /// Rollback configuration to a backup state.
    /// Called when any device fails during the apply phase.
    /// </summary>
    /// <param name="backup">The backup to restore from</param>
    /// <param name="ct">Cancellation token</param>
    Task RollbackConfigurationAsync(
        DeviceConfigurationBackup backup,
        CancellationToken ct);

    /// <summary>
    /// Creates a backup of the current configuration.
    /// Called by SubNodeManager before the apply phase.
    /// </summary>
    /// <returns>A backup that can be used for rollback</returns>
    DeviceConfigurationBackup CreateConfigurationBackup();
}
