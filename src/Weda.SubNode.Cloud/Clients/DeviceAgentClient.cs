using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Net;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Mapping;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Cloud.Clients;

/// <summary>
/// Device Agent Client implementation for NATS
/// Handles device registration, sensor validation, and configuration management
/// </summary>
public class DeviceAgentClient : IDeviceAgentClient
{
    private readonly ILogger<DeviceAgentClient> _logger;
    private readonly NatsClient _client;
    private DeviceConfiguration? _deviceConfiguration;

    public DeviceAgentClient(
        NatsClient client,
        ILogger<DeviceAgentClient>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<DeviceAgentClient>();
    }

    private const string RegisterDeviceSubject = "eco1j.weda.dm.reg.req";
    public async Task<DeviceRegistrationResponse> RegisterDeviceAsync(
        DeviceInfo info,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);

        var request = DeviceRegistrationRequest.Create(info);

        _logger.LogInformation(
            "Registering device: DeviceName={DeviceName}, SubNodeType={SubNodeType}, ReqSeqId={ReqSeqId}",
            info.DeviceName,
            info.SubNodeType,
            request.ReqSeqId);

        var response = await _client.RequestAsync<DeviceRegistrationRequest, DeviceRegistrationResponse>(
            subject: RegisterDeviceSubject,
            data: request,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Device registered: DeviceId={DeviceId}, RegistrationStatus={RegistrationStatus}, RspSeqId={RspSeqId}",
            response.Data?.Data?.DeviceId,
            response.Data?.Data?.RegistrationStatus,
            response.Data?.RspSeqId);

        return response.Data!;
    }

    private const string UploadDeviceConfigurationSubject = "eco1j.weda.dm.cfg.update.req";
    public async Task<ConfigurationUploadResponse> UploadDeviceConfigurationAsync(
        DeviceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(configuration.DeviceId);

        // Convert to DTO using mapping extension
        var dto = configuration.ToConfigurationDto();
        var request = ConfigurationUploadRequest.Create(dto);

        _logger.LogInformation(
            "Uploading device configuration: DeviceId={DeviceId}, DeviceName={DeviceName}, SensorCount={SensorCount}, ReqSeqId={ReqSeqId}",
            configuration.DeviceId,
            configuration.DeviceName,
            configuration.Sensors.Count,
            request.ReqSeqId);

        var response = await _client.RequestAsync<ConfigurationUploadRequest, ConfigurationUploadResponse>(
            subject: UploadDeviceConfigurationSubject,
            data: request,
            cancellationToken: cancellationToken);

        _deviceConfiguration = configuration;

        _logger.LogInformation(
            "Configuration uploaded successfully: DeviceId={DeviceId}, Status={Status}",
            configuration.DeviceId,
            response.Data?.Data?.ConfigurationStatus);

        return response.Data!;
    }

    public async Task<DeviceConfiguration?> GetDeviceConfigurationAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting device configuration: DeviceId={DeviceId}", deviceId);

        // TODO: Request configuration via NATS JetStream
        // var response = await _jetStreamClient.RequestAsync<DeviceConfiguration>(
        //     subject: $"weda.dma.config.get.{deviceId}",
        //     cancellationToken: cancellationToken);

        // Mock response for now
        await Task.Delay(100, cancellationToken);

        _logger.LogWarning("Get device configuration not implemented yet, returning null");

        return _deviceConfiguration;
    }
}
