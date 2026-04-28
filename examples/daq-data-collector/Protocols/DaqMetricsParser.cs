using daq_data_collector.Communication;

using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
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
/// </summary>
public class DaqMetricsParser : IStreamingProtocolParser
{
    private readonly DaqCommunication _communication;
    private readonly ILogger<DaqMetricsParser> _logger;
    private StreamState _streamState = StreamState.Disconnected;

    public ICommunication Communication => _communication;

    public StreamState StreamState => _streamState;

    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;
    public event Action<StreamState>? OnStreamStateChanged;

    public DaqMetricsParser(
        DaqCommunication communication,
        ILogger<DaqMetricsParser> logger)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Start consuming the DaqCommunication stream and emit raw measures to SensorCache.
    /// </summary>
    public Task StartStreamAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "DaqMetricsParser.StartStreamAsync: start streaming and emit raw measures not yet implemented.");
    }

    /// <summary>
    /// Stop consuming the stream and clean up resources.
    /// </summary>
    public Task StopStreamAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "DaqMetricsParser.StopStreamAsync: stop streaming and close connection not yet implemented.");
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
