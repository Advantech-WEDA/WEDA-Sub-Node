using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core.Communication.Common;

/// <summary>
/// Base communication implementation with built-in retry and reconnection mechanism
/// </summary>
public abstract class CommunicationBase : ICommunication
{
    protected readonly ILogger<CommunicationBase> _logger;
    private CommunicationState _state = CommunicationState.Disconnected;
    private bool _disposed;

    public ConnectionSettings Settings { get; }

    protected CommunicationBase(ConnectionSettings? settings = null, ILogger<CommunicationBase>? logger = null)
    {
        Settings = settings ?? new ConnectionSettings();
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<CommunicationBase>();
    }

    public CommunicationState State
    {
        get => _state;
        protected set => SetState(value);
    }

    /// <summary>
    /// Transitions to <paramref name="newState"/> and raises <see cref="StateChanged"/> when the
    /// state actually changes.
    /// </summary>
    /// <remarks>
    /// Implementations must use this (or the <see cref="State"/> setter) rather than calling
    /// <see cref="OnStateChanged"/> directly: raising the event without updating the backing state
    /// leaves <see cref="State"/> stale, and health monitoring reads <see cref="State"/>.
    /// </remarks>
    /// <param name="newState">The state to transition to.</param>
    /// <param name="reason">Optional human-readable reason carried on the event.</param>
    protected void SetState(CommunicationState newState, string? reason = null)
    {
        if (_state == newState)
        {
            return;
        }

        var previous = _state;
        _state = newState;
        OnStateChanged(previous, newState, reason);
    }

    public bool IsConnected => State == CommunicationState.Connected;

    /// <summary>
    /// Connects to the communication endpoint (single attempt).
    /// Retry logic is handled by Polly resilience pipeline in DeviceConnectionManager.
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Attempting connection to communication endpoint...");
            var connected = await ConnectCoreAsync(cancellationToken);

            if (connected)
            {
                _logger.LogDebug("Connection successful");
                return true;
            }

            _logger.LogWarning("Connection failed");
            State = CommunicationState.Error;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection attempt failed with exception");
            State = CommunicationState.Error;
            return false;
        }
    }

    /// <summary>
    /// Reconnects to the communication endpoint after disconnection.
    /// This is a single reconnection attempt. For retry logic with exponential backoff,
    /// use Polly's CreateReconnectionPipeline from ConnectionPolicies.
    /// </summary>
    public async Task<bool> ReconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reconnecting to communication endpoint...");

        await DisconnectAsync(cancellationToken);
        var connected = await ConnectAsync(cancellationToken);

        if (connected)
        {
            _logger.LogInformation("Reconnection successful");
        }
        else
        {
            _logger.LogWarning("Reconnection failed");
        }

        return connected;
    }

    protected abstract Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default);
    public abstract Task DisconnectAsync(CancellationToken cancellationToken = default);

    protected virtual void OnStateChanged(CommunicationState previousState, CommunicationState currentState, string? reason = null)
    {
        _logger.LogDebug("Communication state changed from {PreviousState} to {CurrentState}", previousState, currentState);

        var @event = new ConnectionStateChangedEvent(
            DeviceId: "Unknown", // Will be set by device
            SubNodeType: SubNodeType.CustomDevice,
            PreviousState: previousState,
            CurrentState: currentState,
            Timestamp: DateTimeOffset.UtcNow)
        {
            Reason = reason  // Optional property
        };
        StateChanged?.Invoke(this, @event);
    }

    public event EventHandler<ConnectionStateChangedEvent>? StateChanged;

    public void Dispose()
    {
        if (_disposed) return;
        DisconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
