using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Utilities;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Handles device configuration enrichment.
/// Generates ResourceIds for sensors based on SubNode architecture.
/// NOTE: Registration and Upload logic has been moved to SubNodeManager for
/// aggregate handling after all devices are initialized.
/// </summary>
public sealed class DeviceInitializer(
    SubNodeInfo subNodeInfo,
    ILogger<DeviceInitializer> logger)
{
    private readonly SubNodeInfo _subNodeInfo = subNodeInfo ?? throw new ArgumentNullException(nameof(subNodeInfo));
    private readonly ILogger<DeviceInitializer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Enrich configuration with DeviceId and sensor ResourceIds.
    /// Uses SubNode architecture: resourceId = sha1(subNodeDeviceId + deviceName + sensorName)
    /// </summary>
    /// <param name="configuration">Device configuration to enrich</param>
    /// <param name="subNodeDeviceId">SubNode's globally unique DeviceId</param>
    public void EnrichConfiguration(DeviceConfiguration configuration, string subNodeDeviceId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(subNodeDeviceId);

        _logger.LogDebug("Enriching configuration with SubNode DeviceId and sensor ResourceIds");

        // Set the SubNode's DeviceId on the device configuration
        configuration.DeviceId = subNodeDeviceId;
        configuration.SubNodeInfo = _subNodeInfo;

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
            // Propagate device-level Enabled flag to sensors
            sensor.DeviceEnabled = configuration.Enabled;
        }

        _logger.LogDebug(
            "Configuration enriched for device '{DeviceName}' with {SensorCount} sensor ResourceIds",
            deviceName, configuration.Sensors.Count);
    }
}
