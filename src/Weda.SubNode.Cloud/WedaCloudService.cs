using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private NatsTopicAssignments? _topicAssignments;

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
            "Configuring NATS topic assignments: TelemetryTopic={TelemetryTopic}, BatchTelemetryTopic={BatchTelemetryTopic}, HealthTopic={HealthTopic}, CommandTopic={CommandTopic}",
            topicAssignments.TelemetryTopic,
            topicAssignments.BatchTelemetryTopic,
            topicAssignments.HealthTopic,
            topicAssignments.CommandTopic);

        _topicAssignments = topicAssignments;
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

    public async Task<string?> GetOrRegisterDeviceIdAsync(
        DeviceInfo info,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting or registering device ID: DeviceName={DeviceName}", info.DeviceName);

        // 1. Try to get existing registration from storage (includes NATS topics)
        try
        {
            var existingRegistration = await _registrationStorage.GetRegistrationAsync(cancellationToken);
            if (existingRegistration != null && !string.IsNullOrEmpty(existingRegistration.DeviceId))
            {
                _logger.LogInformation("Found existing device registration: DeviceId={DeviceId}", existingRegistration.DeviceId);

                info.DeviceId = existingRegistration.DeviceId;
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
        ArgumentNullException.ThrowIfNull(handler);

        if (_topicAssignments == null)
        {
            throw new InvalidOperationException(
                "Topic assignments not configured. Call ConfigureTopics() before subscribing to configuration updates.");
        }

        var configUpdateTopic = _topicAssignments.ConfigUpdateTopic;
        _logger.LogInformation(
            "Subscribing to configuration updates: DeviceId={DeviceId}, Topic={Topic}",
            deviceId,
            configUpdateTopic);

        // Create a disposable wrapper with its own cancellation token
        var disposableSubscription = new NatsSubscriptionDisposable();

        // Subscribe to NATS config update topic using pub/sub pattern
        var subscription = _client.SubscribeAsync<SubNodeConfigurationUpdateMessage>(
            subject: configUpdateTopic,
            cancellationToken: disposableSubscription.Token);

        // Start background task to process configuration updates
        _ = Task.Run(async () =>
        {
            _logger.LogInformation("Configuration update subscription task started for device: {DeviceId}", deviceId);

            try
            {
                await foreach (var msg in subscription.WithCancellation(disposableSubscription.Token))
                {
                    try
                    {
                        if (msg.Data == null)
                        {
                            _logger.LogWarning("Received null configuration update data from topic: {Topic}", configUpdateTopic);
                            continue;
                        }

                        _logger.LogInformation(
                            "Received configuration update: DeviceId={DeviceId}, Cmd={Cmd}, SeqId={SeqId}",
                            msg.Data.DeviceId,
                            msg.Data.Cmd,
                            msg.Data.SeqId);

                        // Create UpdateConfigurationEvent with strongly-typed message
                        var configEvent = new UpdateConfigurationEvent(
                            DeviceId: deviceId,
                            Message: msg.Data,
                            Timestamp: DateTimeOffset.UtcNow);

                        // Invoke handler
                        await handler(configEvent);

                        _logger.LogDebug("Configuration update handled successfully: SeqId={SeqId}", msg.Data.SeqId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error processing configuration update for device: {DeviceId}",
                            deviceId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Configuration update subscription cancelled for device: {DeviceId}", deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Configuration update subscription error for device: {DeviceId}", deviceId);
            }

            _logger.LogInformation("Configuration update subscription task ended for device: {DeviceId}", deviceId);
        }, disposableSubscription.Token);

        // Return the disposable subscription wrapper
        return Task.FromResult<IDisposable>(disposableSubscription);
    }

    public Task<IDisposable> SubscribeCommandsAsync(
        string deviceId,
        Func<ExecuteCommandEvent, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (_topicAssignments == null)
        {
            throw new InvalidOperationException(
                "Topic assignments not configured. Call ConfigureTopics() before subscribing to commands.");
        }

        var commandTopic = _topicAssignments.CommandTopic;
        _logger.LogInformation(
            "Subscribing to commands: DeviceId={DeviceId}, Topic={Topic}",
            deviceId,
            commandTopic);

        // Create a disposable wrapper with its own cancellation token
        var disposableSubscription = new NatsSubscriptionDisposable();

        // Subscribe to NATS command topic using pub/sub pattern
        var subscription = _client.SubscribeAsync<DeviceCommand>(
            subject: commandTopic,
            cancellationToken: disposableSubscription.Token);

        // Start background task to process commands
        _ = Task.Run(async () =>
        {
            _logger.LogInformation("Command subscription task started for device: {DeviceId}", deviceId);

            try
            {
                await foreach (var msg in subscription.WithCancellation(disposableSubscription.Token))
                {
                    try
                    {
                        if (msg.Data == null)
                        {
                            _logger.LogWarning("Received null command data from topic: {Topic}", commandTopic);
                            continue;
                        }

                        _logger.LogInformation(
                            "Received command: DeviceCmd={DeviceCmd}, Timeout={Timeout}",
                            msg.Data.DeviceCmd,
                            msg.Data.Timeout);

                        // Create ExecuteCommandEvent
                        var commandEvent = new ExecuteCommandEvent(
                            DeviceId: deviceId,
                            Command: msg.Data,
                            Timestamp: DateTimeOffset.UtcNow);

                        // Invoke handler
                        await handler(commandEvent);

                        _logger.LogDebug("Command handled successfully: {DeviceCmd}", msg.Data.DeviceCmd);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error processing command for device: {DeviceId}",
                            deviceId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Command subscription cancelled for device: {DeviceId}", deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Command subscription error for device: {DeviceId}", deviceId);
            }

            _logger.LogInformation("Command subscription task ended for device: {DeviceId}", deviceId);
        }, disposableSubscription.Token);

        // Return the disposable subscription wrapper
        return Task.FromResult<IDisposable>(disposableSubscription);
    }

    public async Task<bool> PublishConfigurationReportAsync(
        SubNodeConfigurationUpdateMessage report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (_topicAssignments == null)
        {
            throw new InvalidOperationException(
                "Topic assignments not configured. Call ConfigureTopics() before publishing configuration reports.");
        }

        var configResponseTopic = _topicAssignments.ConfigResponseTopic;
        _logger.LogInformation(
            "Publishing configuration report: DeviceId={DeviceId}, Status={Status}, Topic={Topic}",
            report.DeviceId,
            report.Data?.Cfg?.Reported?.Status ?? "unknown",
            configResponseTopic);

        try
        {
            await _client.PublishAsync(
                subject: configResponseTopic,
                data: report,
                cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Configuration report published successfully: DeviceId={DeviceId}",
                report.DeviceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish configuration report: DeviceId={DeviceId}",
                report.DeviceId);
            return false;
        }
    }

    /// <summary>
    /// Simple disposable wrapper for NATS subscription
    /// </summary>
    private class NatsSubscriptionDisposable : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();

        public CancellationToken Token => _cts.Token;

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
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
