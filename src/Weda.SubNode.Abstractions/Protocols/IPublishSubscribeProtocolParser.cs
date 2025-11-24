using ErrorOr;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Protocols;

/// <summary>
/// Publish-Subscribe pattern protocol parser.
/// Used for asynchronous protocols where data is pushed to device via subscriptions.
/// Examples: MQTT, NATS, AMQP, Kafka, WebSocket
/// </summary>
public interface IPublishSubscribeProtocolParser : IProtocolParserCore
{
    /// <summary>
    /// Subscribe to sensor data (asynchronous push).
    /// Parser subscribes to topics/channels and invokes callback when data arrives.
    /// </summary>
    /// <param name="sensorMapping">Mapping from protocol fields to ResourceIds</param>
    /// <param name="callback">Callback function invoked when sensor data arrives</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Subscription task (completes when subscription is established)</returns>
    Task SubscribeToSensorDataAsync(
        SensorMapping sensorMapping,
        Action<List<TelemetryMeasure>> callback,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribe from sensor data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UnsubscribeFromSensorDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish command to device (fire-and-forget, no response confirmation).
    /// </summary>
    /// <param name="command">Device command to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to command responses (if protocol supports bidirectional command flow).
    /// </summary>
    /// <param name="callback">Callback function invoked when command response arrives</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeToCommandResponseAsync(
        Action<ErrorOr<object>> callback,
        CancellationToken cancellationToken = default);
}
