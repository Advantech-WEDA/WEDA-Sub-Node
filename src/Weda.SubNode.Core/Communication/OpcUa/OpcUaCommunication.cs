using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Communication.Common;

namespace Weda.SubNode.Core.Communication.OpcUa;

/// <summary>
/// Static factory for creating OPC-UA communication instances.
/// Provides convenient creation methods following the pattern:
/// - OpcUa.Default (localhost:4840, Anonymous, No Security)
/// - OpcUa.Create(endpointUrl, ...)
/// </summary>
public static class OpcUaFactory
{
    /// <summary>
    /// Create an OPC-UA communication instance with default settings (localhost:4840)
    /// </summary>
    public static OpcUaCommunication Default => Create("opc.tcp://localhost:4840");

    /// <summary>
    /// Create an OPC-UA communication instance with specified endpoint URL
    /// </summary>
    public static OpcUaCommunication Create(
        string endpointUrl,
        OpcUaSecurityMode securityMode = OpcUaSecurityMode.None,
        OpcUaAuthType authType = OpcUaAuthType.Anonymous,
        string? username = null,
        string? password = null,
        string? certificatePath = null,
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
    {
        return new OpcUaCommunication(
            endpointUrl, securityMode, authType,
            username, password, certificatePath,
            settings, logger);
    }
}

/// <summary>
/// Extension methods for creating OpcUaCommunication from DeviceConfiguration
/// </summary>
public static class OpcUaCommunicationExtensions
{
    /// <summary>
    /// Create OpcUaCommunication from DeviceConfiguration.DeviceCommunication dictionary
    /// </summary>
    public static OpcUaCommunication CreateOpcUaCommunication(
        this DeviceConfiguration config,
        ConnectionSettings? connectionSettings = null,
        ILogger<CommunicationBase>? logger = null)
    {
        var comm = config.DeviceCommunication;
        var endpointUrl = comm.GetValueOrDefault("EndpointUrl") as string ?? "opc.tcp://localhost:4840";
        var securityModeStr = comm.GetValueOrDefault("SecurityMode") as string ?? "None";
        var authTypeStr = comm.GetValueOrDefault("AuthType") as string ?? "Anonymous";
        var username = comm.GetValueOrDefault("Username") as string;
        var password = comm.GetValueOrDefault("Password") as string;
        var certificatePath = comm.GetValueOrDefault("CertificatePath") as string;

        var securityMode = Enum.Parse<OpcUaSecurityMode>(securityModeStr, ignoreCase: true);
        var authType = Enum.Parse<OpcUaAuthType>(authTypeStr, ignoreCase: true);

        return new OpcUaCommunication(
            endpointUrl, securityMode, authType,
            username, password, certificatePath,
            connectionSettings, logger);
    }
}

/// <summary>
/// OPC-UA communication implementation.
/// Wraps the OPC Foundation SDK Session for connection lifecycle management.
/// Unlike TCP communication, OPC-UA manages its own transport internally,
/// so this class implements ICommunication (not IRequestResponseCommunication).
/// OPC-UA specific methods (ReadNodesAsync, WriteNodesAsync) are exposed directly.
/// </summary>
public class OpcUaCommunication : CommunicationBase
{
    private readonly string _endpointUrl;
    private readonly OpcUaSecurityMode _securityMode;
    private readonly OpcUaAuthType _authType;
    private readonly string? _username;
    private readonly string? _password;
    private readonly string? _certificatePath;
    private ApplicationConfiguration? _appConfig;
    private ISession? _session;

    public OpcUaCommunication(
        string endpointUrl,
        OpcUaSecurityMode securityMode = OpcUaSecurityMode.None,
        OpcUaAuthType authType = OpcUaAuthType.Anonymous,
        string? username = null,
        string? password = null,
        string? certificatePath = null,
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings, logger)
    {
        _endpointUrl = endpointUrl ?? throw new ArgumentNullException(nameof(endpointUrl));
        _securityMode = securityMode;
        _authType = authType;
        _username = username;
        _password = password;
        _certificatePath = certificatePath;
    }

