using Microsoft.Extensions.Logging;
using Polly;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Devices.Health;
using Weda.SubNode.Core.Devices.Lifecycle;
using Weda.SubNode.Core.Devices.Retry;
using Weda.SubNode.Core.Devices.StateMachine;
using Weda.SubNode.Core.Managers;
using Weda.SubNode.Core.Policies;
using Weda.SubNode.Core.Telemetry;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Centralizes all device manager creation and coordination.
/// Extracts complexity from DeviceBase to achieve true lightweight design.
/// </summary>
public sealed class DeviceOrchestrator : IDisposable
{
    private string _deviceId = "uninitialized";
    private readonly ILogger<DeviceOrchestrator> _logger;

    // All managers in one place
    public IDeviceStateMachine StateMachine { get; }
    public DeviceHealthMonitor HealthMonitor { get; }

    /// <summary>
    /// Polly resilience pipeline for general device operations (telemetry, commands, etc.)
    /// Replaces RetryOrchestrator with standardized retry, circuit breaker, and timeout handling.
    /// </summary>
    public ResiliencePipeline OperationPipeline { get; }

    public ITelemetryPipeline TelemetryPipeline { get; }
    public DeviceLifecycleManager LifecycleManager { get; }
    public IDeviceConnectionManager ConnectionManager { get; }

    public string DeviceId => _deviceId;

    // Event aggregator
    public event EventHandler<DeviceStatusTransitionEvent>? StatusChanged;
    public event EventHandler<DeviceHealthChangedEvent>? HealthChanged;
    public event EventHandler<RetryAttemptEvent>? RetryAttempting;
    public event EventHandler<CircuitBreakerStateChangedEvent>? CircuitBreakerStateChanged;
    public event EventHandler<TelemetryPipelineStageEvent>? TelemetryPipelineStageExecuting;
    public event EventHandler<DeviceLifecycleEvent>? LifecycleExecuting;

    public DeviceOrchestrator(
        IWedaApplicationContext context,
        ICommunication communication,
        ILifecycleHooks lifecycleHooks,
        DeviceConfiguration? configuration = null,
        string? deviceId = null)
    {
        _logger = context.GetLogger<DeviceOrchestrator>();

        // Use provided deviceId or default to "uninitialized"
        _deviceId = deviceId ?? "uninitialized";

        // Create all managers with deviceId
        StateMachine = new DeviceStateMachine(_deviceId, DeviceStatus.Initializing);

        HealthMonitor = new DeviceHealthMonitor(
            _deviceId,
            context.GetLogger<DeviceHealthMonitor>(),
            communication: communication,
            thresholds: null);

        // Create Polly operation pipeline for general device operations
        OperationPipeline = ConnectionPolicies.CreateGeneralOperationPipeline(
            context.GetLogger<DeviceOrchestrator>());

        // Keep RetryOrchestrator for backward compatibility (marked as obsolete)

        TelemetryPipeline = new TelemetryPipeline(
            _deviceId,
            configuration,
            context.CloudService,
            context.GetLogger<TelemetryPipeline>(),
            healthMonitor: HealthMonitor);

        LifecycleManager = new DeviceLifecycleManager(
            _deviceId,
            StateMachine,
            lifecycleHooks,
            context.GetLogger<DeviceLifecycleManager>());

        // Use ConnectionOptions from context for Polly pipeline configuration
        ConnectionManager = new DeviceConnectionManager(
            communication,
            context.CloudService,
            context.ConnectionOptions,
            context.GetLogger<DeviceConnectionManager>());

        // Wire up all events
        WireUpEvents();
    }

    /// <summary>
    /// Sets the device ID after registration/initialization.
    /// Should be called after GetDeviceOrRegisterAsync returns the actual deviceId.
    /// </summary>
    public void SetDeviceId(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        _deviceId = deviceId;
        HealthMonitor.SetDeviceId(deviceId);
        TelemetryPipeline.SetDeviceId(deviceId);
        _logger.LogInformation("Device ID set to: {DeviceId}", _deviceId);
    }

    private void WireUpEvents()
    {
        StateMachine.StatusTransitioned += (s, e) =>
        {
            _logger.LogDebug("State transition: {From} -> {To}", e.FromStatus, e.ToStatus);
            StatusChanged?.Invoke(s, e);
        };

        HealthMonitor.HealthStatusChanged += (s, e) =>
        {
            _logger.LogDebug("Health changed: {Previous} -> {Current}", e.PreviousStatus, e.CurrentStatus);
            HealthChanged?.Invoke(s, e);
        };

        TelemetryPipeline.StageExecuting += (s, e) =>
        {
            if (e.Error != null)
                _logger.LogError("Telemetry stage {Stage} failed: {Error}", e.Stage, e.Error);
            TelemetryPipelineStageExecuting?.Invoke(s, e);
        };

        LifecycleManager.LifecycleExecuting += (s, e) =>
        {
            if (e.Error != null)
                _logger.LogError("Lifecycle stage {Stage} failed: {Error}", e.Stage, e.Error);
            LifecycleExecuting?.Invoke(s, e);
        };
    }

    public void Dispose()
    {
        // Dispose managers in reverse order of creation
        LifecycleManager.DisposeAsync().GetAwaiter().GetResult();
    }
}
