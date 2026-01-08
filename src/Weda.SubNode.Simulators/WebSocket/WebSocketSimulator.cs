using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Weda.SubNode.Simulators.WebSocket;

/// <summary>
/// WebSocket simulator server
/// Simulates streaming sensor data over WebSocket connections
/// </summary>
public class WebSocketSimulator : IDisposable
{
    private readonly ILogger<WebSocketSimulator> _logger;
    private readonly WebSocketSimulatorConfiguration _configuration;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private Task? _broadcastTask;
    private bool _disposed;

    // Connected clients
    private readonly ConcurrentDictionary<string, System.Net.WebSockets.WebSocket> _clients = new();

    // Sensor simulation state
    private readonly Dictionary<string, double> _sensorCurrentValues = new();
    private readonly Dictionary<string, string> _sensorUnits = new();
    private readonly object _sensorLock = new();

    // JSON serialization options
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public WebSocketSimulator(
        WebSocketSimulatorConfiguration configuration,
        ILogger<WebSocketSimulator>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<WebSocketSimulator>();

        InitializeSensorValues();
    }

    /// <summary>
    /// Number of currently connected clients
    /// </summary>
    public int ConnectedClients => _clients.Count;

    /// <summary>
    /// Get current sensor values
    /// </summary>
    public IReadOnlyDictionary<string, double> SensorValues
    {
        get
        {
            lock (_sensorLock)
            {
                return new Dictionary<string, double>(_sensorCurrentValues);
            }
        }
    }

    private void InitializeSensorValues()
    {
        lock (_sensorLock)
        {
            foreach (var sensor in _configuration.Sensors)
            {
                // Get default parameters for sensor type
                var defaults = WebSocketSensorDefaults.GetDefaults(sensor.Type);
                var simParams = sensor.SimulationParams;

                // Apply defaults if not configured
                if (simParams.MinValue == 0 && simParams.MaxValue == 0)
                {
                    simParams.MinValue = defaults.MinValue;
                    simParams.MaxValue = defaults.MaxValue;
                }
                if (simParams.ChangeRate == 0) simParams.ChangeRate = defaults.ChangeRate;
                if (simParams.NoiseLevel == 0) simParams.NoiseLevel = defaults.NoiseLevel;

                // Set unit
                var unit = sensor.Unit ?? WebSocketSensorDefaults.GetDefaultUnit(sensor.Type);
                _sensorUnits[sensor.Name] = unit;

                // Set initial value
                var initialValue = simParams.InitialValue ??
                    Random.Shared.NextDouble() * (simParams.MaxValue - simParams.MinValue) + simParams.MinValue;
                _sensorCurrentValues[sensor.Name] = initialValue;

                _logger.LogInformation(
                    "Initialized sensor {Name} ({Type}): InitialValue={Value:F2} {Unit}",
                    sensor.Name, sensor.Type, initialValue, unit);
            }
        }
    }

