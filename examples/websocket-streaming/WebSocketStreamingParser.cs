using System.Text;
using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Communication;
using WebSocketCommunication = Weda.SubNode.Core.Communication.WebSocketCommunication;

namespace WebSocketStreamingExample;

/// <summary>
/// WebSocket streaming protocol parser.
/// Handles continuous data streaming from WebSocket connections.
/// Parser owns DeviceConfiguration and handles all mapping logic internally.
///
/// Expected JSON format from server:
/// {
///     "timestamp": 1234567890,
///     "data": {
///         "sensorName1": 123.45,
///         "sensorName2": 67.89
///     }
/// }
/// </summary>
public class WebSocketStreamingParser : IStreamingProtocolParser
{
    private readonly DeviceConfiguration _configuration;
    private readonly WebSocketCommunication _communication;
    private readonly ILogger<WebSocketStreamingParser> _logger;

    private CancellationTokenSource? _streamCts;
    private Task? _streamTask;
    private StreamState _streamState = StreamState.Disconnected;

    public WebSocketStreamingParser(
        DeviceConfiguration configuration,
        WebSocketCommunication communication,
        ILogger<WebSocketStreamingParser> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region IProtocolParserCore Implementation

    public ICommunication Communication => _communication;

    public string ProtocolName => "WebSocket Streaming";

    public IReadOnlyList<string> SupportedDataTypes => ["json", "binary"];

    public bool SupportsBidirectional => true;

    #endregion

    #region IStreamingProtocolParser Implementation

    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;
    public event Action<StreamState>? OnStreamStateChanged;

    public StreamState StreamState
    {
        get => _streamState;
        private set
        {
            if (_streamState != value)
            {
                _streamState = value;
                OnStreamStateChanged?.Invoke(value);
            }
        }
    }

    public async Task StartStreamAsync(CancellationToken cancellationToken = default)
    {
        if (StreamState == StreamState.Connected)
        {
            _logger.LogWarning("Stream is already connected");
            return;
        }

        StreamState = StreamState.Connecting;

        // Connect WebSocket
        if (!await _communication.ConnectAsync(cancellationToken))
        {
            StreamState = StreamState.Error;
            throw new InvalidOperationException("Failed to connect WebSocket");
        }

        _streamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start stream processing in background
        _streamTask = Task.Run(async () =>
        {
            StreamState = StreamState.Connected;

            try
            {
                // Create empty request stream (or could send heartbeats)
                var requests = CreateRequestStream(_streamCts.Token);

                await foreach (var data in _communication.StreamAsync(requests, _streamCts.Token))
                {
                    ProcessReceivedData(data);
                }
            }
            catch (OperationCanceledException) when (_streamCts.Token.IsCancellationRequested)
            {
                _logger.LogInformation("Stream processing cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stream processing");
                StreamState = StreamState.Error;
            }
        }, _streamCts.Token);

        _logger.LogInformation("WebSocket stream started");
    }

    public async Task StopStreamAsync(CancellationToken cancellationToken = default)
    {
        if (StreamState == StreamState.Disconnected)
        {
            return;
        }

        _logger.LogDebug("Stopping WebSocket stream");

        _streamCts?.Cancel();

        if (_streamTask != null)
        {
            try
            {
                await _streamTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Stream task did not complete within timeout");
            }
        }

        await _communication.DisconnectAsync(cancellationToken);

        StreamState = StreamState.Disconnected;
        _logger.LogInformation("WebSocket stream stopped");
    }

    public async Task<bool> SendTelemetryAsync(
        IEnumerable<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default)
    {
        if (StreamState != StreamState.Connected)
        {
            _logger.LogWarning("Cannot send telemetry: stream is not connected");
            return false;
        }

        try
        {
            var payload = new
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                data = measures.ToDictionary(m => m.ResourceId, m => m.Value)
            };

            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);

            return await _communication.SendAsync(bytes, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending telemetry");
            return false;
        }
    }

    public async Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        if (StreamState != StreamState.Connected)
        {
            return Error.Failure("Stream.NotConnected", "Stream is not connected");
        }

        try
        {
            var payload = new
            {
                type = "command",
                command = command.DeviceCmd,
                parameters = command.Parameters
            };

            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);

            if (await _communication.SendAsync(bytes, cancellationToken))
            {
                return "Command sent successfully";
            }

