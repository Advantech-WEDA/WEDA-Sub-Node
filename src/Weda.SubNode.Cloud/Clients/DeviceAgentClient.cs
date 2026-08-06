using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Net;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Cloud.Serialization;
using Weda.SubNode.Core.Cloud.Clients.DeviceManagement.Mapping;
using Weda.SubNode.Core.Commands;

namespace Weda.SubNode.Cloud.Clients;

/// <summary>
/// Device Agent Client implementation for NATS
/// Handles device registration, sensor validation, and configuration management
/// </summary>
public class DeviceAgentClient : IDeviceAgentClient
{
    private readonly ILogger<DeviceAgentClient> _logger;
    private readonly NatsClient _client;
    private readonly CommandRegistry? _commandRegistry;
    private readonly ProjectInfo _projectInfo;
    private DeviceConfigurations? _deviceConfigurations;

    public DeviceAgentClient(
        NatsClient client,
        ILogger<DeviceAgentClient>? logger = null,
        CommandRegistry? commandRegistry = null,
        ProjectInfo? projectInfo = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<DeviceAgentClient>();
        _commandRegistry = commandRegistry;
        _projectInfo = projectInfo ?? ProjectInfo.Empty;
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
        DeviceConfigurations configurations,
        CancellationToken cancellationToken = default)
    {
        if (configurations.Count == 0)
        {
            throw new ArgumentException("DeviceConfigurations must not be empty", nameof(configurations));
        }

        // Convert to DTO using mapping extension — pulls Transform / DSP filter
        // descriptors from static factories and command descriptors from the
        // injected registry (empty when registry is not wired up).
        var dto = configurations.ToConfigurationDto(_commandRegistry, _projectInfo);
        var request = ConfigurationUploadRequest.Create(dto);

        _logger.LogInformation(
            "Uploading Subode configuration: DeviceId={DeviceId}, DeviceCountSensorCount={SensorCount}",
            dto.DeviceId,
            dto.DeviceCapabilities.Sensors.Count);


        // Force the ENTIRE outbound payload to camelCase before sending. The DTO
        // envelope carries dictionary keys (each sensor's Parameters, DeviceInfo,
        // Communication, etc.) and DeviceConfigs entry names that would otherwise
        // reach the wire in their authored casing. Serialize with the same options
        // the NATS JSON serializer uses, normalize the bytes to strict camelCase for
        // the schemaVersion=2 cloud validator, then send as raw bytes (passed through
        // untouched by the NATS serializer).
        var payload = CamelCaseJsonNormalizer.SerializeAndNormalize(
            request, WedaNatsSerializerRegistry.DefaultOptions);

        var response = await _client.RequestAsync<byte[], ConfigurationUploadResponse>(
            subject: UploadDeviceConfigurationSubject,
            data: payload,
            cancellationToken: cancellationToken);

        _deviceConfigurations = configurations;

        _logger.LogInformation(
            "Configuration uploaded successfully: DeviceId={DeviceId}, Status={Status}",
            dto.DeviceId,
            response.Data?.Data?.ConfigurationStatus);

        return response.Data!;
    }
}
