using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Base class for Streaming communication pattern devices.
/// Framework handles ReadTelemetryAsync and StartBackgroundTasksAsync internally.
/// User only needs to provide an IStreamingProtocolParser implementation.
///
/// Use this class for protocols like: WebSocket, gRPC streaming, SSE
///
/// Inheritance hierarchy example:
/// MyStreamingDevice -> WebSocketStreamingDevice -> StreamingDeviceBase -> DeviceBase
/// </summary>
public class StreamingDeviceBase : DeviceBase
{
    /// <summary>
    /// The protocol parser for this device. Protected to allow subclass access.
    /// </summary>
    protected readonly IStreamingProtocolParser _parser;
    private readonly ConcurrentDictionary<string, TelemetryMeasure> _latestData = new();
    private CancellationTokenSource? _backgroundTasksCts;
    private Task? _streamTask;
    private Task? _healthTask;

    /// <summary>
    /// Initializes a new instance of StreamingDeviceBase.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration.</param>
    /// <param name="parser">Streaming protocol parser (Parser owns Communication).</param>
    public StreamingDeviceBase(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IStreamingProtocolParser parser)
        : base(context, configuration, parser.Communication)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        _logger.LogDebug(
            "StreamingDeviceBase initialized with {ParserType}",
            _parser.GetType().Name);
    }

    /// <summary>
    /// Current stream state.
    /// </summary>
    public StreamState StreamState => _parser.StreamState;

    /// <summary>
    /// Reads telemetry from the device cache.
    /// Returns the latest received data from streaming.
    /// Framework implementation - returns cached data from OnTelemetryReceived events.
    /// Sealed to prevent subclasses from overriding framework logic.
    /// </summary>
    public sealed override Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var measures = _latestData.Values.ToList();

        if (measures.Count > 0)
        {
            _logger.LogDebug("Returning {Count} cached telemetry measures from stream", measures.Count);
            RaiseDataReceived(measures);
        }

        return Task.FromResult(measures);
    }

    /// <summary>
    /// Executes a command on the device using the parser.
    /// </summary>
    public override async Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing command {CommandName} on device {DeviceId}", command.DeviceCmd, DeviceId);

        var result = await _parser.ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("Command execution failed: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Starts background tasks for streaming and health reporting.
    /// Framework implementation with automatic stream handling.
    /// Sealed to prevent subclasses from overriding framework logic.
    /// </summary>
    protected sealed override Task StartBackgroundTasksAsync(CancellationToken cancellationToken)
    {
        _backgroundTasksCts = new CancellationTokenSource();
        var cts = _backgroundTasksCts.Token;

        // Subscribe to parser's events
        _parser.OnTelemetryReceived += OnTelemetryReceived;
        _parser.OnStreamStateChanged += OnStreamStateChanged;

        _streamTask = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("Starting stream connection");

                // Start streaming
                await _parser.StartStreamAsync(cts);

                _logger.LogInformation("Stream connection established");

                // Keep task alive to maintain stream
                await Task.Delay(Timeout.Infinite, cts);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                _logger.LogInformation("Stream task cancelled for device {DeviceId}", DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stream task for device {DeviceId}", DeviceId);
            }
            finally
            {
                // Clean up stream
                try
                {
                    await _parser.StopStreamAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping stream");
                }
            }
        }, cts);

        _healthTask = Task.Run(async () =>
        {
            var period = Configuration.Periods.ReportHealth;
            _logger.LogDebug("Starting health reporting task with period {Period}ms", period);

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    await ReportHealthAsync(cts);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in health reporting task for device {DeviceId}", DeviceId);
                }

                await Task.Delay(period, cts);
            }
        }, cts);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles telemetry data received from stream.
    /// Updates cache and sends telemetry through pipeline.
    /// </summary>
    private void OnTelemetryReceived(List<TelemetryMeasure> measures)
    {
        try
        {
            // Update cache with latest values
            foreach (var measure in measures)
            {
                _latestData.AddOrUpdate(measure.ResourceId, measure, (_, _) => measure);
            }

            _logger.LogDebug("Received {Count} telemetry measures from stream", measures.Count);

            // Raise data received event
            RaiseDataReceived(measures);

            // Record telemetry in health monitor
            _orchestrator.HealthMonitor.RecordTelemetryReadDuration(TimeSpan.Zero);

            // Send telemetry through pipeline (fire-and-forget for streaming model)
            _ = SendTelemetryAsync(measures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling received telemetry for device {DeviceId}", DeviceId);
        }
    }

    /// <summary>
    /// Handles stream state changes.
    /// </summary>
    private void OnStreamStateChanged(StreamState state)
    {
        _logger.LogInformation(
            "Stream state changed to {State} for device {DeviceId}",
            state, DeviceId);

        // Could trigger reconnection logic here if needed
        if (state == StreamState.Error || state == StreamState.Disconnected)
        {
            _logger.LogWarning(
                "Stream disconnected for device {DeviceId}, may need reconnection",
                DeviceId);
        }
    }
}
