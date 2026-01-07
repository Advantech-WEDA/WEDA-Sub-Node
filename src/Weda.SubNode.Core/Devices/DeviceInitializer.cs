using ErrorOr;

using Microsoft.Extensions.Logging;

using Polly;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Policies;
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
    private readonly TimeSpan maxDelay = TimeSpan.FromSeconds(30);
    private TimeSpan initialDelay = TimeSpan.FromMilliseconds(100);

    private readonly ResiliencePipeline<bool> _retryPipeline;

    public DeviceInitializer(
        IWedaCloudService cloudService,
        SubNodeInfo subNodeInfo,
        ILogger<DeviceInitializer> logger)
    {
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _subNodeInfo = subNodeInfo ?? throw new ArgumentNullException(nameof(subNodeInfo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _retryPipeline = RetryPolicyFactory.CreateAlwaysRetryBool(
            logger: _logger,
            initialDelay: initialDelay,
            maxDelay: maxDelay);
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

        // Create DeviceInfo for SubNode registration using configured SubNodeType
        // If SubNodeInfo already has a DeviceId (from cache), include it in the request
        var subNodeDeviceInfo = new DeviceInfo
        {
            DeviceName = _subNodeInfo.Name,
            SubNodeType = _subNodeInfo.SubNodeType,
            Manufacturer = _subNodeInfo.Manufacturer,
            Model = $"{_subNodeInfo.Model} v{_subNodeInfo.SwVersion}",
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
    /// Step 3: Upload enriched configuration to cloud.
    /// </summary>
    /// <param name="configuration">The configuration enriched with SubNode Device ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// An <see cref="ErrorOr{Success}"/> result indicating the outcome of the operation:
    /// <list type="bullet">
    /// <item><description><see cref="Success"/>: If the configuration was successfully accepted by the cloud.</description></item>
    /// <item><description><see cref="ErrorType.NotFound"/> (or "Device.NotFound"): If the device ID is invalid or not recognized by the cloud.</description></item>
    /// <item><description>Other <see cref="Error"/>: In case of network issues, validation failures, or server-side errors.</description></item>
    /// </list>
    /// </returns>
    public async Task<ErrorOr<Success>> UploadConfigurationAsync(DeviceConfiguration configuration, CancellationToken ct = default)
    {
        _logger.LogDebug("Uploading device configuration to cloud");

        var result = await _cloudService.UploadDeviceConfigurationAsync(configuration, ct);

        if (result.IsError)
        {
            var errorsText = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            _logger.LogError("Failed to upload device configuration for device '{DeviceId}'. Errors: {Errors}",
                configuration.DeviceId, errorsText);

            return result.Errors;
        }

        if (result.Value)
        {
            _logger.LogInformation("Device configuration uploaded successfully");
            return Result.Success;
        }

        return Error.Failure(
            code: "Upload.UnexpectedFalse",
            description: $"Upload returned false for device '{configuration.DeviceId}'");
    }

    /// <summary>
    /// Complete initialization flow for SubNode architecture:
    /// 1. Ensure SubNode is registered (gets or creates SubNode DeviceId)
    /// 2. Enrich device configuration with SubNode DeviceId and sensor ResourceIds
    /// 3. Upload device configuration to cloud
    /// Uses RetryPolicyFactory.CreateAlwaysRetryBool for unlimited retry with exponential backoff.
    /// </summary>
    /// <param name="configuration">Device configuration to initialize</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The SubNode's DeviceId (shared by all devices in this SubNode)</returns>
    public async Task<string> InitializeDeviceAsync(DeviceConfiguration configuration, CancellationToken ct = default)
    {
        string? deviceId = null;

        // Use RetryPolicyFactory for unlimited retry with exponential backoff
        var success = await _retryPipeline.ExecuteAsync(async token =>
        {
            token.ThrowIfCancellationRequested();

            try
            {
                // Step 1: Ensure SubNode is registered
                var id = await EnsureSubNodeRegisteredAsync(token);

                // Step 2: Enrich configuration with DeviceId and ResourceIds
                EnrichConfiguration(configuration, id);

                // Step 3: Upload configuration to cloud
                var result = await UploadConfigurationAsync(configuration, token);

                if (!result.IsError)
                {
                    // Success - save the DeviceId and return true to stop retrying
                    deviceId = id;
                    return true;
                }

                // Check if device not found (need to delete registration and retry)
                var isNotFound = result.Errors.Any(e =>
                    e.Type == ErrorType.NotFound ||
                    string.Equals(e.Code, "Device.NotFound", StringComparison.OrdinalIgnoreCase));

                var errorsText = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));

                if (isNotFound)
                {
                    _logger.LogWarning(
                        "Device not found; deleting registration and retrying. DeviceName={DeviceName}, Errors={Errors}",
                        configuration.DeviceInfo?.DeviceName,
                        errorsText);

                    await _cloudService.DeleteRegistrationAsync(token);
                }
                else
                {
                    _logger.LogWarning(
                        "Initialize attempt failed; will retry. Errors={Errors}. DeviceName={DeviceName}",
                        errorsText,
                        configuration.DeviceInfo?.DeviceName);
                }

                // Return false to trigger retry
                return false;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Initialize attempt failed due to exception; will retry. DeviceName={DeviceName}",
                    configuration.DeviceInfo?.DeviceName);

                // Return false to trigger retry
                return false;
            }
        }, ct);

        if (!success || deviceId == null)
        {
            throw new InvalidOperationException(
                $"Device initialization was cancelled or failed for device '{configuration.DeviceInfo?.DeviceName}'");
        }

        return deviceId;
    }
}
