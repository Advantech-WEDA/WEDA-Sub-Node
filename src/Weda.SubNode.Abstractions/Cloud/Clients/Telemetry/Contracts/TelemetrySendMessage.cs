using System.Text.Json;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Cloud.Clients.Common;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;

/// <summary>
/// Telemetry send request
/// </summary>
public class TelemetrySendMessage : Message<TelemetryDataDto>
{
    /// <summary>
    /// Create a new telemetry send request with auto-generated audit fields
    /// </summary>
    public static TelemetrySendMessage Create(TelemetryDataDto data)
    {
        return Create<TelemetrySendMessage>(data);
    }
}

public record TelemetryDataDto
{
    [property: JsonPropertyName("measures")]public required List<TelemetryMeasureDto> Measures { get; init; }
}

public record TelemetryMeasureDto
{
    /// <summary>
    /// Sensor Short ID (matches the last five characters of Sensor.ResourceId, e.g., "f782c")
    /// </summary>
    [property: JsonPropertyName("sensorId")]
    public required string SensorId { get; init; }

    /// <summary>
    /// Value object (protocol-parsed physical value)
    /// Unit and dataType are defined in DTDL
    /// </summary>
    [property: JsonPropertyName("value")]
    public required object Value { get; init; }

    /// <summary>
    /// Timestamp in milliseconds (Unix epoch)
    /// </summary>
    [property: JsonPropertyName("timestamp")]
    public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static TelemetryDataDto From(List<TelemetryMeasure> measures)
    {
        return new TelemetryDataDto
        {
            Measures = measures.ConvertAll(m => new TelemetryMeasureDto
            {
                SensorId = m.SensorId,
                Value = AdaptValue(m.Value),
                Timestamp = m.Timestamp
            })
        };
    }

    private static object AdaptValue(object value)
    {
        if (value is string str && IsJsonString(str))
        {
            try
            {
                using var doc = JsonDocument.Parse(str);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return value;
            }
        }

        return value;
    }

    private static bool IsJsonString(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return false;

        var trimmed = str.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }
}