    /// <summary>
    /// Gets the active OPC-UA session (null if not connected)
    /// </summary>
    internal ISession? Session => _session;

    protected override async Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Connecting to OPC-UA server at {EndpointUrl}", _endpointUrl);
            State = CommunicationState.Connecting;

            // Build application configuration
            _appConfig = await BuildApplicationConfigurationAsync();

            // Select endpoint
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(
                _appConfig,
                _endpointUrl,
                useSecurity: _securityMode != OpcUaSecurityMode.None);

            var endpointConfiguration = EndpointConfiguration.Create(_appConfig);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

            // Build user identity
            var userIdentity = CreateUserIdentity();

            // Create session
            var sessionTimeout = (uint)(Settings.ConnectionTimeoutMs > 0 ? Settings.ConnectionTimeoutMs : 60000);
            _session = await Opc.Ua.Client.Session.Create(
                _appConfig,
                endpoint,
                updateBeforeConnect: false,
                sessionName: "WedaSubNode_OpcUa",
                sessionTimeout: sessionTimeout,
                userIdentity,
                preferredLocales: null,
                cancellationToken);

            _session.KeepAlive += OnSessionKeepAlive;

            State = CommunicationState.Connected;
            _logger.LogInformation("Connected to OPC-UA server at {EndpointUrl}", _endpointUrl);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to OPC-UA server at {EndpointUrl}", _endpointUrl);
            State = CommunicationState.Error;
            _session?.Dispose();
            _session = null;
            return false;
        }
    }

    public override async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Disconnecting from OPC-UA server at {EndpointUrl}", _endpointUrl);

            if (_session != null)
            {
                _session.KeepAlive -= OnSessionKeepAlive;
                await _session.CloseAsync(cancellationToken);
                _session.Dispose();
                _session = null;
            }

            State = CommunicationState.Disconnected;
            _logger.LogInformation("Disconnected from OPC-UA server");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from OPC-UA server");
        }
    }

    /// <summary>
    /// Read multiple OPC-UA node values in a single batch call.
    /// </summary>
    /// <param name="nodesToRead">List of ReadValueId specifying nodes and attributes to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DataValueCollection containing the read results</returns>
    public async Task<DataValueCollection> ReadNodesAsync(
        ReadValueIdCollection nodesToRead,
        CancellationToken cancellationToken = default)
    {
        if (_session == null || !IsConnected)
            throw new InvalidOperationException("OPC-UA session is not connected");

        var response = await _session.ReadAsync(
            null,
            maxAge: 0,
            TimestampsToReturn.Both,
            nodesToRead,
            cancellationToken);

        return response.Results;
    }

    /// <summary>
    /// Write values to multiple OPC-UA nodes in a single batch call.
    /// </summary>
    /// <param name="nodesToWrite">Collection of WriteValue specifying nodes and values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>StatusCodeCollection with write results</returns>
    public async Task<StatusCodeCollection> WriteNodesAsync(
        WriteValueCollection nodesToWrite,
        CancellationToken cancellationToken = default)
    {
        if (_session == null || !IsConnected)
            throw new InvalidOperationException("OPC-UA session is not connected");

        var response = await _session.WriteAsync(
            null,
            nodesToWrite,
            cancellationToken);

        return response.Results;
    }

    /// <summary>
    /// Create a subscription on the OPC-UA session.
    /// </summary>
    /// <param name="publishingIntervalMs">Publishing interval in milliseconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created Subscription</returns>
    public async Task<Subscription> CreateSubscriptionAsync(
        int publishingIntervalMs = 1000,
        CancellationToken cancellationToken = default)
    {
        if (_session == null || !IsConnected)
            throw new InvalidOperationException("OPC-UA session is not connected");

        var subscription = new Subscription(_session.DefaultSubscription)
        {
            PublishingInterval = publishingIntervalMs,
            PublishingEnabled = true,
            KeepAliveCount = 10,
            LifetimeCount = 100,
            MaxNotificationsPerPublish = 1000
        };

        _session.AddSubscription(subscription);
        await subscription.CreateAsync(cancellationToken);

        _logger.LogDebug("Created OPC-UA subscription with {IntervalMs}ms publishing interval",
            publishingIntervalMs);

        return subscription;
    }

    /// <summary>
    /// Add a monitored item to an existing subscription.
    /// </summary>
    public MonitoredItem AddMonitoredItem(
        Subscription subscription,
        NodeId nodeId,
        int samplingIntervalMs = 1000,
        EventHandler<MonitoredItemNotificationEventArgs>? callback = null)
    {
        var monitoredItem = new MonitoredItem(subscription.DefaultItem)
        {
            StartNodeId = nodeId,
            AttributeId = Attributes.Value,
            SamplingInterval = samplingIntervalMs,
            QueueSize = 10,
            DiscardOldest = true
        };

        if (callback != null)
        {
            monitoredItem.Notification += (MonitoredItem item, MonitoredItemNotificationEventArgs e)
                => callback(item, e);
        }

        subscription.AddItem(monitoredItem);
        return monitoredItem;
    }

    private async Task<ApplicationConfiguration> BuildApplicationConfigurationAsync()
    {
        var application = new ApplicationInstance
        {
            ApplicationName = "WedaSubNode_OpcUaClient",
            ApplicationType = ApplicationType.Client,
            ConfigSectionName = "WedaSubNode_OpcUaClient"
        };

        var config = new ApplicationConfiguration
        {
            ApplicationName = "WedaSubNode_OpcUaClient",
            ApplicationType = ApplicationType.Client,
            ApplicationUri = Utils.Format("urn:{0}:WedaSubNode:OpcUaClient", System.Net.Dns.GetHostName()),
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "WedaSubNode", "pki", "own"),
                    SubjectName = "CN=WedaSubNode OPC-UA Client"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "WedaSubNode", "pki", "issuer")
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "WedaSubNode", "pki", "trusted")
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "WedaSubNode", "pki", "rejected")
                },
                AutoAcceptUntrustedCertificates = true
            },
            TransportConfigurations = [],
            TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
        };

        await config.Validate(ApplicationType.Client);

        if (_securityMode != OpcUaSecurityMode.None)
        {
            application.ApplicationConfiguration = config;
            var hasAppCertificate = await application.CheckApplicationInstanceCertificatesAsync(silent: true);

            if (!hasAppCertificate)
            {
                _logger.LogWarning("No application certificate found, auto-generating one");
            }
        }

        return config;
    }

    private UserIdentity CreateUserIdentity()
    {
        return _authType switch
        {
            OpcUaAuthType.UserPassword when _username != null =>
                new UserIdentity(_username,
                    System.Text.Encoding.UTF8.GetBytes(_password ?? string.Empty)),
            OpcUaAuthType.Certificate when _certificatePath != null =>
                new UserIdentity(
                    System.Security.Cryptography.X509Certificates.X509CertificateLoader
                        .LoadCertificateFromFile(_certificatePath)),
            _ => new UserIdentity(new AnonymousIdentityToken())
        };
    }

    private void OnSessionKeepAlive(ISession session, KeepAliveEventArgs e)
    {
        if (e.Status != null && ServiceResult.IsNotGood(e.Status))
        {
            _logger.LogWarning("OPC-UA session keep-alive failed: {Status}", e.Status);
            State = CommunicationState.Error;
        }
    }
}

/// <summary>
/// OPC-UA security modes
/// </summary>
public enum OpcUaSecurityMode
{
    None,
    Sign,
    SignAndEncrypt
}

/// <summary>
/// OPC-UA authentication types
/// </summary>
public enum OpcUaAuthType
{
    Anonymous,
    UserPassword,
    Certificate
}
