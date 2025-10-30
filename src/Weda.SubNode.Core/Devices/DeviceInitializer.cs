using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Utilities;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Extracts device registration and initialization logic from DeviceBase.
/// Handles: GetOrRegister → EnrichConfiguration → Upload flow.
/// </summary>
public sealed class DeviceInitializer
{
    private readonly IWedaCloudService _cloudService;
    private readonly ILogger<DeviceInitializer> _logger;

    public DeviceInitializer(
        IWedaCloudService cloudService,
        ILogger<DeviceInitializer> logger)
    {
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Step 1: Get or register device ID from cloud service
    /// </summary>
    public async Task<string> GetOrRegisterDeviceIdAsync(DeviceInfo deviceInfo, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting or registering device ID for {DeviceName}", deviceInfo.DeviceName);

        var deviceId = await _cloudService.GetOrRegisterDeviceIdAsync(deviceInfo, ct);

        if (string.IsNullOrEmpty(deviceId))
        {
            var errorMsg = $"Failed to get or register device ID for device '{deviceInfo.DeviceName}'";
            _logger.LogError("{Message}", errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _logger.LogInformation("Device ID obtained: {DeviceId}", deviceId);
        return deviceId;
    }

    /// <summary>
    /// Step 2: Enrich configuration with DeviceId and sensor ResourceIds
    /// </summary>
    public void EnrichConfiguration(DeviceConfiguration configuration, string deviceId)
    {
        _logger.LogDebug("Enriching configuration with DeviceId and sensor ResourceIds");

        configuration.DeviceId = deviceId;

        foreach (var sensor in configuration.Sensors)
        {
            sensor.ResourceId = ResourceIdGenerator.GenerateResourceId(
                deviceId,
                sensor.Name,
                groupId: "weda");
            sensor.DeviceResourceId = deviceId;
        }

        _logger.LogDebug("Configuration enriched with {SensorCount} sensor ResourceIds",
            configuration.Sensors.Count);
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
    /// Complete initialization flow: GetOrRegister → Enrich → Upload
    /// </summary>
    public async Task<string> InitializeDeviceAsync(DeviceConfiguration configuration, CancellationToken ct = default)
    {
        var deviceId = await GetOrRegisterDeviceIdAsync(configuration.DeviceInfo, ct);
        EnrichConfiguration(configuration, deviceId);
        await UploadConfigurationAsync(configuration, ct);
        return deviceId;
    }
}
