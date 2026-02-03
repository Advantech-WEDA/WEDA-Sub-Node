using System.Text.Json.Serialization;

namespace Receiver.Models;

record TelemetryData
{
    [JsonPropertyName("measures")] public List<TelemetryMeasure>? Measures { get; init; }
}
