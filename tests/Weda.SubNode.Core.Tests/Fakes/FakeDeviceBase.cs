using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Tests.Fakes;

/// <summary>
/// Fake device for testing SubNodeManager's two-phase config update.
/// Allows configuring validation and apply results without complex dependencies.
/// </summary>
public class FakeDevice : IDevice
{
    private readonly DeviceConfiguration _configuration;
    private ConfigUpdateValidationResult _validationResult;
    private ConfigUpdateResult _applyResult;
    private readonly DeviceConfigurationBackup _backup;

    public FakeDevice(DeviceConfiguration configuration)
    {
        _configuration = configuration;
        _validationResult = ConfigUpdateValidationResult.Valid(configuration.SubNodeType.ToString());
        _applyResult = ConfigUpdateResult.Success(configuration, configuration.SubNodeType.ToString());
        _backup = new DeviceConfigurationBackup
        {
            ReportHealthPeriod = 60000,
            ReportConfigurationPeriod = 300000,
            SensorBackups = new List<SensorReportBackup>()
        };
    }

    // IDevice implementation
    public string DeviceName => _configuration.DeviceName;
    public string SubNodeId => _configuration.DeviceId ?? "test-subnode-001";
    public SubNodeType SubNodeType => _configuration.SubNodeType;
    public DeviceConfiguration Configuration => _configuration;
    public DeviceInfo DeviceInfo => _configuration.DeviceInfo;
    public IReadOnlyDictionary<string, object> Properties => _configuration.Properties;
    public DeviceStatus Status => DeviceStatus.Running;
    public CommunicationState ConnectionState => CommunicationState.Connected;

    // Two-phase config update methods - call tracking
    public int ValidateCallCount { get; private set; }
    public int ApplyCallCount { get; private set; }
    public int RollbackCallCount { get; private set; }
    public int BackupCallCount { get; private set; }

    public FakeDevice WithValidationResult(ConfigUpdateValidationResult result)
    {
        _validationResult = result;
        return this;
    }

    public FakeDevice WithApplyResult(ConfigUpdateResult result)
    {
        _applyResult = result;
        return this;
    }

    public Task<ConfigUpdateValidationResult> ValidateConfigurationUpdateAsync(
        SubNodeConfigUpdateMessage message,
        CancellationToken ct)
    {
        ValidateCallCount++;
        return Task.FromResult(_validationResult);
    }

    public Task<ConfigUpdateResult> ApplyValidatedConfigurationAsync(
        SubNodeConfigUpdateMessage message,
        DeviceConfigurationBackup backup,
        CancellationToken ct)
    {
        ApplyCallCount++;
        return Task.FromResult(_applyResult);
    }

    public Task RollbackConfigurationAsync(
        DeviceConfigurationBackup backup,
        CancellationToken ct)
    {
        RollbackCallCount++;
        return Task.CompletedTask;
    }

    public DeviceConfigurationBackup CreateConfigurationBackup()
    {
        BackupCallCount++;
        return _backup;
    }

    // IDevice lifecycle - minimal implementation
    public Task<bool> InitializeAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> StopAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public void Dispose() { }

    // IDevice telemetry - minimal implementation
    public Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<TelemetryMeasure>());
    public Task<DeviceHealth> GetHealthAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new DeviceHealth { DeviceId = SubNodeId, Status = HealthStatus.Healthy });
    public Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
    public Task<string?> RegisterAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(SubNodeId);
    public Task SendTelemetryAsync(IAsyncEnumerable<TelemetryMeasure> data, CancellationToken cancellationToken = default, params IDspFilter[] runtimeFilters)
        => Task.CompletedTask;
    public Task ReportHealthAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    // Sensor access - minimal implementation
    public Sensor GetSensor(string sensorName) => throw new KeyNotFoundException(sensorName);
    public Sensor? FindSensor(string sensorName) => null;
    public Sensor GetSensorByResourceId(string resourceId) => throw new KeyNotFoundException(resourceId);
    public Sensor? FindSensorByResourceId(string resourceId) => null;

    // Events - tracking flags
    public bool EnableDataReceivedTracking { get; set; }
    public bool EnableDataProcessedTracking { get; set; }
    public bool EnableConnectionStateTracking { get; set; }
    public bool EnableDeviceStatusTracking { get; set; }
    public bool EnableTelemetrySentTracking { get; set; }
    public bool EnableConfigurationUpdateTracking { get; set; }
    public bool EnableCommandReceivedTracking { get; set; }
    public bool EnableValueChangeTracking { get; set; }

    public event EventHandler<DataReceivedEvent>? DataReceived;
    public event EventHandler<DataProcessedEvent>? DataProcessed;
    public event EventHandler<ConnectionStateChangedEvent>? ConnectionStateChanged;
    public event EventHandler<DeviceStatusChangedEvent>? DeviceStatusChanged;
    public event EventHandler<TelemetrySentEvent>? TelemetrySent;
    public event EventHandler<UpdateConfigurationEvent>? ConfigurationUpdateReceived;
    public event EventHandler<ExecuteCommandEvent>? CommandReceived;
    public event EventHandler<TelemetryValueChangedEvent>? ValueChanged;

    // Suppress warnings for unused events
    protected virtual void OnDataReceived(DataReceivedEvent e) => DataReceived?.Invoke(this, e);
    protected virtual void OnDataProcessed(DataProcessedEvent e) => DataProcessed?.Invoke(this, e);
    protected virtual void OnConnectionStateChanged(ConnectionStateChangedEvent e) => ConnectionStateChanged?.Invoke(this, e);
    protected virtual void OnDeviceStatusChanged(DeviceStatusChangedEvent e) => DeviceStatusChanged?.Invoke(this, e);
    protected virtual void OnTelemetrySent(TelemetrySentEvent e) => TelemetrySent?.Invoke(this, e);
    protected virtual void OnConfigurationUpdateReceived(UpdateConfigurationEvent e) => ConfigurationUpdateReceived?.Invoke(this, e);
    protected virtual void OnCommandReceived(ExecuteCommandEvent e) => CommandReceived?.Invoke(this, e);
    protected virtual void OnValueChanged(TelemetryValueChangedEvent e) => ValueChanged?.Invoke(this, e);
}
