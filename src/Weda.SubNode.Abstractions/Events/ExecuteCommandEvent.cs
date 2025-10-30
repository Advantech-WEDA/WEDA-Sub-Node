using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event: Cloud → SubNode (command execution received from cloud).
/// Fired when cloud service sends a command to execute on the device.
/// </summary>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="Command">The command to execute.</param>
/// <param name="Timestamp">The timestamp when command was received.</param>
public sealed record ExecuteCommandEvent(
    string DeviceId,
    DeviceCommand Command,
    DateTimeOffset Timestamp);
