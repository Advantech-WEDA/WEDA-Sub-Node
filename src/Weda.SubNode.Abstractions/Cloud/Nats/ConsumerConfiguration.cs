using NATS.Client.JetStream.Models;

namespace Weda.SubNode.Abstractions.Cloud.Nats;

/// <summary>
/// Consumer configuration with defaults
/// </summary>
public record ConsumerConfiguration : ConsumerConfig
{
    public static ConsumerConfiguration Default => new ConsumerConfiguration();

    public ConsumerConfiguration()
    {
        AckPolicy = ConsumerConfigAckPolicy.Explicit;
        MaxAckPending = -1;
    }
}
