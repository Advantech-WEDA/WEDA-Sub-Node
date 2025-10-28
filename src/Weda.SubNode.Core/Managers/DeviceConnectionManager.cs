using ErrorOr;
using Microsoft.Extensions.Logging;
using Polly;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Policies;

namespace Weda.SubNode.Core.Managers;

/// <summary>
/// Manages device connections (physical device + cloud service) with Polly resilience policies.
/// Uses Polly for retry, circuit breaker, and timeout handling.
/// Extracted from DeviceBase to follow Single Responsibility Principle.
/// </summary>
public class DeviceConnectionManager : IDeviceConnectionManager
{
    private readonly ICommunication _communication;
    private readonly IWedaCloudService _cloudService;
    private readonly ILogger<DeviceConnectionManager> _logger;
    private readonly ResiliencePipeline<bool> _devicePipeline;
    private readonly ResiliencePipeline<bool> _cloudPipeline;

    public CommunicationState CurrentState { get; private set; }

    public event Func<UpdateConfigurationEvent, Task>? ConfigurationUpdateReceived;
    public event Func<ExecuteCommandEvent, Task>? CommandReceived;

    /// <summary>
    /// Creates a DeviceConnectionManager with default Polly resilience pipelines.
    /// </summary>
    public DeviceConnectionManager(
        ICommunication communication,
        IWedaCloudService cloudService,
        ILogger<DeviceConnectionManager>? logger = null)
        : this(communication, cloudService, null, null, logger)
    {
    }

    /// <summary>
    /// Creates a DeviceConnectionManager with custom Polly resilience pipelines.
    /// </summary>
    public DeviceConnectionManager(
        ICommunication communication,
        IWedaCloudService cloudService,
        ResiliencePipeline<bool>? devicePipeline,
        ResiliencePipeline<bool>? cloudPipeline,
        ILogger<DeviceConnectionManager>? logger = null)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DeviceConnectionManager>.Instance;

        // Create default pipelines if not provided
        _devicePipeline = devicePipeline ?? ConnectionPolicies.CreateDeviceConnectionPipeline(_logger);
        _cloudPipeline = cloudPipeline ?? ConnectionPolicies.CreateCloudConnectionPipeline(_logger);

        CurrentState = CommunicationState.Disconnected;
    }

    public async Task<ErrorOr<Success>> EstablishConnectionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Establishing device connections with Polly resilience policies...");
        CurrentState = CommunicationState.Connecting;

        try
        {
            // Connect to physical device using Polly pipeline (retry + circuit breaker + timeout)
            _logger.LogDebug("Connecting to physical device...");
            var deviceConnected = await _devicePipeline.ExecuteAsync(
                async ct => await _communication.ConnectAsync(ct),
                cancellationToken);

            if (!deviceConnected)
            {
                CurrentState = CommunicationState.Disconnected;
                _logger.LogError("Failed to connect to physical device after all retry attempts");
                return Errors.Device.PhysicalDeviceFailed;
            }

            _logger.LogInformation("Physical device connected successfully");

            // Connect to cloud service using Polly pipeline (retry + circuit breaker + timeout)
            _logger.LogDebug("Connecting to cloud service...");
            var cloudConnected = await _cloudPipeline.ExecuteAsync(
                async ct => await _cloudService.ConnectAsync(ct),
                cancellationToken);

            if (!cloudConnected)
            {
                CurrentState = CommunicationState.Disconnected;
                _logger.LogError("Failed to connect to cloud service after all retry attempts");
                return Errors.Device.CloudServiceFailed;
            }

            _logger.LogInformation("Cloud service connected successfully");

            CurrentState = CommunicationState.Connected;
            _logger.LogInformation("All connections established successfully");

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during connection establishment");
            CurrentState = CommunicationState.Disconnected;
            return Errors.Device.Unexpected(ex);
        }
    }

    public async Task<ErrorOr<Success>> SubscribeToCloudEventsAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        if (CurrentState != CommunicationState.Connected)
        {
            return Errors.Device.NotConnected;
        }

        _logger.LogDebug("Subscribing to cloud events for device {DeviceId}", deviceId);

        try
        {
            await _cloudService.SubscribeConfigurationUpdatesAsync(
                deviceId,
                async (@event) =>
                {
                    if (ConfigurationUpdateReceived != null)
                    {
                        await ConfigurationUpdateReceived.Invoke(@event);
                    }
                },
                cancellationToken);

            await _cloudService.SubscribeCommandsAsync(
                deviceId,
                async (@event) =>
                {
                    if (CommandReceived != null)
                    {
                        await CommandReceived.Invoke(@event);
                    }
                },
                cancellationToken);

            _logger.LogInformation("Successfully subscribed to cloud events");
            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to cloud events");
            return Errors.Device.SubscriptionFailed;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Disconnecting from physical device and cloud service");

        try
        {
            await _communication.DisconnectAsync(cancellationToken);
            await _cloudService.DisconnectAsync(cancellationToken);

            CurrentState = CommunicationState.Disconnected;
            _logger.LogInformation("Successfully disconnected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnection");
            throw;
        }
    }
}
