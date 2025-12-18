using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event: Device → SubNode (telemetry data received from physical device).
/// Fired after successful read from device, before any transformation or filtering.
/// </summary>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="SubNodeType">The device type.</param>
/// <param name="Data">The raw telemetry measures received.</param>
/// <param name="Timestamp">The timestamp when data was received.</param>
public sealed record DataReceivedEvent(
    string DeviceId,
    SubNodeType SubNodeType,
    List<TelemetryMeasure> Data,
    DateTimeOffset Timestamp);
