using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Utilities;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Extracts device registration and initialization logic from DeviceBase.
/// Handles: GetOrRegister → EnrichConfiguration → Upload flow.
/// Supports SubNode architecture where all devices share a single SubNode DeviceId.
/// </summary>
public sealed class DeviceInitializer
{
    private readonly IWedaCloudService _cloudService;
    private readonly SubNodeInfo _subNodeInfo;
    private readonly ILogger<DeviceInitializer> _logger;

    public DeviceInitializer(
        IWedaCloudService cloudService,
        SubNodeInfo subNodeInfo,
        ILogger<DeviceInitializer> logger)
    {
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _subNodeInfo = subNodeInfo ?? throw new ArgumentNullException(nameof(subNodeInfo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Step 1: Ensure SubNode is registered with cloud service.
    /// Always calls GetOrRegisterDeviceIdAsync to ensure topics are configured,
    /// even when DeviceId already exists in SubNodeInfo (from registration cache).
    /// </summary>
    /// <returns>The SubNode's globally unique DeviceId</returns>
    public async Task<string> EnsureSubNodeRegisteredAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Ensuring SubNode '{SubNodeName}' is registered with cloud service", _subNodeInfo.Name);

        // Create DeviceInfo for SubNode registration using configured DeviceType
        // If SubNodeInfo already has a DeviceId (from cache), include it in the request
        var subNodeDeviceInfo = new DeviceInfo
        {
            DeviceName = _subNodeInfo.Name,
            DeviceType = _subNodeInfo.DeviceType,
            Manufacturer = _subNodeInfo.Manufacturer,
            Model = $"{_subNodeInfo.Model} v{_subNodeInfo.Version}",
            DeviceId = _subNodeInfo.DeviceId // Pass existing DeviceId if available
        };

        // Always call GetOrRegisterDeviceIdAsync to:
        // 1. Load registration from cache (including topics) if exists
        // 2. Or register with cloud if not exists
        // This ensures topics are always configured
        var deviceId = await _cloudService.GetOrRegisterDeviceIdAsync(subNodeDeviceInfo, ct);

        if (string.IsNullOrEmpty(deviceId))
        {
            var errorMsg = $"Failed to register SubNode '{_subNodeInfo.Name}' with cloud service";
            _logger.LogError("{Message}", errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _logger.LogInformation(
            "SubNode '{SubNodeName}' registered successfully with DeviceId: {DeviceId}",
            _subNodeInfo.Name, deviceId);

        return deviceId;
    }

    /// <summary>
    /// Step 2: Enrich configuration with DeviceId and sensor ResourceIds.
    /// Uses SubNode architecture: resourceId = sha1(subNodeDeviceId + deviceName + sensorName)
    /// </summary>
    /// <param name="configuration">Device configuration to enrich</param>
    /// <param name="subNodeDeviceId">SubNode's globally unique DeviceId</param>
    public void EnrichConfiguration(DeviceConfiguration configuration, string subNodeDeviceId)
    {
        _logger.LogDebug("Enriching configuration with SubNode DeviceId and sensor ResourceIds");

        // Set the SubNode's DeviceId on the device configuration
        configuration.DeviceId = subNodeDeviceId;

        var deviceName = configuration.DeviceInfo.DeviceName;

        foreach (var sensor in configuration.Sensors)
        {
            // Generate ResourceId using SubNode architecture:
            // sha1(subNodeDeviceId + deviceName + sensorName)
            sensor.ResourceId = ResourceIdGenerator.GenerateSensorResourceId(
                subNodeDeviceId,
                deviceName,
                sensor.Name,
                groupId: "weda");
            sensor.DeviceResourceId = subNodeDeviceId;
        }

        _logger.LogDebug(
            "Configuration enriched for device '{DeviceName}' with {SensorCount} sensor ResourceIds",
            deviceName, configuration.Sensors.Count);
    }

    /// <summary>
    /// Step 3: Upload enriched configuration to cloud
    /// </summary>
    public async Task UploadConfigurationAsync(DeviceConfiguration configuration, CancellationToken ct = default)
    {
        _logger.LogDebug("Uploading device configuration to cloud");

        var success = await _cloudService.UploadDeviceConfigurationAsync(configuration, ct);

        if (!success)
        {
            var errorMsg = $"Failed to upload device configuration for device '{configuration.DeviceId}'";
            _logger.LogError("{Message}", errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _logger.LogInformation("Device configuration uploaded successfully");
    }

    /// <summary>
    /// Complete initialization flow for SubNode architecture:
    /// 1. Ensure SubNode is registered (gets or creates SubNode DeviceId)
    /// 2. Enrich device configuration with SubNode DeviceId and sensor ResourceIds
    /// 3. Upload device configuration to cloud
    /// </summary>
    /// <param name="configuration">Device configuration to initialize</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The SubNode's DeviceId (shared by all devices in this SubNode)</returns>
    public async Task<string> InitializeDeviceAsync(DeviceConfiguration configuration, CancellationToken ct = default)
    {
        // Step 1: Ensure SubNode is registered (only registers once, subsequent calls return cached DeviceId)
        var subNodeDeviceId = await EnsureSubNodeRegisteredAsync(ct);

        // Step 2: Enrich configuration using SubNode architecture
        // resourceId = sha1(subNodeDeviceId + deviceName + sensorName)
        EnrichConfiguration(configuration, subNodeDeviceId);

        // Step 3: Upload device configuration to cloud
        await UploadConfigurationAsync(configuration, ct);

        _logger.LogInformation(
            "Device '{DeviceName}' initialized under SubNode '{SubNodeName}' (DeviceId: {DeviceId})",
            configuration.DeviceInfo.DeviceName, _subNodeInfo.Name, subNodeDeviceId);

        return subNodeDeviceId;
    }
}
