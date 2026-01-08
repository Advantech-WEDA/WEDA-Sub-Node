namespace Weda.SubNode.Abstractions.Communication;

/// <summary>
/// Non-generic Pub/Sub communication interface for backward compatibility.
/// This is equivalent to IPubSubCommunication&lt;byte[]&gt;.
/// Suitable for MQTT, NATS, RabbitMQ, etc.
/// </summary>
public interface IPubSub : IPubSubCommunication<byte[]>
{
}
