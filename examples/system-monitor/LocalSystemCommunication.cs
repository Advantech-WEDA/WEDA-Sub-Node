using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace SystemMonitorExample;

/// <summary>
/// Communication implementation for local system resource access.
/// This represents the "connection" to the local operating system APIs.
/// </summary>
public class LocalSystemCommunication : ICommunication
{
    private readonly ILogger<LocalSystemCommunication> _logger;
    private CommunicationState _state = CommunicationState.Disconnected;
    private bool _disposed;

    public LocalSystemCommunication(
        ConnectionSettings? settings = null,
        ILogger<LocalSystemCommunication>? logger = null)
    {
        Settings = settings ?? new ConnectionSettings();
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<LocalSystemCommunication>();
    }

    public ConnectionSettings Settings { get; }

    public CommunicationState State
    {
        get => _state;
        private set
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

    public event EventHandler<ConnectionStateChangedEvent>? StateChanged;

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot connect: communication has been disposed");
            return Task.FromResult(false);
        }

        try
        {
            // Verify we can access system APIs
            // This is our "connection test" - if we can read basic system info, we're connected
            var processorCount = Environment.ProcessorCount;
            var osVersion = Environment.OSVersion;

            _logger.LogDebug(
                "Local system communication connected. Processors: {ProcessorCount}, OS: {OSVersion}",
                processorCount,
                osVersion);

            State = CommunicationState.Connected;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to local system APIs");
            State = CommunicationState.Error;
            return Task.FromResult(false);
        }
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (State == CommunicationState.Disconnected)
        {
            return Task.CompletedTask;
        }

        _logger.LogDebug("Disconnecting from local system communication");
        State = CommunicationState.Disconnected;
        return Task.CompletedTask;
    }

    private void OnStateChanged(CommunicationState previousState, CommunicationState currentState, string? reason = null)
    {
        _logger.LogDebug(
            "Local system communication state changed from {PreviousState} to {CurrentState}",
            previousState,
            currentState);

        var @event = new ConnectionStateChangedEvent(
            DeviceId: "LocalSystem",
            DeviceType: DeviceType.CustomDevice,
            PreviousState: previousState,
            CurrentState: currentState,
            Timestamp: DateTimeOffset.UtcNow)
        {
            Reason = reason
        };

        StateChanged?.Invoke(this, @event);
    }

    public void Dispose()
    {
        if (_disposed) return;

        DisconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
