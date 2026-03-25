using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Events;

public record TelemetryRecordingEvent(
    Sensor Sensor,
    int Interval,
    SchemaType SchemaType,
    long Timestamp,
    object Value);