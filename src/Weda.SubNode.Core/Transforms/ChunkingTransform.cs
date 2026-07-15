using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO.Hashing;
using System.Text;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace Weda.SubNode.Core.Transforms;

/// <summary>
/// Configuration parameters for <see cref="ChunkingTransform"/>.
/// </summary>
public class ChunkingParameters
{
    [Description("Maximum size in bytes for each chunk. Must be between 1KB and 750KB.")]
    [JsonPropertyName("chunkSize")]
    [Range(1024, 750 * 1024)]
    [DefaultValue(256 * 1024)]
    public int ChunkSize { get; init; } = 256 * 1024;
}

/// <summary>
/// Splits large base64 string measurements into multiple chunks to fit within
/// bandwidth-limited transports. JSON-shaped strings (starting with '{' or '[')
/// pass through unchanged.
/// </summary>
public class ChunkingTransform
    : ITelemetryTransform,
      IConfigurableTransform<ChunkingTransform, ChunkingParameters>
{
    private int _chunkSize;

    public string Name => "chunking";
    public bool Enabled { get; set; } = true;

    public static string TypeName => "chunking";

    public static string? Description =>
        "Split large base64 string measurements into bandwidth-friendly chunks.";

    public static ChunkingTransform Create(ChunkingParameters parameters) =>
        new() { _chunkSize = parameters.ChunkSize };

    public void UpdateParameters(ChunkingParameters parameters)
    {
        _chunkSize = parameters.ChunkSize;
    }

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new List<TelemetryMeasure>();

        foreach (var measure in measures)
        {
            if (measure.Value is string base64 && base64.Length > _chunkSize && !IsJsonString(base64))
            {
                result.AddRange(CreateChunks(measure, base64));
            }
            else
            {
                result.Add(measure);
            }
        }

        return Task.FromResult(result);
    }

    private List<TelemetryMeasure> CreateChunks(TelemetryMeasure measure, string base64)
    {
        var transferId = Guid.NewGuid().ToString();
        var totalChunks = (int)Math.Ceiling((double)base64.Length / _chunkSize);
        var checksum = Crc32.HashToUInt32(Encoding.UTF8.GetBytes(base64));
        var chunks = new List<TelemetryMeasure>();

        for (int i = 0; i < totalChunks; i++)
        {
            var start = i * _chunkSize;
            var length = Math.Min(_chunkSize, base64.Length - start);
            var chunkValue = base64.Substring(start, length);

            var metadata = new Dictionary<string, object>
            {
                ["transferId"] = transferId,
                ["chunkIndex"] = i,
                ["totalChunks"] = totalChunks,
                ["crc32Checksum"] = checksum,
            };

            if (measure.Metadata != null)
            {
                foreach (var kv in measure.Metadata)
                {
                    metadata.TryAdd(kv.Key, kv.Value);
                }
            }

            chunks.Add(new TelemetryMeasure
            {
                ResourceId = measure.ResourceId,
                Value = chunkValue,
                Timestamp = measure.Timestamp,
                Metadata = metadata,
            });
        }

        return chunks;
    }

    private static bool IsJsonString(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            return false;
        }
        var trimmed = str.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }
}
