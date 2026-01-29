using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Events;

public record TelemetryRecordingEvent(
    Sensor Sensor,
    int Interval,
    long Timestamp,
    double Value);