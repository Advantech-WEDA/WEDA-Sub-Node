using System.Text.Json.Serialization;

namespace Receiver.Models;

record TelemetryEnvelope
{
    [JsonPropertyName("data")] public TelemetryData? Data { get; init; }
}