    /// <summary>
    /// Start the WebSocket server
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener != null)
            throw new InvalidOperationException("Simulator already started");

        var server = _configuration.Server;
        var prefix = $"http://{server.Host}:{server.Port}/";

        _logger.LogInformation(
            "Starting WebSocket Simulator on ws://{Host}:{Port}{Path}",
            server.Host, server.Port, server.Path);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new HttpListener();
        _listener.Prefixes.Add(prefix);
        _listener.Start();

        _listenerTask = Task.Run(() => AcceptClientsAsync(_cts.Token), _cts.Token);
        _broadcastTask = Task.Run(() => BroadcastSensorDataAsync(_cts.Token), _cts.Token);

        _logger.LogInformation("WebSocket Simulator started successfully");
        _logger.LogInformation("────────────────────────────────────────────────────────");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop the WebSocket server
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping WebSocket Simulator");

        _cts?.Cancel();

        // Close all client connections
        foreach (var client in _clients.Values)
        {
            try
            {
                if (client.State == WebSocketState.Open)
                {
                    await client.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Server shutting down",
                        CancellationToken.None);
                }
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }
        _clients.Clear();

        _listener?.Stop();

        if (_listenerTask != null)
        {
            try { await _listenerTask; }
            catch (OperationCanceledException) { }
        }

        if (_broadcastTask != null)
        {
            try { await _broadcastTask; }
            catch (OperationCanceledException) { }
        }

        _logger.LogInformation("WebSocket Simulator stopped");
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener!.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    // Check max connections
                    if (_clients.Count >= _configuration.Server.MaxConnections)
                    {
                        _logger.LogWarning(
                            "Max connections ({Max}) reached, rejecting client",
                            _configuration.Server.MaxConnections);
                        context.Response.StatusCode = 503;
                        context.Response.Close();
                        continue;
                    }

                    _ = HandleWebSocketAsync(context, cancellationToken);
                }
                else
                {
                    // Return simple status page for HTTP requests
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";
                    var status = JsonSerializer.Serialize(new
                    {
                        status = "running",
                        connectedClients = _clients.Count,
                        sensors = _configuration.Sensors.Select(s => s.Name).ToList()
                    }, JsonOptions);
                    var bytes = Encoding.UTF8.GetBytes(status);
                    await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
                    context.Response.Close();
                }
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting client");
            }
        }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var clientId = Guid.NewGuid().ToString("N")[..8];
        var remoteEndPoint = context.Request.RemoteEndPoint;

        _logger.LogInformation(
            "WebSocket connection request from {RemoteEndPoint} (ClientId: {ClientId})",
            remoteEndPoint, clientId);

        System.Net.WebSockets.WebSocket webSocket;
        try
        {
            var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
            webSocket = wsContext.WebSocket;
            _clients.TryAdd(clientId, webSocket);

            _logger.LogInformation(
                "WebSocket connected: {ClientId} (Total: {Count})",
                clientId, _clients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to accept WebSocket connection");
            context.Response.StatusCode = 500;
            context.Response.Close();
            return;
        }

        try
        {
            // Handle incoming messages
            var receiveBuffer = new byte[4096];
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(receiveBuffer),
                        cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Client {ClientId} initiated close", clientId);
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Client closed",
                            CancellationToken.None);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                        await HandleClientMessageAsync(clientId, webSocket, message, cancellationToken);
                    }
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    _logger.LogWarning("Client {ClientId} disconnected unexpectedly", clientId);
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Connection with {ClientId} cancelled", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket connection {ClientId}", clientId);
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
            webSocket.Dispose();

            _logger.LogInformation(
                "WebSocket disconnected: {ClientId} (Remaining: {Count})",
                clientId, _clients.Count);
        }
    }

    private async Task HandleClientMessageAsync(
        string clientId,
        System.Net.WebSockets.WebSocket webSocket,
        string message,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Received from {ClientId}: {Message}", clientId, message);

        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (root.TryGetProperty("type", out var typeElement))
            {
                var messageType = typeElement.GetString();

                switch (messageType)
                {
                    case "heartbeat":
                        // Echo heartbeat
                        var response = JsonSerializer.Serialize(new
                        {
                            type = "heartbeat_ack",
                            timestamp = DateTimeOffset.UtcNow
                        }, JsonOptions);
                        await SendToClientAsync(webSocket, response, cancellationToken);
                        break;

                    case "command":
                        // Handle command
                        var ack = JsonSerializer.Serialize(new
                        {
                            type = "command_ack",
                            received = message,
                            timestamp = DateTimeOffset.UtcNow
                        }, JsonOptions);
                        await SendToClientAsync(webSocket, ack, cancellationToken);
                        break;

                    default:
                        _logger.LogDebug("Unknown message type: {Type}", messageType);
                        break;
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON message, just acknowledge
            var ack = JsonSerializer.Serialize(new
            {
                type = "ack",
                received = message,
                timestamp = DateTimeOffset.UtcNow
            }, JsonOptions);
            await SendToClientAsync(webSocket, ack, cancellationToken);
        }
    }

    private async Task BroadcastSensorDataAsync(CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_configuration.Simulation.BroadcastIntervalMs, cancellationToken);

                // Update sensor values if enabled
                if (_configuration.Simulation.EnableValueChanges)
                {
                    UpdateSensorValues(startTime);
                }

                // Build sensor data message
                var sensorData = BuildSensorDataMessage();
                var json = JsonSerializer.Serialize(sensorData, JsonOptions);

                // Broadcast to all connected clients
                var disconnected = new List<string>();
                foreach (var (clientId, webSocket) in _clients)
                {
                    if (webSocket.State != WebSocketState.Open)
                    {
                        disconnected.Add(clientId);
                        continue;
                    }

                    try
                    {
                        await SendToClientAsync(webSocket, json, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send to client {ClientId}", clientId);
                        disconnected.Add(clientId);
                    }
                }

                // Remove disconnected clients
                foreach (var clientId in disconnected)
                {
                    _clients.TryRemove(clientId, out _);
                }

                if (_clients.Count > 0)
                {
                    _logger.LogDebug("Broadcast sensor data to {Count} clients", _clients.Count);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in broadcast loop");
            }
        }
    }

    private void UpdateSensorValues(DateTimeOffset startTime)
    {
        lock (_sensorLock)
        {
            foreach (var sensor in _configuration.Sensors)
            {
                var simParams = sensor.SimulationParams;
                var currentValue = _sensorCurrentValues[sensor.Name];

                // Calculate change with optional sine wave pattern for some sensors
                var elapsed = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
                var direction = Random.Shared.NextDouble() * 2 - 1;
                var change = direction * simParams.ChangeRate;

                // Add sine wave pattern for temperature
                if (sensor.Type == WebSocketSensorType.Temperature)
                {
                    change += Math.Sin(elapsed * 0.05) * 0.2;
                }

                // Add noise
                var noise = (Random.Shared.NextDouble() * 2 - 1) * simParams.NoiseLevel;

                // Apply change
                var newValue = currentValue + change + noise;

                // Clamp to min/max
                newValue = Math.Clamp(newValue, simParams.MinValue, simParams.MaxValue);

                _sensorCurrentValues[sensor.Name] = newValue;
            }
        }
    }

    private object BuildSensorDataMessage()
    {
        var sensors = new Dictionary<string, object>();

        lock (_sensorLock)
        {
            foreach (var sensor in _configuration.Sensors)
            {
                var value = _sensorCurrentValues[sensor.Name];
                var unit = _sensorUnits[sensor.Name];

                // Determine quality based on probability
                var quality = "good";
                if (Random.Shared.NextDouble() < sensor.SimulationParams.UncertainProbability)
                {
                    quality = "uncertain";
                }

                sensors[sensor.Name] = new
                {
                    value = Math.Round(value, 2),
                    unit,
                    quality
                };
            }
        }

        if (_configuration.Simulation.IncludeTimestamp)
        {
            return new
            {
                timestamp = DateTimeOffset.UtcNow,
                sensors
            };
        }

        return new { sensors };
    }

    private static async Task SendToClientAsync(
        System.Net.WebSockets.WebSocket webSocket,
        string message,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;

        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
