using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Abstractions.Communication;

/// <summary>
/// Non-generic Message Broker communication interface for backward compatibility.
/// This is equivalent to IMessageBrokerCommunication&lt;byte[]&gt;.
/// Suitable for MQTT, NATS, RabbitMQ, etc.
/// </summary>
public interface IMessageBroker : IMessageBrokerCommunication<byte[]>
{
}
