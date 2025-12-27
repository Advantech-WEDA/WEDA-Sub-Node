using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Net;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Cloud.Subscriptions;
using Weda.SubNode.Core.Storage;

namespace Weda.SubNode.Cloud;

/// <summary>
/// Weda Cloud Service implementation
/// Application service layer that coordinates Client layer operations
/// Delegates protocol-specific communication to DeviceAgentClient and TelemetryClient
/// In SubNode architecture, only one SubNode registration is stored with a single DeviceId
/// and corresponding NATS topic assignments.
/// Sealed to prevent inheritance and ensure template method pattern integrity
/// </summary>
public sealed class WedaCloudService : IWedaCloudService
{
    private readonly ILogger<WedaCloudService> _logger;
    private readonly NatsClient _client;
    private readonly IDeviceAgentClient _deviceAgentClient;
    private readonly ITelemetryClient _telemetryClient;
    private readonly IDeviceRegistrationStorage _registrationStorage;
    private readonly CloudSubscriptionManager _subscriptionManager;

    /// <summary>
    /// Single topic assignment for the SubNode (one SubNode = one DeviceId = one set of topics)
    /// </summary>
    private NatsTopicAssignments? _topicAssignments;

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
        _subscriptionManager = new CloudSubscriptionManager(
            client,
            logger != null
                ? NullLoggerFactory.Instance.CreateLogger<CloudSubscriptionManager>()
                : null);
    }

    /// <inheritdoc />
    public ISubscriptionManager Subscriptions => _subscriptionManager;

    public bool IsConnected => _isConnected;

    public void ConfigureTopics(string deviceName, NatsTopicAssignments topicAssignments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentNullException.ThrowIfNull(topicAssignments);

        _logger.LogInformation(
            "Configuring NATS topic assignments for SubNode: TelemetryTopic={TelemetryTopic}, HealthTopic={HealthTopic}, CommandTopic={CommandTopic}",
            topicAssignments.TelemetryTopic,
            topicAssignments.HealthTopic,
            topicAssignments.CommandTopic);

        _topicAssignments = topicAssignments;

        // Configure telemetry client with the SubNode's topics
        _telemetryClient.ConfigureTopics(deviceName, topicAssignments);
    }

    public NatsTopicAssignments? GetTopics(string deviceName)
    {
        // In SubNode architecture, there's only one set of topics for the entire SubNode
        return _topicAssignments;
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
        var deviceName = info.DeviceName;
        _logger.LogInformation("Getting or registering SubNode: DeviceName={DeviceName}", deviceName);

        // 1. Try to get existing SubNode registration from storage (includes NATS topics)
        try
        {
            var existingRegistration = await _registrationStorage.GetRegistrationAsync(cancellationToken);
            if (existingRegistration != null && !string.IsNullOrEmpty(existingRegistration.DeviceId))
            {
                _logger.LogInformation(
                    "Found existing SubNode registration: DeviceName={DeviceName}, DeviceId={DeviceId}",
                    deviceName, existingRegistration.DeviceId);

                info.DeviceId = existingRegistration.DeviceId;

                // Configure topics from stored registration
                ConfigureTopics(deviceName, existingRegistration.NatsTopicAssignments);

                return existingRegistration.DeviceId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to read SubNode registration from storage, falling back to Cloud registration");
            // Fall through to Cloud registration
        }

        // 2. SubNode not in storage or read failed, register with Cloud
        _logger.LogInformation("Registering SubNode {DeviceName} with Cloud", deviceName);

        var response = await _deviceAgentClient.RegisterDeviceAsync(
            info,
            cancellationToken);

        if (response.Code != 0 || response.Data?.DeviceId == null)
        {
            _logger.LogError(
                "Failed to register SubNode {DeviceName} with Cloud: Code={Code}, Message={Message}",
                deviceName, response.Code, response.Message);
            return null;
        }

        var deviceId = response.Data.DeviceId;
        _logger.LogInformation(
            "SubNode registered with Cloud: DeviceName={DeviceName}, DeviceId={DeviceId}, Status={Status}",
            deviceName, deviceId, response.Data.RegistrationStatus);

        // 3. Configure NATS topics from registration response
        ConfigureTopics(deviceName, response.Data.NatsTopicAssignments);

        // 4. Try to save SubNode registration data to storage for future use
        try
        {
            await _registrationStorage.SaveRegistrationAsync(response.Data, cancellationToken);
            _logger.LogInformation(
                "SubNode registration saved to storage (includes NATS topic assignments)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to save SubNode registration to storage (will re-register on restart)");
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

    public async Task<IDisposable> SubscribeConfigurationUpdatesAsync(
        string deviceId,
        Func<UpdateConfigurationEvent, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        // Find the device's topic assignments by deviceId
        var topicAssignments = FindTopicsByDeviceId(deviceId);
        if (topicAssignments == null)
        {
            throw new InvalidOperationException(
                $"Topic assignments not configured for device {deviceId}. " +
                "Call ConfigureTopics() before subscribing to configuration updates.");
        }

        // Get all configured config subscriptions (system, device, custom)
        var configSubscriptions = topicAssignments.GetConfigSubscriptions().ToList();
        if (configSubscriptions.Count == 0)
        {
            throw new InvalidOperationException(
                $"No config subscriptions configured for device {deviceId}.");
        }

        _logger.LogInformation(
            "Subscribing to {Count} configuration topics: DeviceId={DeviceId}, Types=[{Types}]",
            configSubscriptions.Count,
            deviceId,
            string.Join(", ", configSubscriptions.Select(s => s.Type.Value)));

        // Subscribe to all config topics
        var disposables = new List<IDisposable>();
        foreach (var configSub in configSubscriptions)
        {
            var subscriptionInfo = await _subscriptionManager.SubscribeAsync<SubNodeConfigurationUpdateMessage>(
                topic: configSub.DesiredTopic,
                handler: async msg =>
                {
                    _logger.LogInformation(
                        "Received configuration update: Type={Type}, DeviceId={DeviceId}, Cmd={Cmd}, SeqId={SeqId}",
                        configSub.Type.Value,
                        msg.DeviceId,
                        msg.Cmd,
                        msg.SeqId);

                    var configEvent = new UpdateConfigurationEvent(
                        DeviceId: deviceId,
                        ConfigType: configSub.Type,
                        Message: msg,
                        Timestamp: DateTimeOffset.UtcNow);

                    await handler(configEvent);

                    _logger.LogDebug("Configuration update handled successfully: Type={Type}, SeqId={SeqId}",
                        configSub.Type.Value, msg.SeqId);
                },
                subscriptionType: configSub.Type,
                responseTopic: configSub.ReportedTopic,
                cancellationToken: cancellationToken);

            disposables.Add(new SubscriptionDisposable(_subscriptionManager, subscriptionInfo.Topic));
        }

        // Return a composite disposable that unsubscribes all
        return new CompositeDisposable(disposables);
    }

    public async Task<IDisposable> SubscribeCommandsAsync(
        string deviceId,
        Func<ExecuteCommandEvent, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        // Find the device's topic assignments by deviceId
        var topicAssignments = FindTopicsByDeviceId(deviceId);
        if (topicAssignments == null)
        {
            throw new InvalidOperationException(
                $"Topic assignments not configured for device {deviceId}. " +
                "Call ConfigureTopics() before subscribing to commands.");
        }

        var commandTopic = topicAssignments.CommandTopic;
        _logger.LogInformation(
            "Subscribing to commands: DeviceId={DeviceId}, Topic={Topic}",
            deviceId,
            commandTopic);

        // Use subscription manager for the actual subscription
        var subscriptionInfo = await _subscriptionManager.SubscribeAsync<DeviceCommand>(
            topic: commandTopic,
            handler: async msg =>
            {
                _logger.LogInformation(
                    "Received command: DeviceCmd={DeviceCmd}, Timeout={Timeout}",
                    msg.DeviceCmd,
                    msg.Timeout);

                var commandEvent = new ExecuteCommandEvent(
                    DeviceId: deviceId,
                    Command: msg,
                    Timestamp: DateTimeOffset.UtcNow);

                await handler(commandEvent);

                _logger.LogDebug("Command handled successfully: {DeviceCmd}", msg.DeviceCmd);
            },
            subscriptionType: SubscriptionTypes.Command,
            responseTopic: topicAssignments.CommandResponseTopic,
            cancellationToken: cancellationToken);

        // Return a disposable that unsubscribes when disposed
        return new SubscriptionDisposable(_subscriptionManager, subscriptionInfo.Topic);
    }

    public async Task<bool> PublishConfigurationReportAsync(
        SubscriptionType configType,
        SubNodeConfigurationUpdateMessage report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configType);
        ArgumentNullException.ThrowIfNull(report);

        // Find the device's topic assignments by deviceId in the report
        var topicAssignments = FindTopicsByDeviceId(report.DeviceId);
        if (topicAssignments == null)
        {
            throw new InvalidOperationException(
                $"Topic assignments not configured for device {report.DeviceId}. " +
                "Call ConfigureTopics() before publishing configuration reports.");
        }

        // Get the config subscription for the specified type
        var configSubscription = topicAssignments.GetConfigSubscription(configType);
        if (configSubscription == null || string.IsNullOrEmpty(configSubscription.ReportedTopic))
        {
            throw new InvalidOperationException(
                $"Configuration type '{configType.Value}' is not configured for device {report.DeviceId}.");
        }

        var reportedTopic = configSubscription.ReportedTopic;
        _logger.LogInformation(
            "Publishing configuration report: DeviceId={DeviceId}, Type={ConfigType}, Status={Status}, Topic={Topic}",
            report.DeviceId,
            configType.Value,
            report.Data?.Cfg?.Reported?.Status ?? "unknown",
            reportedTopic);

        try
        {
            await _client.PublishAsync(
                subject: reportedTopic,
                data: report,
                cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Configuration report published successfully: DeviceId={DeviceId}, Type={ConfigType}",
                report.DeviceId,
                configType.Value);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish configuration report: DeviceId={DeviceId}, Type={ConfigType}",
                report.DeviceId,
                configType.Value);
            return false;
        }
    }

    public async Task<bool> SendCommandResponseAsync(
        string responseTopic,
        CommandResponse response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (string.IsNullOrEmpty(responseTopic))
        {
            _logger.LogWarning(
                "Command response topic is empty, skipping response: DeviceId={DeviceId}, Command={Command}, Status={Status}",
                response.DeviceId, response.Command, response.Status);
            return false;
        }

        _logger.LogInformation(
            "Sending command response: DeviceId={DeviceId}, Command={Command}, Status={Status}, Topic={Topic}",
            response.DeviceId, response.Command, response.Status, responseTopic);

        try
        {
            await _client.PublishAsync(
                subject: responseTopic,
                data: response,
                cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Command response sent successfully: DeviceId={DeviceId}, Command={Command}, Status={Status}",
                response.DeviceId, response.Command, response.Status);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send command response: DeviceId={DeviceId}, Command={Command}",
                response.DeviceId, response.Command);
            return false;
        }
    }

    /// <summary>
    /// Get topic assignments for the SubNode.
    /// In SubNode architecture, there's only one DeviceId and one set of topics.
    /// Returns null if topics haven't been configured yet.
    /// </summary>
    private NatsTopicAssignments? FindTopicsByDeviceId(string deviceId)
    {
        if (_topicAssignments == null)
        {
            _logger.LogWarning(
                "Topic assignments not configured for SubNode. DeviceId={DeviceId}",
                deviceId);
        }

        return _topicAssignments;
    }

    /// <summary>
    /// Disposable wrapper that unsubscribes from topic when disposed
    /// </summary>
    private sealed class SubscriptionDisposable : IDisposable
    {
        private readonly ISubscriptionManager _manager;
        private readonly string _topic;
        private bool _disposed;

        public SubscriptionDisposable(ISubscriptionManager manager, string topic)
        {
            _manager = manager;
            _topic = topic;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _manager.UnsubscribeAsync(_topic).GetAwaiter().GetResult();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogInformation("Disposing WedaCloudService");

        // Dispose subscription manager first
        _subscriptionManager.DisposeAsync().AsTask().GetAwaiter().GetResult();

        DisconnectAsync().GetAwaiter().GetResult();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Composite disposable that disposes multiple disposables
    /// </summary>
    private sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _disposables;
        private bool _disposed;

        public CompositeDisposable(IEnumerable<IDisposable> disposables)
        {
            _disposables = disposables.ToList();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }
    }
}
