using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Net;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Storage;

namespace Weda.SubNode.Cloud;

/// <summary>
/// Weda Cloud Service implementation
/// Application service layer that coordinates Client layer operations
/// Delegates protocol-specific communication to DeviceAgentClient and TelemetryClient
/// Sealed to prevent inheritance and ensure template method pattern integrity
/// </summary>
public sealed class WedaCloudService : IWedaCloudService
{
    private readonly ILogger<WedaCloudService> _logger;
    private readonly NatsClient _client;
    private readonly IDeviceAgentClient _deviceAgentClient;
    private readonly ITelemetryClient _telemetryClient;
    private readonly IDeviceRegistrationStorage _registrationStorage;
    private bool _isConnected;
    private bool _disposed;

    public WedaCloudService(
        NatsClient client,
        IDeviceAgentClient deviceAgentClient,
        ITelemetryClient telemetryClient,
        IDeviceRegistrationStorage? registrationStorage = null,
        ILogger<WedaCloudService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _deviceAgentClient = deviceAgentClient ?? throw new ArgumentNullException(nameof(deviceAgentClient));
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        _registrationStorage = registrationStorage ?? new JsonDeviceRegistrationStorage();
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<WedaCloudService>();
    }

    public bool IsConnected => _isConnected;

    public void ConfigureTopics(NatsTopicAssignments topicAssignments)
    {
        ArgumentNullException.ThrowIfNull(topicAssignments);

        _logger.LogInformation(
            "Configuring NATS topic assignments: TelemetryTopic={TelemetryTopic}, BatchTelemetryTopic={BatchTelemetryTopic}, HealthTopic={HealthTopic}",
            topicAssignments.TelemetryTopic,
            topicAssignments.BatchTelemetryTopic,
            topicAssignments.HealthTopic);

        _telemetryClient.ConfigureTopics(topicAssignments);
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected)
        {
            _logger.LogDebug("Already connected to Weda Cloud Service");
            return true;
        }

        _logger.LogInformation("Connecting to Weda Cloud Service");

        try
        {
            // Ping NATS server to verify connection
            var rtt = await _client.PingAsync(cancellationToken);
            _logger.LogInformation("NATS connection verified - RTT: {RttMs}ms", rtt.TotalMilliseconds);

            _isConnected = true;
            _logger.LogInformation("Connected to Weda Cloud Service");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Weda Cloud Service");
            _isConnected = false;
            return false;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            _logger.LogDebug("Already disconnected from Weda Cloud Service");
            return;
        }

        _logger.LogInformation("Disconnecting from Weda Cloud Service");

