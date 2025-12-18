using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event: Device → SubNode (telemetry data processed through pipeline).
/// Fired after successful transformation and DSP filtering, before sending to cloud.
/// Use this event when you need processed/transformed data instead of raw data.
/// </summary>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="SubNodeType">The device type.</param>
/// <param name="Data">The processed telemetry measures after transform and filter.</param>
/// <param name="Timestamp">The timestamp when data was processed.</param>
public sealed record DataProcessedEvent(
    string DeviceId,
    SubNodeType SubNodeType,
    List<TelemetryMeasure> Data,
    DateTimeOffset Timestamp);
