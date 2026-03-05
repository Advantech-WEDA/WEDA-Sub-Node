using System.IO.Hashing;
using System.Text;
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

    /// <summary>
    /// The metadata of Telemetry Dto
    /// </summary>
    [property: JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Metadata { get; init; }

    public static TelemetryDataDto From(List<TelemetryMeasure> measures)
        => From(measures, TelemetryOptions.Default);

    public static TelemetryDataDto From(List<TelemetryMeasure> measures, TelemetryOptions options)
    {
        var result = new List<TelemetryMeasureDto>();

        foreach (var m in measures)
        {
            var adapted = AdaptValue(m.Value);

            // Chunk if chunking is needed for large Base64 strings
            if (adapted is string base64 && base64.Length > options.ChunkSize && !IsJsonString(base64))
            {
                result.AddRange(CreateChunks(m, base64, options.ChunkSize));
            }
            else
            {
                result.Add(new TelemetryMeasureDto
                {
                    SensorId = m.SensorId,
                    Value = adapted,
                    Timestamp = m.Timestamp,
                    Metadata = m.Metadata?.ToDictionary(kv => kv.Key, KeyValuePair => KeyValuePair.Value)
                });
            }
        }

        return new TelemetryDataDto
        {
            Measures = result
        };
    }

    private static List<TelemetryMeasureDto> CreateChunks(TelemetryMeasure measure, string base64, int chunkSize)
    {
        var transferId = Guid.NewGuid().ToString();
        var totalChunks = (int)Math.Ceiling((double)base64.Length / chunkSize);
        var checksum = Crc32.HashToUInt32(Encoding.UTF8.GetBytes(base64));
        var chunks = new List<TelemetryMeasureDto>();

        for (int i = 0; i < totalChunks; i++)
        {
            var start = i * chunkSize;
            var length = Math.Min(chunkSize, base64.Length - start);
            var chunkValue = base64.Substring(start, length);

            var metadata = new Dictionary<string, object>
            {
                ["transferId"] = transferId,
                ["chunkIndex"] = i,
                ["totalChunks"] = totalChunks,
                ["crc32Checksum"] = checksum
            };

            if (measure.Metadata != null)
            {
                foreach (var kv in measure.Metadata)
                    metadata.TryAdd(kv.Key, kv.Value);
                
            }

            chunks.Add(new TelemetryMeasureDto
            {
                SensorId = measure.SensorId,
                Value = chunkValue,
                Timestamp = measure.Timestamp,
                Metadata = metadata
            });
        }

        return chunks;
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