        try
        {
            // Dispose NATS client connection
            await _client.DisposeAsync();
            _isConnected = false;
            _logger.LogInformation("Disconnected from Weda Cloud Service");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect from Weda Cloud Service");
            _isConnected = false;
        }
    }

    [Obsolete("Use DeviceInitializer instead for complete registration flow with NATS topic assignments")]
    public async Task<string?> GetOrRegisterDeviceIdAsync(
        DeviceInfo info,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "GetOrRegisterDeviceIdAsync is deprecated. Use DeviceInitializer for complete registration flow.");
        _logger.LogInformation("Getting or registering device ID: DeviceName={DeviceName}", info.DeviceName);

        // 1. Try to get existing registration from storage (includes NATS topics)
        try
        {
            var existingRegistration = await _registrationStorage.GetRegistrationAsync(cancellationToken);
            if (existingRegistration != null && !string.IsNullOrEmpty(existingRegistration.DeviceId))
            {
                _logger.LogInformation("Found existing device registration: DeviceId={DeviceId}", existingRegistration.DeviceId);

                // Configure NATS topics from stored registration
                ConfigureTopics(existingRegistration.NatsTopicAssignments);

                return existingRegistration.DeviceId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read device registration from storage, falling back to Cloud registration");
            // Fall through to Cloud registration
        }

        // 2. Device not in storage or read failed, register device with Cloud
        _logger.LogInformation("Registering device with Cloud");

        var response = await _deviceAgentClient.RegisterDeviceAsync(
            info,
            cancellationToken);

        if (response.Code != 0 || response.Data?.DeviceId == null)
        {
            _logger.LogError("Failed to register device with Cloud: Code={Code}, Message={Message}",
                response.Code, response.Message);
            return null;
        }

        var deviceId = response.Data.DeviceId;
        _logger.LogInformation("Device registered with Cloud: DeviceId={DeviceId}, Status={Status}",
            deviceId, response.Data.RegistrationStatus);

        // 3. Configure NATS topics from registration response
        ConfigureTopics(response.Data.NatsTopicAssignments);

        // 4. Try to save complete registration data to storage for future use
        try
        {
            await _registrationStorage.SaveRegistrationAsync(response.Data, cancellationToken);
            _logger.LogInformation("Device registration saved to storage (includes NATS topic assignments)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save device registration to storage (device will re-register on restart)");
            // Non-critical failure, continue with deviceId
        }

        return deviceId;
    }

    public async Task<bool> UploadDeviceConfigurationAsync(
        DeviceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Uploading device configuration: DeviceId={DeviceId}, DeviceName={DeviceName}",
            configuration.DeviceId, configuration.DeviceName);

        var response = await _deviceAgentClient.UploadDeviceConfigurationAsync(
            configuration,
            cancellationToken);

        if (response.IsSuccess == true)
        {
            _logger.LogInformation("Device configuration uploaded successfully");
            return true;
        }

        _logger.LogError("Device configuration upload failed: Code={Code}, Message={Message}",
            response.Code, response.Message);
        return false;
    }

    public async Task<DeviceConfiguration?> GetDeviceConfigurationAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting device configuration: DeviceId={DeviceId}", deviceId);

        var configuration = await _deviceAgentClient.GetDeviceConfigurationAsync(
            deviceId,
            cancellationToken);

        return configuration;
    }

    public async Task<bool> SendTelemetryAsync(
        string deviceId,
        TelemetryData telemetryData,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending telemetry: DeviceId={DeviceId}, MeasureCount={MeasureCount}",
            deviceId, telemetryData.Measures.Count);

        var response = await _telemetryClient.SendTelemetryAsync(
            deviceId,
            telemetryData,
            cancellationToken);

        if (response.IsSuccess == true)
        {
            return true;
        }

        _logger.LogError("Telemetry send failed: Code={Code}, Message={Message}",
            response.Code, response.Message);
        return false;
    }

    public async Task<bool> ReportHealthAsync(
        string deviceId,
        DeviceHealth health,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reporting health: DeviceId={DeviceId}, IsHealthy={IsHealthy}",
            deviceId, health.IsHealthy);

        var response = await _telemetryClient.ReportHealthAsync(
            deviceId,
            health,
            cancellationToken);

        if (response.IsSuccess == true)
        {
            return true;
        }

        _logger.LogError("Health report failed: Code={Code}, Message={Message}",
            response.Code, response.Message);
        return false;
    }

    public Task<IDisposable> SubscribeConfigurationUpdatesAsync(
        string deviceId,
        Func<UpdateConfigurationEvent, Task> handler,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Subscribing to configuration updates: DeviceId={DeviceId}", deviceId);

        // TODO: Subscribe via NATS JetStream
        // var subscription = await _jetStreamClient.SubscribeAsync(
        //     subject: $"{groupId}.weda.dm.config.update.{deviceName}",
        //     handler: async (msg) => await handler(ParseEvent(msg)),
        //     cancellationToken: cancellationToken);

        // Return no-op disposable for now
        return Task.FromResult<IDisposable>(new NoOpDisposable());
    }

    public Task<IDisposable> SubscribeCommandsAsync(
        string deviceId,
        Func<ExecuteCommandEvent, Task> handler,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Subscribing to commands: DeviceId={DeviceId}", deviceId);

        // TODO: Subscribe via NATS JetStream
        // var subscription = await _jetStreamClient.SubscribeAsync(
        //     subject: $"{groupId}.weda.dm.command.{deviceName}",
        //     handler: async (msg) => await handler(ParseCommand(msg)),
        //     cancellationToken: cancellationToken);

        // Return no-op disposable for now
        return Task.FromResult<IDisposable>(new NoOpDisposable());
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogInformation("Disposing WedaCloudService");

        DisconnectAsync().GetAwaiter().GetResult();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
