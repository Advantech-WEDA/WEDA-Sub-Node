using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Cloud;

/// <summary>
/// Null object pattern implementation of IWedaCloudService
/// Used for testing and standalone device operation without cloud connectivity
/// Logs all operations instead of sending to cloud
/// </summary>
public class NullCloudService : IWedaCloudService
{
    private readonly ILogger<NullCloudService> _logger;
    private bool _isConnected;
    private bool _disposed;
    private DeviceConfiguration? _deviceConfiguration;

    public NullCloudService(ILogger<NullCloudService>? logger = null)
    {
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<NullCloudService>();
    }

    public bool IsConnected => _isConnected;

    public void ConfigureTopics(NatsTopicAssignments topicAssignments)
    {
        _logger.LogInformation(
            "Configure NATS topics (simulated): TelemetryTopic={TelemetryTopic}, HealthTopic={HealthTopic}",
            topicAssignments?.TelemetryTopic ?? "null",
            topicAssignments?.HealthTopic ?? "null");
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Simulating cloud connection");
        _isConnected = true;
        return Task.FromResult(true);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Simulating cloud disconnection");
        _isConnected = false;
        return Task.CompletedTask;
    }

    public Task<string?> GetOrRegisterDeviceIdAsync(DeviceInfo info, CancellationToken cancellationToken = default)
    {
        var mockDeviceId = Guid.NewGuid().ToString();
        _logger.LogInformation(
            "Get or register device ID (simulated): DeviceName={DeviceName}, DeviceId={DeviceId}",
            info.DeviceName,
            mockDeviceId);

        return Task.FromResult<string?>(mockDeviceId);
    }

    public Task<bool> UploadDeviceConfigurationAsync(DeviceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Device registration (simulated): DeviceName={DeviceId}, Type={DeviceType}, Manufacturer={Manufacturer}, Model={Model}",
            configuration.DeviceId,
            configuration.DeviceType,
            configuration.DeviceCapabilities.Manufacturer,
            configuration.DeviceCapabilities.Model);

        _logger.LogDebug(
            "Sensors count: {SensorCount}",
            configuration.Sensors.Count);

        foreach (var sensor in configuration.Sensors)
        {
            _logger.LogDebug(
                "  - Sensor: {SensorId} ({Name}), DTMI: {Dtmi}, Group: {Group}",
                sensor.ResourceId,
                sensor.Name,
                sensor.Dtmi,
                sensor.SensorGroup);
        }

        _deviceConfiguration = configuration;

        return Task.FromResult(true);
    }

    public Task<DeviceConfiguration?> GetDeviceConfigurationAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Get device configuration (simulated): DeviceId={DeviceId}",
            deviceId);

        return Task.FromResult(_deviceConfiguration);
    }

    public Task<bool> SendTelemetryAsync(string deviceId, TelemetryData telemetryData, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Send telemetry (simulated): DeviceId={DeviceId}, MeasureCount={Count}",
            deviceId,
            telemetryData.Measures.Count);

        foreach (var measure in telemetryData.Measures)
        {
            // Try to find sensor info from configuration
            var sensor = _deviceConfiguration?.Sensors
                .FirstOrDefault(s => s.ResourceId == measure.ResourceId);

            var displayName = GetDisplayName(sensor?.Name, sensor?.SensorGroup ?? SensorGroup.SYS);
            var unit = GetUnitFromSensorGroup(sensor?.SensorGroup);
            var valueStr = FormatValue(measure.Value);

            _logger.LogInformation(
                "Sensor {SensorName} updated: {Value} {Unit}",
                displayName,
                valueStr,
                unit);
        }

        return Task.FromResult(true);
    }

    private static string GetUnitFromSensorGroup(SensorGroup? sensorGroup)
    {
        return sensorGroup switch
        {
            SensorGroup.TEMP => "°C",
            SensorGroup.PWR => "V/A",
            SensorGroup.AI => "",
            SensorGroup.AO => "",
            SensorGroup.DI => "",
            SensorGroup.DO => "",
            SensorGroup.SYS => "",
            _ => ""
        };
    }

    private static string GetDisplayName(string? sensorName, SensorGroup sensorGroup)
    {
        // If we have a sensor name, convert it to PascalCase display name
        // Example: "temperature.sensor" -> "TemperatureSensor"
        if (!string.IsNullOrEmpty(sensorName))
        {
            var parts = sensorName.Split('.');
            var displayParts = parts.Select(part =>
                char.ToUpper(part[0]) + part.Substring(1).ToLower());
            return string.Join("", displayParts);
        }

        return $"[{sensorGroup}] UnknownSensor";
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return "null";

        // Try to format as double with 2 decimal places
        if (value is double d)
            return d.ToString("F2");
        if (value is float f)
            return f.ToString("F2");
        if (value is decimal dec)
            return dec.ToString("F2");

        // Try to convert to double
        if (double.TryParse(value.ToString(), out var dblValue))
            return dblValue.ToString("F2");

        return value.ToString() ?? "null";
    }

    public Task<bool> ReportHealthAsync(string deviceId, DeviceHealth health, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Report health (simulated): DeviceId={DeviceId}, IsHealthy={IsHealthy}, CpuUsage={CpuUsage}%, MemoryUsage={MemoryUsage}%, ErrorCount={ErrorCount}",
            deviceId,
            health.IsHealthy,
            health.CpuUsage,
            health.MemoryUsage,
            health.ErrorCount);

        if (!string.IsNullOrEmpty(health.LastError))
        {
            _logger.LogWarning(
                "Last error: {LastError}",
                health.LastError);
        }

        return Task.FromResult(true);
    }

    public Task<IDisposable> SubscribeConfigurationUpdatesAsync(
        string deviceId,
        Func<UpdateConfigurationEvent, Task> handler,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Subscribe to configuration updates (simulated): DeviceId={DeviceId}",
            deviceId);

        // Return a no-op disposable
        return Task.FromResult<IDisposable>(new NoOpDisposable());
    }

    public Task<IDisposable> SubscribeCommandsAsync(
        string deviceId,
        Func<ExecuteCommandEvent, Task> handler,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Subscribe to commands (simulated): DeviceId={DeviceId}",
            deviceId);

        // Return a no-op disposable
        return Task.FromResult<IDisposable>(new NoOpDisposable());
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogInformation("Disposing");
        _isConnected = false;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
