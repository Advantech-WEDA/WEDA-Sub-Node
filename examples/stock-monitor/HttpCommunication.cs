using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace StockMonitor;

/// <summary>
/// HTTP-based communication implementation for TWSE stock API.
/// Wraps TwseStockClient and implements ICommunication for device integration.
/// </summary>
public class HttpCommunication : ICommunication
{
    private readonly TwseStockClient _stockClient;
    private readonly ILogger<HttpCommunication> _logger;
    private CommunicationState _state = CommunicationState.Disconnected;

    private const string DefaultDeviceId = "http-communication";

    public TwseStockClient StockClient => _stockClient;
    public ConnectionSettings Settings { get; }
    public CommunicationState State => _state;
    public bool IsConnected => _state == CommunicationState.Connected;

    public event EventHandler<ConnectionStateChangedEvent>? StateChanged;

    public HttpCommunication(
        TwseStockClient stockClient,
        ConnectionSettings? settings = null,
        ILogger<HttpCommunication>? logger = null)
    {
        _stockClient = stockClient ?? throw new ArgumentNullException(nameof(stockClient));
        Settings = settings ?? new ConnectionSettings();
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<HttpCommunication>();
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        // HTTP is stateless - "connect" just marks us as ready
        var oldState = _state;
        _state = CommunicationState.Connected;

        if (oldState != _state)
        {
            _logger.LogDebug("HTTP communication state changed: {OldState} -> {NewState}", oldState, _state);
            StateChanged?.Invoke(this, new ConnectionStateChangedEvent(
                DefaultDeviceId, DeviceType.CustomDevice, oldState, _state, DateTimeOffset.UtcNow));
        }

        return Task.FromResult(true);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var oldState = _state;
        _state = CommunicationState.Disconnected;

        if (oldState != _state)
        {
            _logger.LogDebug("HTTP communication state changed: {OldState} -> {NewState}", oldState, _state);
            StateChanged?.Invoke(this, new ConnectionStateChangedEvent(
                DefaultDeviceId, DeviceType.CustomDevice, oldState, _state, DateTimeOffset.UtcNow));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Fetches stock quotes via the injected TwseStockClient.
    /// </summary>
    public async Task<TwseStockResponse?> GetStockQuotesAsync(
        IEnumerable<string> stockCodes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _stockClient.GetStockQuotesAsync(stockCodes, cancellationToken);
        }
        catch (HttpRequestException)
        {
            SetErrorState();
            throw;
        }
    }

    private void SetErrorState()
    {
        var oldState = _state;
        _state = CommunicationState.Error;

        if (oldState != _state)
        {
            StateChanged?.Invoke(this, new ConnectionStateChangedEvent(
                DefaultDeviceId, DeviceType.CustomDevice, oldState, _state, DateTimeOffset.UtcNow));
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
