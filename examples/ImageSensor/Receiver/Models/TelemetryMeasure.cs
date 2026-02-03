using System.Text.Json;
using System.Text.Json.Serialization;

namespace Receiver.Models;

record TelemetryMeasure
{
    [JsonPropertyName("sensorId")] public string SensorId { get; init; } = "";
    [JsonPropertyName("value")] public object? Value { get; init; }
    [JsonPropertyName("timestamp")] public long Timestamp { get; init; }
    [JsonPropertyName("metadata")] public Dictionary<string, JsonElement>? Metadata { get; init; }
}
