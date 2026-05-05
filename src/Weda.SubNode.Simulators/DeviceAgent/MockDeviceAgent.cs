using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NATS.Client.Services;

using NATS.Net;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

namespace Weda.SubNode.Simulators.DeviceAgent;

/// <summary>
/// Mock Device Agent that registers as a NATS micro-service
/// and handles registration requests via request-reply pattern.
/// Simulates the cloud-side Device Agent service for local development and testing.
///
/// Usage:
///   services.Configure&lt;MockDeviceAgentOptions&gt;(o => o.NatsUrl = "nats://localhost:4224");
///   services.AddHostedService&lt;MockDeviceAgent&gt;();
///
/// Subject: eco1j.weda.dm.reg.req
/// </summary>
public class MockDeviceAgent : IHostedService, IAsyncDisposable
{
    private readonly ILogger<MockDeviceAgent> _logger;
    private readonly NatsClient _client;
    private readonly MockDeviceAgentOptions _options;
    private INatsSvcServer? _service;

    /// <summary>
    /// Tracks registered devices: DeviceName → DeviceId
    /// </summary>
    private readonly Dictionary<string, string> _registeredDevices = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private const string RegisterDeviceSubject = "eco1j.weda.dm.reg.req";
    private const string UploadConfigSubject = "eco1j.weda.dm.cfg.update.req";

    public MockDeviceAgent(
        IOptions<MockDeviceAgentOptions>? options = null,
        ILogger<MockDeviceAgent>? logger = null)
    {
        _options = options?.Value ?? new MockDeviceAgentOptions();
        _client = new NatsClient(_options.NatsUrl);
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<MockDeviceAgent>();
    }

    /// <summary>
    /// Gets all registered devices.
    /// </summary>
    public IReadOnlyDictionary<string, string> RegisteredDevices
    {
        get { lock (_lock) return new Dictionary<string, string>(_registeredDevices); }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "MockDeviceAgent starting: NatsUrl='{NatsUrl}', DeviceIdPrefix='{Prefix}'",
            _options.NatsUrl, _options.DeviceIdPrefix);

        var svc = _client.CreateServicesContext();
        _service = await svc.AddServiceAsync("MockDeviceAgent", "1.0.0", cancellationToken: cancellationToken);

        // Register device endpoint: eco1j.weda.dm.reg.req
        await _service.AddEndpointAsync<DeviceRegistrationRequest>(
            name: "RegisterDevice",
            subject: RegisterDeviceSubject,
            handler: HandleRegistrationAsync,
            cancellationToken: cancellationToken);

        // Upload config endpoint: eco1j.weda.dm.cfg.update.req
        await _service.AddEndpointAsync<ConfigurationUploadRequest>(
            name: "UploadConfig",
            subject: UploadConfigSubject,
            handler: HandleConfigUploadAsync,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "MockDeviceAgent started. Listening on '{RegSubject}' and '{CfgSubject}'",
            RegisterDeviceSubject, UploadConfigSubject);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MockDeviceAgent stopping");

        if (_service != null)
        {
            await _service.DisposeAsync();
            _service = null;
        }

        _logger.LogInformation("MockDeviceAgent stopped. Total registered devices: {Count}", _registeredDevices.Count);
    }

    private async ValueTask HandleRegistrationAsync(NatsSvcMsg<DeviceRegistrationRequest> msg)
    {
        try
        {
            var request = msg.Data;
            var deviceName = request?.Data?.DeviceName ?? "unknown";
            var requestDeviceId = request?.Data?.DeviceId;

            _logger.LogInformation(
                "Registration request received: DeviceName={DeviceName}, RequestDeviceId={DeviceId}",
                deviceName, requestDeviceId);

            var deviceId = ResolveDeviceId(deviceName, requestDeviceId);
            var response = BuildRegistrationResponse(request!, deviceName, deviceId);

            await msg.ReplyAsync(response);

            _logger.LogInformation(
                "Registration response sent: DeviceName={DeviceName}, DeviceId={DeviceId}",
                deviceName, deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing registration request");
            await msg.ReplyErrorAsync(500, ex.Message);
        }
    }

    private async ValueTask HandleConfigUploadAsync(NatsSvcMsg<ConfigurationUploadRequest> msg)
    {
        try
        {
            var request = msg.Data;
            _logger.LogInformation("Config upload request received: ReqSeqId={ReqSeqId}", request?.ReqSeqId);

            var response = new ConfigurationUploadResponse
            {
                ReqSeqId = request?.ReqSeqId ?? "",
                Code = 0,
                Message = "OK",
                Data = new ConfigurationUploadResponseData
                {
                    ConfigurationStatus = "accepted"
                }
            };

            await msg.ReplyAsync(response);

            _logger.LogInformation("Config upload response sent: Status=accepted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing config upload request");
            await msg.ReplyErrorAsync(500, ex.Message);
        }
    }

    private string ResolveDeviceId(string deviceName, string? requestDeviceId)
    {
        lock (_lock)
        {
            if (_registeredDevices.TryGetValue(deviceName, out var existingId))
            {
                _logger.LogDebug("Device '{DeviceName}' already registered: {DeviceId}", deviceName, existingId);
                return existingId;
            }

            var deviceId = !string.IsNullOrEmpty(requestDeviceId)
                ? requestDeviceId
                : $"{_options.DeviceIdPrefix}{Guid.NewGuid():N}"[..36];

            _registeredDevices[deviceName] = deviceId;
            return deviceId;
        }
    }

    private DeviceRegistrationResponse BuildRegistrationResponse(
        DeviceRegistrationRequest request,
        string deviceName,
        string deviceId)
    {
        var topicPrefix = $"{_options.TopicPrefix}.{deviceId}";

        return new DeviceRegistrationResponse
        {
            ReqSeqId = request.ReqSeqId,
            Code = 0,
            Message = "OK",
            Data = new DeviceRegistrationResponseData
            {
                DeviceName = deviceName,
                DeviceId = deviceId,
                RegistrationStatus = "accepted",
                NatsTopicAssignments = new NatsTopicAssignments
                {
                    TelemetryTopic = $"{topicPrefix}.dm.dt.update.rl",
                    BatchTelemetryTopic = $"{topicPrefix}.dm.dt.batch.rl",
                    HealthTopic = $"{topicPrefix}.dm.health.update.rl",
                    EventTopic = $"{topicPrefix}.dm.event.rl",
                    CommandTopic = $"{topicPrefix}.dm.cmd.req",
                    CommandResponseTopic = $"{topicPrefix}.dm.cmd.resp",
                    SystemConfigDesiredTopic = $"{topicPrefix}.dm.systemcfg.desired",
                    SystemConfigReportedTopic = $"{topicPrefix}.dm.systemcfg.reported",
                    DeviceConfigDesiredTopic = $"{topicPrefix}.dm.devicecfg.desired",
                    DeviceConfigReportedTopic = $"{topicPrefix}.dm.devicecfg.reported",
                    CustomConfigDesiredTopic = $"{topicPrefix}.dm.customcfg.desired",
                    CustomConfigReportedTopic = $"{topicPrefix}.dm.customcfg.reported"
                }
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        GC.SuppressFinalize(this);
    }
}
