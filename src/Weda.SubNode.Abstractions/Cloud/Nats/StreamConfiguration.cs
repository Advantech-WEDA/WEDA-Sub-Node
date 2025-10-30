using NATS.Client.JetStream.Models;

namespace Weda.SubNode.Abstractions.Cloud.Nats;

/// <summary>
/// Stream configuration with defaults
/// </summary>
public record StreamConfiguration : StreamConfig
{
    public static StreamConfiguration Default => new StreamConfiguration();

    public StreamConfiguration()
    {
        Description = "Weda SubNode Service Stream";
        Retention = StreamConfigRetention.Limits;
        MaxAge = TimeSpan.FromDays(7);
        Storage = StreamConfigStorage.File;
        NumReplicas = 1;
    }
}
