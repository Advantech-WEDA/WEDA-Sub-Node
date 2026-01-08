using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication.Common;

namespace Weda.SubNode.Core.Communication.Mqtt;

/// <summary>
/// MQTT communication implementation using Pub/Sub pattern.
/// Provides subscribe/publish operations for topic-based messaging.
/// </summary>
public class MqttCommunication : PubSubCommunicationBase<byte[]>, IPubSub
{
    private readonly string _brokerUrl;
    private readonly int _port;
    private readonly string? _clientId;

    // MQTTnet client
    private IMqttClient? _mqttClient;
    private readonly MqttFactory _mqttFactory;

    // Active subscriptions tracking
    private readonly ConcurrentDictionary<string, bool> _subscriptions;

    public MqttCommunication(
        string brokerUrl,
        int port,
        string? clientId = null,
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings, logger)
    {
        _brokerUrl = brokerUrl ?? throw new ArgumentNullException(nameof(brokerUrl));
        _port = port;
        _clientId = clientId ?? $"mqtt-client-{Guid.NewGuid():N}";
        _subscriptions = new ConcurrentDictionary<string, bool>();
        _mqttFactory = new MqttFactory();
    }

    // ===== IPubSub Implementation =====

    /// <summary>
    /// Subscribe to MQTT topic
    /// </summary>
    public override async Task<bool> SubscribeAsync(string topic, CancellationToken cancellationToken = default)
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
    public override async Task<bool> UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
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
    public override async Task<bool> PublishAsync(string topic, byte[] payload, CancellationToken cancellationToken = default)
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

    // ===== Connection Management =====

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
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(_brokerUrl, _port)
                .WithClientId(_clientId)
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(60));

            // Apply security settings if configured
            if (Settings.Security != null)
            {
                ApplySecuritySettings(optionsBuilder, Settings.Security);
            }

            var options = optionsBuilder.Build();

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
    /// Handler for MQTT messages received from MQTTnet client
    /// </summary>
    private Task OnMqttMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.PayloadSegment.ToArray();

            // Trigger MessageReceived event using base class helper method
            OnMessageReceived(
                topic: topic,
                message: payload,
                qos: (int)e.ApplicationMessage.QualityOfServiceLevel,
                retain: e.ApplicationMessage.Retain);

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
    /// Apply security settings to MQTT connection options
    /// </summary>
    private void ApplySecuritySettings(MqttClientOptionsBuilder optionsBuilder, SecuritySettings security)
    {
        // Apply username/password authentication
        if (!string.IsNullOrEmpty(security.Username))
        {
            optionsBuilder.WithCredentials(security.Username, security.Password);
            _logger?.LogDebug("MQTT authentication configured with username: {Username}", security.Username);
        }

        // Apply TLS/SSL settings
        if (security.UseTls)
        {
            optionsBuilder.WithTlsOptions(tlsOptionsBuilder =>
            {
                tlsOptionsBuilder.UseTls();
                tlsOptionsBuilder.WithSslProtocols(ConvertTlsVersion(security.TlsVersion));

                // Load client certificate for mTLS if specified
                if (!string.IsNullOrEmpty(security.ClientCertificatePath))
                {
                    try
                    {
                        var clientCert = string.IsNullOrEmpty(security.ClientCertificatePassword)
                            ? X509CertificateLoader.LoadCertificateFromFile(security.ClientCertificatePath)
                            : X509CertificateLoader.LoadPkcs12FromFile(security.ClientCertificatePath, security.ClientCertificatePassword);

                        var certificates = new X509Certificate2Collection { clientCert };
                        tlsOptionsBuilder.WithClientCertificates(certificates);
                        _logger?.LogInformation("MQTT client certificate loaded from: {Path}", security.ClientCertificatePath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to load client certificate from {Path}", security.ClientCertificatePath);
                        throw;
                    }
                }

                // Load CA certificate for server verification if specified
                if (!string.IsNullOrEmpty(security.CaCertificatePath))
                {
                    try
                    {
                        var caCert = X509CertificateLoader.LoadCertificateFromFile(security.CaCertificatePath);

                        tlsOptionsBuilder.WithCertificateValidationHandler(context =>
                        {
                            // Custom validation with CA certificate
                            if (context.Certificate == null)
                                return false;

                            var chain = new X509Chain();
                            chain.ChainPolicy.ExtraStore.Add(caCert);
                            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                            var serverCert = new X509Certificate2(context.Certificate);
                            var isValid = chain.Build(serverCert);

                            if (!isValid)
                            {
                                _logger?.LogWarning("Server certificate validation failed: {Errors}",
                                    string.Join(", ", chain.ChainStatus.Select(s => s.StatusInformation)));
                            }

                            return isValid;
                        });
                        _logger?.LogInformation("MQTT CA certificate loaded from: {Path}", security.CaCertificatePath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to load CA certificate from {Path}", security.CaCertificatePath);
                        throw;
                    }
                }

                // Development/testing options (WARNING: insecure!)
                if (security.AllowUntrustedCertificates || security.IgnoreCertificateHostnameValidation)
                {
                    _logger?.LogWarning("MQTT TLS validation disabled - THIS IS INSECURE AND SHOULD NOT BE USED IN PRODUCTION!");

                    tlsOptionsBuilder.WithCertificateValidationHandler(context =>
                    {
                        if (security.AllowUntrustedCertificates)
                            return true; // Accept any certificate

                        // Perform default validation but ignore hostname mismatch
                        if (security.IgnoreCertificateHostnameValidation && context.Certificate != null)
                        {
                            var chain = new X509Chain();
                            var serverCert = new X509Certificate2(context.Certificate);
                            return chain.Build(serverCert);
                        }

                        return false;
                    });
                }
            });

            _logger?.LogInformation("MQTT TLS/SSL enabled with protocol version: {TlsVersion}", security.TlsVersion);
        }
    }

    /// <summary>
    /// Convert TlsVersion enum to SslProtocols
    /// </summary>
    private static SslProtocols ConvertTlsVersion(TlsVersion version)
    {
        var protocols = SslProtocols.None;

#pragma warning disable CS0618, SYSLIB0039 // Type or member is obsolete
        if (version.HasFlag(TlsVersion.Tls10))
            protocols |= SslProtocols.Tls;
        if (version.HasFlag(TlsVersion.Tls11))
            protocols |= SslProtocols.Tls11;
#pragma warning restore CS0618, SYSLIB0039

        if (version.HasFlag(TlsVersion.Tls12))
            protocols |= SslProtocols.Tls12;
        if (version.HasFlag(TlsVersion.Tls13))
            protocols |= SslProtocols.Tls13;

        return protocols != SslProtocols.None ? protocols : SslProtocols.Tls12 | SslProtocols.Tls13;
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
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
    {
        return new MqttCommunication(brokerUrl, port, clientId, settings, logger);
    }
}
