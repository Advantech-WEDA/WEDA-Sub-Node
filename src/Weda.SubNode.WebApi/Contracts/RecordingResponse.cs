using System.Text.Json.Serialization;
using Weda.SubNode.Abstractions.Storage;

namespace Weda.SubNode.WebApi.Contracts;

public record RecordingResponse
{
    [JsonPropertyName("measures")]
    public required List<RecordingMeasure> Measures { get; init; }

    public static RecordingResponse FromResult(RecordingResult result) => new()
    {
        Measures = result.Measures
            .Select(m => TrimNaNValues(result.SensorId, m))
            .Where(m => m.Values.Count > 0)
            .ToList()
    };

    private static RecordingMeasure TrimNaNValues(string sensorId, RecordingMeasureResult m)
    {
        var values = m.Values;

        // Find first non-NaN index (prefix trim)
        var firstValidIndex = 0;
        while (firstValidIndex < values.Count && double.IsNaN(values[firstValidIndex]))
            firstValidIndex++;

        // Find last non-NaN index (postfix trim)
        var lastValidIndex = values.Count - 1;
        while (lastValidIndex >= 0 && double.IsNaN(values[lastValidIndex]))
            lastValidIndex--;

        // All values are NaN
        if (firstValidIndex > lastValidIndex)
        {
            return new RecordingMeasure
            {
                Id = sensorId,
                Interval = m.Interval,
                StartTimeStamp = m.StartTimeStamp,
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
            Values = trimmedValues
        };
    }
}

public record RecordingMeasure
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("interval")]
    public required int Interval { get; init; }

    [JsonPropertyName("startTimeStamp")]
    public required long StartTimeStamp { get; init; }

    [JsonPropertyName("values")]
    [JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)]
    public required List<double> Values { get; init; }
}
