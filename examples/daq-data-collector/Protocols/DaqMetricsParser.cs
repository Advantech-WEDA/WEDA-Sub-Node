using daq_data_collector.Communication;

using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace daq_data_collector.Protocols;

/// <summary>
/// Protocol parser for DAQ streaming pattern.
/// Implements IStreamingProtocolParser to manage stream lifecycle and
/// map raw DAQ frames to telemetry measures via SensorCache.
///
/// Consumes the DaqCommunication stream, emits raw payload to SensorCache,
/// which then triggers telemetry pipeline (Transform → Filter → Send).
/// Implements frame decimation to map the 2 Hz hardware frame push rate
/// to a configurable effective update rate (default 1 Hz with DecimationFactor=2).
/// </summary>
public class DaqMetricsParser : IStreamingProtocolParser
{
    private readonly DaqCommunication _communication;
    private readonly ILogger<DaqMetricsParser> _logger;
    private readonly int _decimationFactor;
    private readonly Sensor? _rawSensor;  // Raw vibration sensor with ResourceId for telemetry emission
    private StreamState _streamState = StreamState.Disconnected;
    private volatile int _frameCounter = 0;
    private Task? _streamTask;
    private CancellationTokenSource? _streamCts;

    public ICommunication Communication => _communication;

    public StreamState StreamState => _streamState;

    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;
    public event Action<StreamState>? OnStreamStateChanged;

    public DaqMetricsParser(
        DaqCommunication communication,
        ILogger<DaqMetricsParser> logger,
        int decimationFactor = 2,
        Sensor? rawSensor = null)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (decimationFactor <= 0)
            throw new ArgumentException("DecimationFactor must be a positive integer.", nameof(decimationFactor));

        _decimationFactor = decimationFactor;
        _rawSensor = rawSensor;  // Raw sensor with ResourceId for telemetry emission
    }

    /// <summary>
    /// Start consuming the DaqCommunication stream and emit raw measures to SensorCache.
    /// Uses the injected raw sensor ResourceId to identify telemetry payloads.
    /// Initializes decimation counter and launches stream consumer background task.
    /// </summary>
    public async Task StartStreamAsync(CancellationToken cancellationToken = default)
    {
        if (_streamState != StreamState.Disconnected)
        {
            _logger.LogWarning("Stream is already initialized (state={state}). Skipping StartStreamAsync.", _streamState);
            return;
        }

        try
        {
            _logger.LogInformation($"Starting DaqMetricsParser stream with DecimationFactor={_decimationFactor}...");

            if (_rawSensor != null)
            {
                _logger.LogInformation($"Raw sensor ResourceId: {_rawSensor.ResourceId}");
            }
            else
            {
                _logger.LogWarning("Raw sensor not provided, using default ResourceId pattern");
            }

            // Initialize decimation counter
            _frameCounter = 0;

            // Create cancellation token source for stream lifecycle
            _streamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Update state and notify
            SetStreamState(StreamState.Connecting);

            // Connect communication layer
            await _communication.ConnectAsync(_streamCts.Token).ConfigureAwait(false);
            SetStreamState(StreamState.Connected);

            _logger.LogInformation("DaqCommunication connected successfully.");

            // Launch stream consumer background task (fire-and-forget)
            _streamTask = RunStreamConsumerAsync(_streamCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting stream.");
            SetStreamState(StreamState.Error);
            _streamCts?.Dispose();
            _streamCts = null;
            throw;
        }
    }

    /// <summary>
    /// Stop consuming the stream and clean up resources.
    /// </summary>
    public async Task StopStreamAsync(CancellationToken cancellationToken = default)
    {
        if (_streamState == StreamState.Disconnected)
        {
            _logger.LogWarning("Stream is already disconnected. Skipping StopStreamAsync.");
            return;
        }

        try
        {
            _logger.LogInformation("Stopping DaqMetricsParser stream...");

            SetStreamState(StreamState.Connected);  // Transition toward disconnection

            // Signal stream task cancellation
            _streamCts?.Cancel();

            // Wait for stream task to complete (with timeout)
            if (_streamTask != null)
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _streamTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Stream consumer task did not complete within timeout.");
                }
            }

            // Disconnect communication
            await _communication.DisconnectAsync(cancellationToken).ConfigureAwait(false);

            SetStreamState(StreamState.Disconnected);
            _logger.LogInformation("DaqMetricsParser stream stopped successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping stream.");
            SetStreamState(StreamState.Error);
            throw;
        }
        finally
        {
            _streamCts?.Dispose();
            _streamCts = null;
            _streamTask = null;
        }
    }

    /// <summary>
    /// Background task that consumes the DaqCommunication stream and emits raw telemetry measures.
    /// Implements frame decimation: increments counter and only emits when counter % DecimationFactor == 0.
    /// </summary>
    private async Task RunStreamConsumerAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Empty request stream for one-way streaming (no requests from device → cloud)
            var requestStream = Enumerable.Empty<object>().ToAsyncEnumerable();

            await foreach (var payload in _communication.StreamAsync(requestStream, cancellationToken).ConfigureAwait(false))
            {
                // Increment frame counter
                _frameCounter++;

                // Only emit telemetry when decimation condition is met
                if (_frameCounter % _decimationFactor == 0)
                {
                    _logger.LogInformation("Emitting raw telemetry (frame {frame}, decimation {factor}).", _frameCounter, _decimationFactor);

                    var rawMeasure = new TelemetryMeasure
                    {
                        ResourceId = _rawSensor?.ResourceId ?? "daqraw:vibration:payload",  // Use injected sensor ResourceId or default
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Value = payload?.ToString() ?? string.Empty,
                        Metadata = null
                    };

                    OnTelemetryReceived?.Invoke(new List<TelemetryMeasure> { rawMeasure });
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stream consumer task cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in stream consumer task.");
            SetStreamState(StreamState.Error);
        }
    }

    /// <summary>
    /// Helper to update stream state and notify listeners.
    /// </summary>
    private void SetStreamState(StreamState newState)
    {
        if (_streamState != newState)
        {
            _streamState = newState;
            OnStreamStateChanged?.Invoke(_streamState);
            _logger.LogInformation("Stream state changed to: {state}", _streamState);
        }
    }

    /// <summary>
    /// Send telemetry data through the stream (bidirectional comms, if supported).
    /// </summary>
    public Task<bool> SendTelemetryAsync(
        IEnumerable<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "DaqMetricsParser.SendTelemetryAsync: bidirectional streaming not yet implemented.");
    }

    /// <summary>
    /// Execute device command through the stream.
    /// </summary>
    public Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "DaqMetricsParser.ExecuteCommandAsync: command execution not yet implemented.");
    }
}
