using System.Text.Json.Serialization;
using NATS.Client.Core;
using NATS.Net;

namespace Weda.SubNode.Abstractions.Cloud.Nats;

/// <summary>
/// Settings for a named NATS connection
/// </summary>
public record NatsConnectionSettings
{
    public static readonly NatsConnectionSettings Default = new();

    /// <summary>
    /// URL for the NATS connection
    /// </summary>
    public string Url { get; set; } = "localhost";

    /// <summary>
    /// Path to the credential file for authentication
    /// </summary>
    public string CredFile { get; set; } = "";

    /// <summary>
    /// Connection name for identification
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Serializer type name for JSON configuration (e.g., "json", "protobuf", "default")
    /// </summary>
    public string SerializerType { get; set; } = "json";

    /// <summary>
    /// Serializer registry for the NATS connection
    /// </summary>
    [JsonIgnore]
    public INatsSerializerRegistry NatsSerializerRegistry { get; set; } = NatsClientDefaultSerializerRegistry.Default;
}
