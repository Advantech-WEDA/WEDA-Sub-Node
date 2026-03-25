using System.Text.Json.Serialization;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.WebApi.Contracts;

public record RecordingResponse
{
    [JsonPropertyName("measures")]
    public required List<RecordingMeasure> Measures { get; init; }

    public static RecordingResponse FromResult(RecordingResult result) => new()
    {
        Measures = result.Measures
            .Select(m => TrimEmptyValues(result.SensorId, m))
            .Where(m => m.Values.Count > 0)
            .ToList()
    };

    private static RecordingMeasure TrimEmptyValues(string sensorId, RecordingMeasureResult m)
    {
        var values = m.Values;
        var schemaType = m.SchemaType;

        // Find first valid index (prefix trim)
        var firstValidIndex = 0;
        while (firstValidIndex < values.Count && IsEmptyValue(values[firstValidIndex], schemaType))
            firstValidIndex++;

        // Find last valid index (postfix trim)
        var lastValidIndex = values.Count - 1;
        while (lastValidIndex >= 0 && IsEmptyValue(values[lastValidIndex], schemaType))
            lastValidIndex--;

        // All values are empty
        if (firstValidIndex > lastValidIndex)
        {
            return new RecordingMeasure
            {
                Id = sensorId,
                Interval = m.Interval,
                StartTimeStamp = m.StartTimeStamp,
                SchemaType = schemaType.ToString().ToLowerInvariant(),
                Values = []
            };
        }

        // Calculate new start timestamp based on trimmed prefix
        var newStartTimeStamp = m.StartTimeStamp + (long)firstValidIndex * m.Interval;
        var trimmedValues = values.Skip(firstValidIndex).Take(lastValidIndex - firstValidIndex + 1).ToList();

        return new RecordingMeasure
        {
            Id = sensorId,
            Interval = m.Interval,
            StartTimeStamp = newStartTimeStamp,
            SchemaType = schemaType.ToString().ToLowerInvariant(),
            Values = trimmedValues
        };
    }

    private static bool IsEmptyValue(object value, SchemaType schemaType) => schemaType switch
    {
        SchemaType.Double => value is double d && double.IsNaN(d),
        SchemaType.Long => value is long l && l == long.MinValue,
        SchemaType.Integer => value is int i && i == int.MinValue,
        SchemaType.Boolean => value is bool b && b == false && value.Equals(0xFF), // 0xFF is the empty marker
        _ => false
    };
}

public record RecordingMeasure
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("interval")]
    public required int Interval { get; init; }

    [JsonPropertyName("startTimeStamp")]
    public required long StartTimeStamp { get; init; }

    [JsonPropertyName("schemaType")]
    public required string SchemaType { get; init; }

    [JsonPropertyName("values")]
    [JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)]
    public required List<object> Values { get; init; }
}

/// <summary>
/// Response for MIME type recordings (JSON, images, etc.)
/// </summary>
public record DynamicRecordingResponse
{
    [JsonPropertyName("sensorId")]
    public required string SensorId { get; init; }

    [JsonPropertyName("schemaType")]
    public required string SchemaType { get; init; }

    [JsonPropertyName("records")]
    public required List<DynamicRecordItem> Records { get; init; }
}

public record DynamicRecordItem
{
    [JsonPropertyName("timestamp")]
    public required long Timestamp { get; init; }

    /// <summary>
    /// For JSON schema: raw JSON object (not escaped).
    /// For binary schemas (images): base64 encoded string.
    /// </summary>
    [JsonPropertyName("data")]
    public required object Data { get; init; }
}