            return Error.Failure("Command.SendFailed", "Failed to send command");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command {CommandName}", command.DeviceCmd);
            return Error.Failure("Command.ExecutionFailed", ex.Message);
        }
    }

    #endregion

    #region Private Methods

    private async IAsyncEnumerable<byte[]> CreateRequestStream(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Send periodic heartbeats to keep connection alive
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            var heartbeat = JsonSerializer.SerializeToUtf8Bytes(new { type = "heartbeat" });
            yield return heartbeat;
        }
    }

    private void ProcessReceivedData(byte[] data)
    {
        try
        {
            var json = Encoding.UTF8.GetString(data);
            _logger.LogTrace("Received data: {Json}", json);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Parse timestamp
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (root.TryGetProperty("timestamp", out var timestampElement))
            {
                if (timestampElement.ValueKind == JsonValueKind.Number)
                {
                    timestamp = timestampElement.GetInt64();
                }
                else if (timestampElement.ValueKind == JsonValueKind.String &&
                         DateTimeOffset.TryParse(timestampElement.GetString(), out var dto))
                {
                    timestamp = dto.ToUnixTimeMilliseconds();
                }
            }

            // Try to parse "sensors" format first (from simulator)
            // Format: { "timestamp": "...", "sensors": { "temp1": { "value": 25.5, "unit": "C", "quality": "good" }, ... } }
            if (root.TryGetProperty("sensors", out var sensorsElement))
            {
                ProcessSensorsFormat(sensorsElement, timestamp);
                return;
            }

            // Fallback to "data" format
            // Format: { "timestamp": 123456789, "data": { "sensorName": 123.45, ... } }
            if (root.TryGetProperty("data", out var dataElement))
            {
                ProcessDataFormat(dataElement, timestamp);
                return;
            }

            _logger.LogWarning("Received data has neither 'sensors' nor 'data' property");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received data");
        }
    }

    private void ProcessSensorsFormat(JsonElement sensorsElement, long timestamp)
    {
        var measures = new List<TelemetryMeasure>();

        // Build sensor lookup from configuration (enabled sensors only)
        var sensorLookup = _configuration.Sensors
            .Where(s => s.Config.Enabled)
            .ToDictionary(s => s.Name, s => s.ResourceId);

        foreach (var property in sensorsElement.EnumerateObject())
        {
            var sensorName = property.Name;

            // Check if sensor is enabled in configuration
            if (!sensorLookup.TryGetValue(sensorName, out var resourceId))
            {
                _logger.LogTrace("Sensor {SensorName} not found in configuration", sensorName);
                continue;
            }

            // Extract value from nested object { "value": ..., "unit": ..., "quality": ... }
            double value = 0;
            if (property.Value.ValueKind == JsonValueKind.Object &&
                property.Value.TryGetProperty("value", out var valueElement))
            {
                value = valueElement.ValueKind switch
                {
                    JsonValueKind.Number => valueElement.GetDouble(),
                    JsonValueKind.String when double.TryParse(valueElement.GetString(), out var d) => d,
                    _ => 0.0
                };
            }

            measures.Add(new TelemetryMeasure
            {
                ResourceId = resourceId,
                Value = value,
                Timestamp = timestamp
            });

            _logger.LogTrace(
                "Parsed sensor data: {SensorName} = {Value} (ResourceId: {ResourceId})",
                sensorName, value, resourceId);
        }

        // Raise event with parsed measures
        if (measures.Count > 0)
        {
            OnTelemetryReceived?.Invoke(measures);
        }
    }

    private void ProcessDataFormat(JsonElement dataElement, long timestamp)
    {
        var measures = new List<TelemetryMeasure>();

        // Build sensor lookup from configuration (enabled sensors only)
        var sensorLookup = _configuration.Sensors
            .Where(s => s.Config.Enabled)
            .ToDictionary(s => s.Name, s => s.ResourceId);

        foreach (var property in dataElement.EnumerateObject())
        {
            var sensorName = property.Name;

            // Check if sensor is enabled in configuration
            if (!sensorLookup.TryGetValue(sensorName, out var resourceId))
            {
                _logger.LogTrace("Sensor {SensorName} not found in configuration", sensorName);
                continue;
            }

            // Extract value directly
            double value = property.Value.ValueKind switch
            {
                JsonValueKind.Number => property.Value.GetDouble(),
                JsonValueKind.String when double.TryParse(property.Value.GetString(), out var d) => d,
                JsonValueKind.True => 1.0,
                JsonValueKind.False => 0.0,
                _ => 0.0
            };

            measures.Add(new TelemetryMeasure
            {
                ResourceId = resourceId,
                Value = value,
                Timestamp = timestamp
            });

            _logger.LogTrace(
                "Parsed streaming data: {SensorName} = {Value} (ResourceId: {ResourceId})",
                sensorName, value, resourceId);
        }

        // Raise event with parsed measures
        if (measures.Count > 0)
        {
            OnTelemetryReceived?.Invoke(measures);
        }
    }

    #endregion
}
