namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// Represents a single data point in a recording.
/// </summary>
/// <param name="Timestamp">Unix timestamp in milliseconds.</param>
/// <param name="Value">The recorded value (type depends on SchemaType).</param>
/// <param name="SchemaType">The data type of the value. Defaults to Double for V1 compatibility.</param>
public readonly record struct RecordingDataPoint(
    long Timestamp,
    object Value,
    SchemaType SchemaType = SchemaType.Double);
