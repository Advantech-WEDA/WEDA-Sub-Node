namespace Weda.SubNode.Abstractions.Storage.Recordings;

public readonly record struct RecordingDataPoint(
    long Timestamp,
    double Value
);