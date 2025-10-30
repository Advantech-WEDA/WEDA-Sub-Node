using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core.Communication;

/// <summary>
/// Base communication implementation with built-in retry and reconnection mechanism
/// </summary>
public abstract class CommunicationBase : ICommunication
{
    protected readonly ILogger<CommunicationBase> _logger;
    private CommunicationState _state = CommunicationState.Disconnected;
    private bool _disposed;
    private int _reconnectAttempts = 0;

    public ConnectionSettings Settings { get; }

    protected CommunicationBase(ConnectionSettings? settings = null, ILogger<CommunicationBase>? logger = null)
    {
        Settings = settings ?? new ConnectionSettings();
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<CommunicationBase>();
    }

    public CommunicationState State
    {
        get => _state;
        protected set
        {
            if (_state != value)
            {
                var previous = _state;
                _state = value;
                OnStateChanged(previous, value);
            }
        }
    }

    public bool IsConnected => State == CommunicationState.Connected;

    /// <summary>
    /// Connect with automatic retry mechanism
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt <= Settings.MaxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = CalculateRetryDelay(attempt);
                    _logger.LogWarning("Connection attempt {Attempt}/{MaxRetries}, retrying after {Delay}ms...",
                        attempt, Settings.MaxRetries, delay);
                    await Task.Delay(delay, cancellationToken);
                }

                var connected = await ConnectCoreAsync(cancellationToken);
                if (connected)
                {
                    _reconnectAttempts = 0;
                    return true;
                }
            }
            catch (Exception ex) when (attempt < Settings.MaxRetries)
            {
                _logger.LogWarning(ex, "Connection attempt {Attempt}/{MaxRetries} failed",
                    attempt + 1, Settings.MaxRetries + 1);
            }
        }

        _logger.LogError("Failed to connect after {MaxRetries} attempts", Settings.MaxRetries + 1);
        State = CommunicationState.Error;
        return false;
    }

    /// <summary>
    /// Reconnect with exponential backoff
    /// </summary>
    public async Task<bool> ReconnectAsync(CancellationToken cancellationToken = default)
    {
        _reconnectAttempts++;

        if (_reconnectAttempts > Settings.MaxRetries)
        {
            _logger.LogError("Max reconnection attempts ({MaxRetries}) reached", Settings.MaxRetries);
            State = CommunicationState.Error;
            return false;
        }

        var delay = CalculateRetryDelay(_reconnectAttempts);
        _logger.LogWarning("Reconnection attempt {Attempt}/{MaxRetries} after {Delay}ms...",
            _reconnectAttempts, Settings.MaxRetries, delay);

        await Task.Delay(delay, cancellationToken);
        await DisconnectAsync(cancellationToken);

        var connected = await ConnectAsync(cancellationToken);
        if (connected)
        {
            _reconnectAttempts = 0;
        }

        return connected;
    }

    /// <summary>
    /// Reset reconnection attempt counter (call on successful operation)
    /// </summary>
    public void ResetReconnectAttempts()
    {
        if (_reconnectAttempts > 0)
        {
            _logger.LogDebug("Resetting reconnection attempts counter");
            _reconnectAttempts = 0;
        }
    }

    protected abstract Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default);
    public abstract Task DisconnectAsync(CancellationToken cancellationToken = default);
    public abstract Task<byte[]> ReadAsync(CancellationToken cancellationToken = default);
    public abstract Task<bool> WriteAsync(byte[] data, CancellationToken cancellationToken = default);

    private int CalculateRetryDelay(int attempt)
    {
        if (!Settings.UseExponentialBackoff)
            return Settings.InitialRetryDelayMs;

        // Exponential backoff: delay = InitialDelay * 2^(attempt-1)
        var delay = Settings.InitialRetryDelayMs * (int)Math.Pow(2, attempt - 1);
        return Math.Min(delay, 60000); // Cap at 60 seconds
    }

    protected virtual void OnStateChanged(CommunicationState previousState, CommunicationState currentState, string? reason = null)
    {
        _logger.LogInformation("Communication state changed from {PreviousState} to {CurrentState}", previousState, currentState);

        var @event = new ConnectionStateChangedEvent(
            DeviceId: "Unknown", // Will be set by device
            DeviceType: DeviceType.CustomDevice,
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
