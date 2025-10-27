using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core.Communication;

/// <summary>
/// MQTT communication implementation supporting both ICommunication and IMessageBroker
/// - ICommunication: ReadAsync/WriteAsync for business logic compatibility
/// - IMessageBroker: Subscribe/Publish for native MQTT operations
/// </summary>
public class MqttCommunication : CommunicationBase, IMessageBroker
{
    private readonly string _brokerUrl;
    private readonly int _port;
    private readonly string? _clientId;
    private readonly string? _defaultTopic;

    // MQTTnet client
    private IMqttClient? _mqttClient;
    private readonly MqttFactory _mqttFactory;

    // Internal message queue for ICommunication.ReadAsync compatibility
    private readonly BlockingCollection<byte[]> _messageQueue;
    private readonly int _maxQueueSize;

    // Active subscriptions tracking
    private readonly ConcurrentDictionary<string, bool> _subscriptions;

    public MqttCommunication(
        string brokerUrl,
        int port,
        string? clientId = null,
        string? defaultTopic = null,
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings, logger)
    {
        _brokerUrl = brokerUrl ?? throw new ArgumentNullException(nameof(brokerUrl));
        _port = port;
        _clientId = clientId ?? $"mqtt-client-{Guid.NewGuid():N}";
        _defaultTopic = defaultTopic;
        _maxQueueSize = 100; // Default queue size
        _messageQueue = new BlockingCollection<byte[]>(_maxQueueSize);
        _subscriptions = new ConcurrentDictionary<string, bool>();
        _mqttFactory = new MqttFactory();
    }

    // ===== IMessageBroker Implementation (Native Pub/Sub) =====

