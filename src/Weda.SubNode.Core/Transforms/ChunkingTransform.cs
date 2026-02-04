using System.IO.Hashing;
using System.Text;

using ErrorOr;

using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace Weda.SubNode.Core.Transforms;

public class ChunkingTransform : ITelemetryTransform, IConfigurableTransform<ChunkingTransform>
{
    private int _chunkSize = 256 * 1024; // Default 256KB for bandwidth-limited scenarios
    
    public string Name => "chunking";
    public bool Enabled { get; set; } = true;

    public static string TypeName => "chunking";

    public static ChunkingTransform Create(Dictionary<string, object> parameters)
    {
        var transform = new ChunkingTransform();
        transform.UpdateParameters(parameters);
        return transform;
    }

    public ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("chunkSize", out var value))
        {
            if (value is not int and not long and not double)
                return Error.Failure(description: "chunkSize must be a number");

            var size = Convert.ToInt32(value);
            if (size < 1024)
                return Error.Failure(description: "chunkSize must be at least 1KB");
            if (size > 750 * 1024)
                return Error.Failure(description: "chunkSize cannot exceed 750KB");
        }

        return Result.Success;
    }

    public void UpdateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("chunkSize", out var value))
        {
            _chunkSize = Convert.ToInt32(value);
        }
    }

    public Task<List<TelemetryMeasure>> TransformAsync(List<TelemetryMeasure> measures, TelemetryTransformContext context, CancellationToken cancellationToken = default)
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
        var imageId = Guid.NewGuid().ToString();
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
                ["imageId"] = imageId,
                ["chunkIndex"] = i,
                ["totalChunks"] = totalChunks,
                ["checksum"] = checksum
            };

            if (measure.Metadata != null)
            {
                foreach (var kv in measure.Metadata)
                    metadata.TryAdd(kv.Key, kv.Value);
                
            }

            chunks.Add(new TelemetryMeasure
            {
                ResourceId = measure.ResourceId,
                Value = chunkValue,
                Timestamp = measure.Timestamp,
                Metadata = metadata
            });
        }

        return chunks;
    }

    private static bool IsJsonString(string str)
    {
        if (string.IsNullOrWhiteSpace(str))
            return false;

        var trimmed = str.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }
}