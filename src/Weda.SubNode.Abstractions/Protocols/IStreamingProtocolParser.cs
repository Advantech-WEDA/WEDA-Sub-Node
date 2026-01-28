using ErrorOr;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Protocols;

/// <summary>
/// Streaming pattern protocol parser.
/// Used for bidirectional streaming protocols where data flows continuously.
/// Examples: WebSocket, gRPC streaming, SSE (Server-Sent Events)
///
/// Parser is responsible for:
/// - Communication with the streaming endpoint
/// - Protocol-specific parsing logic
/// - Mapping protocol fields to ResourceIds (internally using DeviceConfiguration)
/// - Managing the stream lifecycle
/// </summary>
public interface IStreamingProtocolParser : IProtocolParserCore
{
    /// <summary>
    /// Event raised when telemetry data is received from stream.
    /// Parser internally handles all mapping logic using DeviceConfiguration.
    /// </summary>
    event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    /// <summary>
    /// Event raised when the stream connection state changes.
    /// </summary>
    event Action<StreamState>? OnStreamStateChanged;

    /// <summary>
    /// Current stream state.
    /// </summary>
    StreamState StreamState { get; }

    /// <summary>
    /// Start streaming and receive telemetry data continuously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when stream is established</returns>
    Task StartStreamAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop streaming and clean up resources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StopStreamAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Send telemetry data through the stream (if bidirectional).
    /// </summary>
    /// <param name="measures">Telemetry measures to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if send was successful</returns>
    Task<bool> SendTelemetryAsync(
        IEnumerable<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute command through the stream.
    /// </summary>
    /// <param name="command">Device command to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command execution result or error</returns>
    Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stream connection state.
/// </summary>
public enum StreamState
{
    /// <summary>
    /// Stream is disconnected/closed.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Stream is connecting.
    /// </summary>
    Connecting,

    /// <summary>
    /// Stream is connected and active.
    /// </summary>
    Connected,

    /// <summary>
    /// Stream is reconnecting after a disconnection.
    /// </summary>
    Reconnecting,

    /// <summary>
    /// Stream encountered an error.
    /// </summary>
    Error
}
