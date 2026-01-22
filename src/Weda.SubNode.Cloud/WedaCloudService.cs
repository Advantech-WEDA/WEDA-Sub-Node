using System.Text.Json;

using ErrorOr;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NATS.Client.Core;
using NATS.Net;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.Command.Contracts;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;
using Weda.SubNode.Abstractions.Cloud.Nats;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Cloud.Subscriptions;
using Weda.SubNode.Core.Storage;

namespace Weda.SubNode.Cloud;

/// <summary>
/// WedaNode implementation
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

    /// <summary>
    /// Shared JSON serializer options for command deserialization.
    /// </summary>
    private static readonly JsonSerializerOptions CommandJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
            _logger.LogDebug("Already connected to WedaNode");
            return true;
        }

        _logger.LogInformation("Connecting to WedaNode");

        try
        {
            // Ping NATS server to verify connection
            var rtt = await _client.PingAsync(cancellationToken);
            _logger.LogInformation("NATS connection verified - RTT: {RttMs}ms", rtt.TotalMilliseconds);

            var response = await _client.RequestAsync<string, NatsServicePingResponse>("$SRV.PING", "", cancellationToken: cancellationToken);
            var pingResponse = response.Data;
            _logger.LogInformation(
                "NATS service discovered: Name={Name}, Version={Version}, WedaNodeId={DeviceId}, Endpoints={Endpoints}",
                pingResponse?.Name,
                pingResponse?.Version,
                pingResponse?.Metadata?.DeviceId,
                pingResponse?.Metadata?.Endpoints);

            _isConnected = true;
            _logger.LogInformation("Connected to WedaNode");
            return true;
        }
        catch (NatsNoRespondersException)
        {
            _logger.LogError("Failed to NatsNoRespondersException");
            _isConnected = false;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to WedaNode");
            _isConnected = false;
            return false;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            _logger.LogDebug("Already disconnected from WedaNode");
            return;
        }

        _logger.LogInformation("Disconnecting from WedaNode");

        try
        {
            // Dispose NATS client connection
            await _client.DisposeAsync();
            _isConnected = false;
            _logger.LogInformation("Disconnected from WedaNode");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect from WedaNode");
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

    /// <summary>
    /// Communicates with the Device Agent to upload the provided configurations.
    /// </summary>
    /// <param name="configurations">The device configurations to be uploaded.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// An <see cref="ErrorOr{T}"/> where T is <see cref="bool"/>, representing the following states:
    /// <list type="bullet">
    /// <item>
    ///     <term>Success (<c>true</c>)</term>
    ///     <description>Returned when <c>response.IsSuccess</c> is true, indicating the configuration was successfully received.</description>
    /// </item>
    /// <item>
    ///     <term>Error (<c>Device.NotFound</c>)</term>
    ///     <description>Returned when the response code is <c>404</c>, signaling that the device does not exist in the agent's record.</description>
    /// </item>
    /// <item>
    ///     <term>Error (<c>Upload.{Code}</c>)</term>
    ///     <description>Returned as a failure type for any other non-success response codes, containing the specific error code and message.</description>
    /// </item>
    /// </list>
    /// </returns>
    public async Task<ErrorOr<bool>> UploadDeviceConfigurationsAsync(
        DeviceConfigurations configurations,
        CancellationToken cancellationToken = default)
    {
        if (configurations.Count == 0)
        {
            return Error.Validation(
                code: "Configurations.Empty",
                description: "DeviceConfigurations must not empty");
        }

        var subNodeInfo = configurations.Values.First().SubNodeInfo!;
        _logger.LogInformation("Uploading device configuration: DeviceId={DeviceId}, DeviceName={DeviceName}",
            subNodeInfo.DeviceId, subNodeInfo.Name);

        var response = await _deviceAgentClient.UploadDeviceConfigurationAsync(
            configurations,
            cancellationToken);

        _logger.LogInformation(
            "Uploading response: IsSuccess={IsSuccess}, Code={Code} ",
            response.IsSuccess, response.Code);

        if (response.IsSuccess == true)
        {
            return true;
        }

        if (response.Code == 404)
        {
            var error = Error.NotFound(
                code: "Device.NotFound",
                description: $"Device configuration upload failed: NotFound (404). DeviceId={subNodeInfo.DeviceId}, DeviceName={subNodeInfo.Name}");
            _logger.LogError("Device configuration upload failed: {Description}", error.Description);
            return error;
        }

        var failure = Error.Failure(
            code: $"Upload.{response.Code}",
            description: $"Device configuration upload failed: Code={response.Code}, Message={response.Message}");
        _logger.LogError("Device configuration upload failed: Code={Code}, Message={Message}",
            response.Code, response.Message);
        return failure;
    }

    public async Task<bool> SendTelemetryAsync(
        string deviceId,
        TelemetryData telemetryData,
        CancellationToken cancellationToken = default)
    {
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
            var subscriptionInfo = await _subscriptionManager.SubscribeAsync<SubNodeConfigUpdateMessage>(
                topic: configSub.DesiredTopic,
                handler: async (msg, subject) =>
                {
                    _logger.LogInformation(
                        "Received configuration update: Type={Type}, SeqId={SeqId}, Subject={Subject}",
                        configSub.Type.Value,
                        msg.SeqId,
                        subject);

                    // Parse protoVer, groupId, deviceId from subject
                    // Subject format: {protoVer}.{groupId}.{deviceId}.subnode.shadow.{configType}.{action}
                    var subjectInfo = SubjectParser.Parse(subject);
                    if (subjectInfo != null)
                    {
                        msg.ProtoVer = subjectInfo.ProtoVer;
                        msg.GroupId = subjectInfo.GroupId;
                        msg.DeviceId = subjectInfo.DeviceId;
                        _logger.LogDebug(
                            "Parsed subject: ProtoVer={ProtoVer}, GroupId={GroupId}, DeviceId={DeviceId}",
                            subjectInfo.ProtoVer, subjectInfo.GroupId, subjectInfo.DeviceId);
                    }

                    // Skip messages with no actual configuration data (e.g., JetStream replays or acks)
                    if (msg.Data?.Cfg?.Desired == null)
                    {
                        _logger.LogDebug(
                            "Skipping config update with no Desired data: Type={Type}, SeqId={SeqId}",
                            configSub.Type.Value, msg.SeqId);
                        return;
                    }

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
        // Subscribe to NatsCommandMessage (envelope) and extract DeviceCommand from data
        var subscriptionInfo = await _subscriptionManager.SubscribeAsync<NatsCommandMessage>(
            topic: commandTopic,
            handler: async envelope =>
            {
                _logger.LogDebug(
                    "Received command envelope: Cmd={Cmd}, SeqId={SeqId}, ReqSeqId={ReqSeqId}",
                    envelope.Cmd,
                    envelope.SeqId,
                    envelope.ReqSeqId);

                // Extract DeviceCommand from the envelope's data field
                var deviceCommand = ExtractDeviceCommand(envelope);
                if (deviceCommand is null)
                {
                    _logger.LogWarning("Failed to extract DeviceCommand from envelope");
                    return;
                }

                _logger.LogInformation(
                    "Received command: DeviceCmd={DeviceCmd}, Timeout={Timeout}",
                    deviceCommand.DeviceCmd,
                    deviceCommand.Timeout);

                var commandEvent = new ExecuteCommandEvent(
                    DeviceId: deviceId,
                    Command: deviceCommand,
                    Timestamp: DateTimeOffset.FromUnixTimeMilliseconds(envelope.Timestamp));

                await handler(commandEvent);

                _logger.LogDebug("Command handled successfully: {DeviceCmd}", deviceCommand.DeviceCmd);
            },
            subscriptionType: SubscriptionTypes.Command,
            responseTopic: topicAssignments.CommandResponseTopic,
            cancellationToken: cancellationToken);

        // Return a disposable that unsubscribes when disposed
        return new SubscriptionDisposable(_subscriptionManager, subscriptionInfo.Topic);
    }

    public async Task<bool> PublishConfigurationReportAsync(
        SubscriptionType configType,
        SubNodeConfigUpdateMessage report,
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
            report.Data?.Cfg?.Reported?.DeviceCfg?.Message?.Status ?? "unknown",
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

    public async Task<bool> SendBatchTelemetryAsync(
        string deviceId,
        BatchTelemetrySendMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var topicAssignments = FindTopicsByDeviceId(deviceId);
        if (topicAssignments == null)
        {
            _logger.LogWarning(
                "Cannot send batch telemetry: topics not configured for device {DeviceId}",
                deviceId);
            return false;
        }

        var batchTelemetryTopic = topicAssignments.BatchTelemetryTopic;
        _logger.LogInformation(
            "Sending batch telemetry: DeviceId={DeviceId}, MeasureCount={MeasureCount}, Topic={Topic}",
            deviceId, message.Data.Measures.Count, batchTelemetryTopic);

        try
        {
            await _client.PublishAsync(
                subject: batchTelemetryTopic,
                data: message,
                cancellationToken: cancellationToken);

            _logger.LogDebug(
                "Batch telemetry sent successfully: DeviceId={DeviceId}, MeasureCount={MeasureCount}",
                deviceId, message.Data.Measures.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send batch telemetry: DeviceId={DeviceId}",
                deviceId);
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

    public Task ResetRegistrationAsync(CancellationToken ct = default)
    {
        return _registrationStorage.DeleteRegistrationAsync(ct);
    }

    /// <summary>
    /// Extracts a DeviceCommand from a NatsCommandMessage envelope.
    /// The envelope contains the command data in its Data property as a JsonElement.
    /// </summary>
    private DeviceCommand? ExtractDeviceCommand(NatsCommandMessage envelope)
    {
        if (envelope.Data is null)
        {
            _logger.LogWarning("Command envelope has null data");
            return null;
        }

        try
        {
            // Deserialize the data JsonElement to DeviceCommand (basic fields only)
            var deviceCommand = envelope.Data.Value.Deserialize<DeviceCommand>(CommandJsonOptions);
            if (deviceCommand is null)
            {
                _logger.LogWarning("Failed to deserialize command data to DeviceCommand");
                return null;
            }

            // Store the raw JSON data for CommandRegistry to deserialize to specific command types
            deviceCommand.RawData = envelope.Data;

            // Also populate Parameters dictionary for backward compatibility with device protocol parsers
            // This extracts all properties as Dictionary<string, object> for easy access
            var allProperties = envelope.Data.Value.Deserialize<Dictionary<string, object>>(CommandJsonOptions);
            if (allProperties != null)
            {
                // Remove known DeviceCommand properties, keep only the extra ones
                allProperties.Remove("deviceCmd");
                allProperties.Remove("timeout");
                allProperties.Remove("respTopic");
                deviceCommand.Parameters = allProperties;
            }

            return deviceCommand;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse command data JSON");
            return null;
        }
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