    /// <summary>
    /// Subscribe to MQTT topic
    /// </summary>
    public virtual async Task<bool> SubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected)
        {
            _logger?.LogWarning("Cannot subscribe to topic {Topic}: MQTT client not connected", topic);
            return false;
        }

        try
        {
            var subscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(f => f.WithTopic(topic))
                .Build();

            await _mqttClient.SubscribeAsync(subscribeOptions, cancellationToken);
            _subscriptions.TryAdd(topic, true);

            _logger?.LogInformation("Subscribed to MQTT topic: {Topic}", topic);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to subscribe to topic {Topic}", topic);
            return false;
        }
    }

    /// <summary>
    /// Unsubscribe from MQTT topic
    /// </summary>
    public virtual async Task<bool> UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected)
        {
            return false;
        }

        try
        {
            var unsubscribeOptions = _mqttFactory.CreateUnsubscribeOptionsBuilder()
                .WithTopicFilter(topic)
                .Build();

            await _mqttClient.UnsubscribeAsync(unsubscribeOptions, cancellationToken);
            _subscriptions.TryRemove(topic, out _);

            _logger?.LogInformation("Unsubscribed from MQTT topic: {Topic}", topic);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to unsubscribe from topic {Topic}", topic);
            return false;
        }
    }

    /// <summary>
    /// Publish message to MQTT topic
    /// </summary>
    public virtual async Task<bool> PublishAsync(string topic, byte[] payload, CancellationToken cancellationToken = default)
    {
        if (_mqttClient == null || !_mqttClient.IsConnected)
        {
            _logger?.LogWarning("Cannot publish to topic {Topic}: MQTT client not connected", topic);
            return false;
        }

        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(message, cancellationToken);
            _logger?.LogDebug("Published message to topic: {Topic}", topic);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to publish to topic {Topic}", topic);
            return false;
        }
    }

    /// <summary>
    /// Event fired when message received on subscribed topics
    /// </summary>
    public event EventHandler<MessageReceivedEvent>? MessageReceived;

    // ===== ICommunication Implementation (Compatibility Layer) =====

    /// <summary>
    /// Connect to MQTT broker
    /// </summary>
    protected override async Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _mqttClient = _mqttFactory.CreateMqttClient();

            // Setup message received handler
            _mqttClient.ApplicationMessageReceivedAsync += OnMqttMessageReceivedAsync;

            // Setup disconnected handler
            _mqttClient.DisconnectedAsync += e =>
            {
                if (e.ClientWasConnected)
                {
                    _logger?.LogWarning("MQTT client disconnected. Reason: {Reason}", e.Reason);

                    // Notify state change
                    OnStateChanged(CommunicationState.Connected, CommunicationState.Disconnected, "Connection lost");
                }
                return Task.CompletedTask;
            };

            // Build connection options
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_brokerUrl, _port)
                .WithClientId(_clientId)
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                .Build();

            // Connect
            var result = await _mqttClient.ConnectAsync(options, cancellationToken);

            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                _logger?.LogInformation("Connected to MQTT broker {Broker}:{Port} with client ID {ClientId}",
                    _brokerUrl, _port, _clientId);
                return true;
            }
            else
            {
                _logger?.LogError("Failed to connect to MQTT broker. Result code: {ResultCode}",
                    result.ResultCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception while connecting to MQTT broker {Broker}:{Port}",
                _brokerUrl, _port);
            return false;
        }
    }

    /// <summary>
    /// Disconnect from MQTT broker
    /// </summary>
    public override async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_mqttClient != null && _mqttClient.IsConnected)
        {
            try
            {
                await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
                _logger?.LogInformation("Disconnected from MQTT broker");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disconnecting from MQTT broker");
            }
        }
    }

    /// <summary>
    /// Read from internal message queue (blocking until message available)
    /// Provides compatibility with ICommunication interface
    /// </summary>
    public override Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => _messageQueue.Take(cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Publish to default topic (if set)
    /// Provides compatibility with ICommunication interface
    /// </summary>
    public override Task<bool> WriteAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_defaultTopic))
        {
            throw new InvalidOperationException("Default topic not set. Use PublishAsync with explicit topic instead.");
        }

        return PublishAsync(_defaultTopic, data, cancellationToken);
    }

    /// <summary>
    /// Handler for MQTT messages received from MQTTnet client
    /// </summary>
    private Task OnMqttMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.PayloadSegment.ToArray();

            // Add to message queue for ICommunication.ReadAsync compatibility
            if (_messageQueue.Count < _maxQueueSize)
            {
                _messageQueue.TryAdd(payload);
            }
            else
            {
                _logger?.LogWarning("Message queue full, dropping message from topic {Topic}", topic);
            }

            // Trigger MessageReceived event for IMessageBroker
            var messageEvent = new MessageReceivedEvent(
                Topic: topic,
                Payload: payload,
                Timestamp: DateTimeOffset.UtcNow)
            {
                QoS = (int)e.ApplicationMessage.QualityOfServiceLevel,
                Retain = e.ApplicationMessage.Retain
            };

            OnMessageReceived(messageEvent);

            _logger?.LogDebug("Received message from topic {Topic}, payload size: {Size} bytes",
                topic, payload.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling MQTT message");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Trigger MessageReceived event (for testing and internal use)
    /// </summary>
    protected virtual void OnMessageReceived(MessageReceivedEvent e)
    {
        MessageReceived?.Invoke(this, e);
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _messageQueue?.Dispose();
            _mqttClient?.Dispose();
        }
    }
}

/// <summary>
/// Static factory for creating MQTT communication instances
/// </summary>
public static class Mqtt
{
    /// <summary>
    /// Default MQTT communication (localhost:1883)
    /// </summary>
    public static MqttCommunication Default => Create("localhost", 1883);

    /// <summary>
    /// Create MQTT communication with specified broker
    /// </summary>
    public static MqttCommunication Create(
        string brokerUrl,
        int port,
        string? clientId = null,
        string? defaultTopic = null,
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
    {
        return new MqttCommunication(brokerUrl, port, clientId, defaultTopic, settings, logger);
    }
}
