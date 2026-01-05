using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Policies;

namespace Weda.SubNode.Core.Managers;

/// <summary>
/// Manages physical device connections with Polly resilience policies.
/// Uses Polly for retry, circuit breaker, and timeout handling.
/// Cloud connection is handled by SubNodeManager at the SubNode level.
/// Extracted from DeviceBase to follow Single Responsibility Principle.
/// </summary>
public class DeviceConnectionManager : IDeviceConnectionManager
{
    private readonly ICommunication _communication;
    private readonly ILogger<DeviceConnectionManager> _logger;
    private readonly ResiliencePipeline<bool> _devicePipeline;

    public CommunicationState CurrentState { get; private set; }

    /// <summary>
    /// Creates a DeviceConnectionManager with default Polly resilience pipelines.
    /// </summary>
    public DeviceConnectionManager(
        ICommunication communication,
        ILogger<DeviceConnectionManager>? logger = null)
        : this(communication, (ConnectionPolicyOptions?)null, logger)
    {
    }

    /// <summary>
    /// Creates a DeviceConnectionManager with ConnectionOptions (simplified configuration).
    /// The ConnectionOptions will be converted to ConnectionPolicyOptions internally.
    /// </summary>
    /// <param name="communication">The communication instance for physical device</param>
    /// <param name="connectionOptions">Simplified connection options from developer configuration</param>
    /// <param name="logger">Optional logger</param>
    public DeviceConnectionManager(
        ICommunication communication,
        ConnectionOptions connectionOptions,
        ILogger<DeviceConnectionManager>? logger = null)
        : this(communication,
            connectionOptions != null ? ConnectionPolicyOptions.FromConnectionOptions(connectionOptions) : null,
            logger)
    {
    }

    /// <summary>
    /// Creates a DeviceConnectionManager with ConnectionPolicyOptions (advanced configuration).
    /// </summary>
    /// <param name="communication">The communication instance for physical device</param>
    /// <param name="devicePolicyOptions">Policy options for device connection (null for default)</param>
    /// <param name="logger">Optional logger</param>
    public DeviceConnectionManager(
        ICommunication communication,
        ConnectionPolicyOptions? devicePolicyOptions,
        ILogger<DeviceConnectionManager>? logger = null)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? NullLogger<DeviceConnectionManager>.Instance;

        // Create pipeline from options (or use defaults)
        _devicePipeline = ConnectionPolicies.CreateDeviceConnectionPipeline(_logger, devicePolicyOptions);

        CurrentState = CommunicationState.Disconnected;
    }

    /// <summary>
    /// Creates a DeviceConnectionManager with custom Polly resilience pipeline (for advanced scenarios).
    /// </summary>
    public DeviceConnectionManager(
        ICommunication communication,
        ResiliencePipeline<bool>? devicePipeline,
        ILogger<DeviceConnectionManager>? logger = null)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? NullLogger<DeviceConnectionManager>.Instance;

        // Create default pipeline if not provided
        _devicePipeline = devicePipeline ?? ConnectionPolicies.CreateDeviceConnectionPipeline(_logger);

        CurrentState = CommunicationState.Disconnected;
    }

    /// <summary>
    /// Establishes connection to the physical device only.
    /// Cloud connection is handled by SubNodeManager at the SubNode level.
    /// </summary>
    public async Task<ErrorOr<Success>> EstablishPhysicalConnectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Establishing physical device connection with Polly resilience policies...");
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
            CurrentState = CommunicationState.Connected;

            return Result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during physical device connection");
            CurrentState = CommunicationState.Disconnected;
            return Errors.Device.Unexpected(ex);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Disconnecting from physical device");

        try
        {
            await _communication.DisconnectAsync(cancellationToken);

            CurrentState = CommunicationState.Disconnected;
            _logger.LogInformation("Successfully disconnected from physical device");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnection");
            throw;
        }
    }
}
