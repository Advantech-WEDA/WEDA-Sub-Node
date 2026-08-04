using System.Text.Json.Serialization;

namespace VisionObjectDetection.Protocols;

/// <summary>
/// Payload of the CV container's <c>advantech/&lt;DEVICE_ID&gt;/vision/detections</c> topic.
/// Mirrors §6 of the Advantech-YOLO-Vision-Applications MQTT telemetry contract exactly.
/// </summary>
public sealed record VisionDetectionMessage
{
    [JsonPropertyName("timestamp")] public string? Timestamp { get; init; }
    [JsonPropertyName("deviceId")] public string? DeviceId { get; init; }
    [JsonPropertyName("frame")] public long Frame { get; init; }
    [JsonPropertyName("lap")] public long Lap { get; init; }
    [JsonPropertyName("fps")] public double Fps { get; init; }
    [JsonPropertyName("objectCount")] public int ObjectCount { get; init; }
    [JsonPropertyName("classCounts")] public Dictionary<string, int>? ClassCounts { get; init; }
    [JsonPropertyName("detections")] public List<VisionDetection>? Detections { get; init; }
}

/// <summary>A single detected object inside a <see cref="VisionDetectionMessage"/>.</summary>
public sealed record VisionDetection
{
    [JsonPropertyName("class")] public string? Class { get; init; }
    [JsonPropertyName("confidence")] public double Confidence { get; init; }

    /// <summary>Bounding box in source-image pixels, ordered [x1, y1, x2, y2].</summary>
    [JsonPropertyName("bbox")] public double[]? Bbox { get; init; }
}

/// <summary>
/// Payload of the retained <c>advantech/&lt;DEVICE_ID&gt;/vision/meta</c> topic
/// (model, source clip, and inference thresholds). Logged for diagnostics.
/// </summary>
public sealed record VisionMetaMessage
{
    [JsonPropertyName("model")] public string? Model { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("confThreshold")] public double ConfThreshold { get; init; }
    [JsonPropertyName("iouThreshold")] public double IouThreshold { get; init; }
    [JsonPropertyName("demoVersion")] public string? DemoVersion { get; init; }
}
