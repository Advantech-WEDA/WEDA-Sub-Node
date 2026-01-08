using ErrorOr;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Protocols;

/// <summary>
/// Pub/Sub pattern protocol parser.
/// Used for asynchronous protocols where data is pushed to device via subscriptions.
/// Examples: MQTT, NATS, AMQP, Kafka, WebSocket
///
/// Parser is responsible for:
/// - Communication with the message broker
/// - Protocol-specific parsing logic
/// - Mapping protocol fields to ResourceIds (internally using DeviceConfiguration)
/// </summary>
public interface IPubSubProtocolParser : IProtocolParserCore
{
    /// <summary>
    /// Event raised when telemetry data is received from subscription.
    /// Parser internally handles all mapping logic using DeviceConfiguration.
    /// </summary>
    event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    /// <summary>
    /// Start subscription to receive telemetry data.
    /// Parser subscribes to topics/channels and raises OnTelemetryReceived when data arrives.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when subscription is established</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop subscription and clean up resources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute command on device (publish to command topic).
    /// </summary>
    /// <param name="command">Device command to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command execution result or error</returns>
    Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default);
}
