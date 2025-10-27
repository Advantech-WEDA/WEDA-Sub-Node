using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
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
    protected readonly DeviceOrchestrator _orchestrator;
    protected readonly DeviceInitializer _initializer;

    private CancellationTokenSource? _runningCts;

    public DeviceConfiguration Configuration { get; }
    public string DeviceId => _orchestrator.DeviceId;
    public string DeviceName => Configuration.DeviceInfo.DeviceName;
    public DeviceType DeviceType => Configuration.DeviceInfo.DeviceType;
    public DeviceInfo DeviceInfo => Configuration.DeviceInfo;
    public IReadOnlyDictionary<string, object> Properties => Configuration.Properties;
    public DeviceStatus Status => _orchestrator.StateMachine.CurrentStatus;
    public CommunicationState ConnectionState => _communication.State;
    public virtual List<IDspFilter>? DspFilters { get; set; }

    protected DeviceBase(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        ICommunication communication)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = context.GetLogger<DeviceBase>();
        _cloudService = context.CloudService;

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

        // Configure DSP filters
        if (DspFilters != null)
        {
            foreach (var filter in DspFilters)
                _orchestrator.TelemetryPipeline.AddFilter(filter);
        }

        // Wire events
        WireEvents();
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

        var subResult = await _orchestrator.ConnectionManager.SubscribeToCloudEventsAsync(deviceId, ct);
        if (subResult.IsError) return subResult.Errors;

        await OnAfterInitializeAsync(ct);
        return Result.Success;
    }

    async Task<ErrorOr<Success>> ILifecycleHooks.OnStartAsync(CancellationToken ct)
    {
        _runningCts = new CancellationTokenSource();
        _ = StartBackgroundTasksAsync(_runningCts.Token);
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
        var result = await _orchestrator.RetryOrchestrator.ExecuteAsync<ErrorOr<Success>>(
            async c => await _orchestrator.TelemetryPipeline.ProcessAsync(measures, c),
            "SendTelemetry", ct);
        return !result.IsError;
    }

    public async Task SendTelemetryAsync(IAsyncEnumerable<TelemetryMeasure> data, CancellationToken ct = default, params IDspFilter[] filters)
    {
        var list = new List<TelemetryMeasure>();
        await foreach (var m in data.WithCancellation(ct)) list.Add(m);
        await SendTelemetryAsync(list, ct);
    }

    public async Task<DeviceHealth> GetHealthAsync(CancellationToken ct = default)
        => await _orchestrator.HealthMonitor.GetCurrentHealthAsync(ct);

    public async Task ReportHealthAsync(CancellationToken ct = default)
        => await _cloudService.ReportHealthAsync(DeviceId ?? "unknown", await GetHealthAsync(ct), ct);

    public async Task<string?> RegisterAsync(CancellationToken ct = default)
        => await _cloudService.GetOrRegisterDeviceIdAsync(DeviceInfo, ct);

    public async Task<DeviceConfiguration?> GetCurrentConfigurationAsync(CancellationToken ct = default)
        => await _cloudService.GetDeviceConfigurationAsync(DeviceId ?? "unknown", ct);

    public abstract Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken ct = default);

    // ===== Hooks =====

    protected virtual Task OnBeforeInitializeAsync(CancellationToken ct) => Task.CompletedTask;
    protected virtual Task OnAfterInitializeAsync(CancellationToken ct) => Task.CompletedTask;
    protected abstract Task StartBackgroundTasksAsync(CancellationToken ct);

    /// <summary>
    /// Triggers DataReceived event. Derived classes can call this to raise the event.
    /// </summary>
    protected void RaiseDataReceived(List<TelemetryMeasure> measures)
    {
        DataReceived?.Invoke(this, new DataReceivedEvent(
            DeviceId: DeviceId ?? "unknown",
            DeviceType: DeviceType,
            Data: measures,
            Timestamp: DateTimeOffset.UtcNow));
    }

    // ===== Events =====

    public event EventHandler<DataReceivedEvent>? DataReceived;
    public event EventHandler<ConnectionStateChangedEvent>? ConnectionStateChanged;
    public event EventHandler<DeviceStatusChangedEvent>? DeviceStatusChanged;
    public event EventHandler<TelemetrySentEvent>? TelemetrySent;
    public event EventHandler<UpdateConfigurationEvent>? ConfigurationUpdateReceived;
    public event EventHandler<ExecuteCommandEvent>? CommandReceived;

    private void WireEvents()
    {
        _communication.StateChanged += (s, e) => ConnectionStateChanged?.Invoke(this, e);
        _orchestrator.StatusChanged += (s, e) => DeviceStatusChanged?.Invoke(this, new(DeviceId ?? "unknown", DeviceType, e.FromStatus, e.ToStatus, e.Timestamp));
        _orchestrator.ConnectionManager.ConfigurationUpdateReceived += async e =>
        {
            ConfigurationUpdateReceived?.Invoke(this, e);
            await Task.CompletedTask;
        };
        _orchestrator.ConnectionManager.CommandReceived += async e =>
        {
            CommandReceived?.Invoke(this, e);
            await Task.CompletedTask;
        };
    }

    public void Dispose()
    {
        _orchestrator.Dispose();
        GC.SuppressFinalize(this);
    }
}
