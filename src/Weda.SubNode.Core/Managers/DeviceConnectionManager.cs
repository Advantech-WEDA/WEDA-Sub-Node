using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core.Managers;

/// <summary>
/// Manages device connections (physical device + cloud service)
/// Extracted from DeviceBase to follow Single Responsibility Principle
/// </summary>
public class DeviceConnectionManager : IDeviceConnectionManager
{
    private readonly ICommunication _communication;
    private readonly IWedaCloudService _cloudService;
    private readonly ILogger<DeviceConnectionManager> _logger;
    private readonly ConnectionOptions _options;

    public CommunicationState CurrentState { get; private set; }

    public event Func<UpdateConfigurationEvent, Task>? ConfigurationUpdateReceived;
    public event Func<ExecuteCommandEvent, Task>? CommandReceived;

    public DeviceConnectionManager(
        ICommunication communication,
        IWedaCloudService cloudService,
        ConnectionOptions? options = null,
        ILogger<DeviceConnectionManager>? logger = null)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _options = options ?? ConnectionOptions.Default;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DeviceConnectionManager>.Instance;

        CurrentState = CommunicationState.Disconnected;
    }

    public async Task<ErrorOr<Success>> EstablishConnectionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Establishing device connections...");
        CurrentState = CommunicationState.Connecting;

        try
        {
            // Connect to physical device with retry
            var deviceConnected = await ConnectWithRetryAsync(
                () => _communication.ConnectAsync(cancellationToken),
                "physical device",
                cancellationToken);

            if (!deviceConnected)
            {
                CurrentState = CommunicationState.Disconnected;
                return Errors.Device.PhysicalDeviceFailed;
            }

            // Connect to cloud service with retry
            var cloudConnected = await ConnectWithRetryAsync(
                () => _cloudService.ConnectAsync(cancellationToken),
                "cloud service",
                cancellationToken);

            if (!cloudConnected)
            {
                CurrentState = CommunicationState.Disconnected;
                return Errors.Device.CloudServiceFailed;
            }

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

    private async Task<bool> ConnectWithRetryAsync(
        Func<Task<bool>> connectFunc,
        string targetName,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= _options.MaxRetryAttempts; attempt++)
        {
            try
            {
                _logger.LogDebug(
                    "Connecting to {Target} (attempt {Attempt}/{Max})",
                    targetName,
                    attempt,
                    _options.MaxRetryAttempts);

                var connected = await connectFunc();

                if (connected)
                {
                    _logger.LogDebug("Successfully connected to {Target}", targetName);
                    return true;
                }

                _logger.LogWarning(
                    "Failed to connect to {Target} (attempt {Attempt}/{Max})",
                    targetName,
                    attempt,
                    _options.MaxRetryAttempts);
            }
            catch (Exception ex) when (attempt < _options.MaxRetryAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Error connecting to {Target} (attempt {Attempt}/{Max})",
                    targetName,
                    attempt,
                    _options.MaxRetryAttempts);
            }

            if (attempt < _options.MaxRetryAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(
                    _options.RetryDelayMs * Math.Pow(2, attempt - 1));

                await Task.Delay(delay, cancellationToken);
            }
        }

        return false;
    }
}
