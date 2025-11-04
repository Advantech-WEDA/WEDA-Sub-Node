using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Core.Utilities;

namespace Weda.SubNode.Core.Initialization;

/// <summary>
/// Device initializer handles the complete device registration flow
/// 1. Check for existing device registration in storage
/// 2. Request device ID from cloud if needed
/// 3. Generate sensor resource IDs
/// 4. Register device with complete configuration
/// 5. Upload configuration
/// 6. Store registration response (including NATS topics)
/// </summary>
public class DeviceInitializer
{
    private readonly IDeviceAgentClient _deviceAgent;
    private readonly IDeviceRegistrationStorage _registrationStorage;
    private readonly IWedaCloudService _cloudService;
    private readonly ILogger<DeviceInitializer> _logger;

    /// <summary>
    /// Create device initializer with registration storage
    /// Stores complete registration including NATS topics
    /// </summary>
    public DeviceInitializer(
        IDeviceAgentClient deviceAgent,
        IDeviceRegistrationStorage registrationStorage,
        IWedaCloudService cloudService,
        ILogger<DeviceInitializer>? logger = null)
    {
        _deviceAgent = deviceAgent ?? throw new ArgumentNullException(nameof(deviceAgent));
        _registrationStorage = registrationStorage ?? throw new ArgumentNullException(nameof(registrationStorage));
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<DeviceInitializer>();
    }

    /// <summary>
    /// Initialize device with complete registration flow
    /// </summary>
    /// <param name="configuration">Device configuration (DeviceId and ResourceIds will be set)</param>
    /// <param name="macAddress">MAC address for device ID generation (optional, not used)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device registration response data (includes NATS topics)</returns>
    public async Task<DeviceRegistrationResponseData?> InitializeAsync(
        DeviceConfiguration configuration,
        string? macAddress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger.LogInformation(
            "Starting device initialization: DeviceName={DeviceName}, DeviceType={DeviceType}",
            configuration.DeviceName,
            configuration.DeviceType);

        // Step 1: Check for existing registration (to get stored deviceId if available)
        var existingRegistration = await GetExistingRegistrationAsync(cancellationToken);
        if (existingRegistration != null)
        {
            _logger.LogInformation(
                "Found existing device ID in storage: DeviceId={DeviceId}",
                existingRegistration.DeviceId);

            // Set the deviceId in configuration so it will be sent to cloud
            configuration.DeviceId = existingRegistration.DeviceId;
        }
        else
        {
            _logger.LogInformation("No existing device ID found in storage - cloud will generate new ID");
        }

        // Step 2: ALWAYS register device with cloud (with or without deviceId)
        // Cloud will either:
        // - Use the provided deviceId if it exists and is valid
        // - Generate a new deviceId if not provided or invalid
        _logger.LogInformation(
            "Registering device with cloud: DeviceId={DeviceId}",
            configuration.DeviceId ?? "(new)");

        var registrationData = await RegisterDeviceAsync(configuration, cancellationToken);

        // Step 3: Set device ID in configuration (cloud may have changed it)
        configuration.DeviceId = registrationData.DeviceId;

        // Step 4: Generate sensor resource IDs
        GenerateSensorResourceIds(configuration, registrationData.DeviceId);

        // Step 5: Configure NATS topics from registration response
        _cloudService.ConfigureTopics(registrationData.NatsTopicAssignments);

        // Step 6: Upload configuration
        await UploadConfigurationAsync(configuration, cancellationToken);

        _logger.LogInformation(
            "Device initialization completed successfully: DeviceId={DeviceId}",
            registrationData.DeviceId);

        return registrationData;
    }

    /// <summary>
    /// Get existing registration from storage
    /// </summary>
    private async Task<DeviceRegistrationResponseData?> GetExistingRegistrationAsync(
        CancellationToken cancellationToken)
    {
        var registration = await _registrationStorage.GetRegistrationAsync(cancellationToken);

        if (registration != null && !string.IsNullOrEmpty(registration.DeviceId))
        {
            _logger.LogInformation(
                "Device registration found in storage: DeviceId={DeviceId}, Status={Status}",
                registration.DeviceId,
                registration.RegistrationStatus);
            return registration;
        }

        _logger.LogInformation("No existing registration found in storage");
        return null;
    }

    /// <summary>
    /// Generate sensor resource IDs based on device ID
    /// </summary>
    private void GenerateSensorResourceIds(DeviceConfiguration configuration, string deviceId)
    {
        _logger.LogInformation(
            "Generating sensor resource IDs for {SensorCount} sensors",
            configuration.Sensors.Count);

        for (int i = 0; i < configuration.Sensors.Count; i++)
        {
            var sensor = configuration.Sensors[i];
            sensor.ResourceId = ResourceIdGenerator.GenerateSensorIdFromDeviceId(deviceId, i + 1);
            sensor.DeviceResourceId = deviceId;

            _logger.LogDebug(
                "Generated sensor resource ID: SensorName={SensorName}, ResourceId={ResourceId}",
                sensor.Name,
                sensor.ResourceId);
        }

        _logger.LogInformation("Sensor resource IDs generated successfully");
    }

    /// <summary>
    /// Register device with cloud
    /// Stores full registration response including NATS topics
    /// </summary>
    private async Task<DeviceRegistrationResponseData> RegisterDeviceAsync(
        DeviceConfiguration configuration,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Registering device with cloud");

        var response = await _deviceAgent.RegisterDeviceAsync(
            configuration.DeviceInfo,
            cancellationToken);

        if (response.Code != 0 || response.Data == null)
        {
            throw new InvalidOperationException(
                $"Device registration failed: Code={response.Code}, Message={response.Message}");
        }

        _logger.LogInformation(
            "Device registered successfully: DeviceId={DeviceId}, Status={Status}",
            response.Data.DeviceId,
            response.Data.RegistrationStatus);

        // Save complete registration data
        await _registrationStorage.SaveRegistrationAsync(response.Data, cancellationToken);
        _logger.LogInformation(
            "Device registration data saved to storage (includes NATS topic assignments)");

        return response.Data;
    }

    /// <summary>
    /// Upload device configuration
    /// </summary>
    private async Task UploadConfigurationAsync(
        DeviceConfiguration configuration,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Uploading device configuration");

        var response = await _deviceAgent.UploadDeviceConfigurationAsync(
            configuration,
            cancellationToken);

        if (response.Code != 0)
        {
            _logger.LogWarning(
                "Configuration upload failed: Code={Code}, Message={Message}",
                response.Code,
                response.Message);
        }
        else
        {
            _logger.LogInformation("Configuration uploaded successfully");
        }
    }

    /// <summary>
    /// Reset device registration (for testing or re-registration)
    /// Deletes stored registration data
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Resetting device registration - deleting stored data");

        await _registrationStorage.DeleteRegistrationAsync(cancellationToken);

        _logger.LogInformation("Device registration reset completed");
    }

    /// <summary>
    /// Get stored registration data (includes NATS topics)
    /// </summary>
    public async Task<DeviceRegistrationResponseData?> GetStoredRegistrationAsync(
        CancellationToken cancellationToken = default)
    {
        return await _registrationStorage.GetRegistrationAsync(cancellationToken);
    }
}
