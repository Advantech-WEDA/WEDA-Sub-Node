using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
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
    /// Unique device identifier (e.g., "74fe488d5d54-ffff")
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// Device type (e.g., "adamEthernet", "modbusRTU")
    /// </summary>
    DeviceType DeviceType { get; }

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

    // ===== DSP Filters =====

    /// <summary>
    /// DSP filters applied to telemetry data
    /// </summary>
    List<IDspFilter>? DspFilters { get; set; }

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
    Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default);

    // ===== Cloud Interactions (SubNode → Cloud) =====

    /// <summary>
    /// Register device with DMA and get device ID
    /// Returns device ID if successful, null otherwise
    /// </summary>
    Task<string?> RegisterAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current configuration from cloud
    /// </summary>
    Task<DeviceConfiguration?> GetCurrentConfigurationAsync(CancellationToken cancellationToken = default);

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

    // ===== Events =====

    /// <summary>
    /// Event: Telemetry data received from device (Device → SubNode)
    /// </summary>
    event EventHandler<DataReceivedEvent>? DataReceived;

    /// <summary>
    /// Event: Connection state changed (Device → SubNode)
    /// </summary>
    event EventHandler<ConnectionStateChangedEvent>? ConnectionStateChanged;

    /// <summary>
    /// Event: Device status changed (Device → SubNode)
    /// </summary>
    event EventHandler<DeviceStatusChangedEvent>? DeviceStatusChanged;

    /// <summary>
    /// Event: Telemetry sent to cloud (SubNode internal)
    /// </summary>
    event EventHandler<TelemetrySentEvent>? TelemetrySent;

    /// <summary>
    /// Event: Configuration update received from cloud (Cloud → SubNode)
    /// NOTE: For internal framework use only. Use OnBeforeConfigUpdateAsync/OnAfterConfigUpdateAsync hooks instead.
    /// </summary>
    event EventHandler<UpdateConfigurationEvent>? ConfigurationUpdateReceived;

    /// <summary>
    /// Event: Command received from cloud (Cloud → SubNode)
    /// NOTE: For internal framework use only. Command execution is automatic.
    /// Use OnBeforeCommandAsync/OnAfterCommandAsync hooks for custom logic.
    /// </summary>
    event EventHandler<ExecuteCommandEvent>? CommandReceived;
}
