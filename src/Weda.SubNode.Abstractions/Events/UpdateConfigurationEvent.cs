namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event: Cloud → SubNode (configuration update received from cloud).
/// Fired when cloud service sends a configuration update command.
/// </summary>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="Configuration">The updated configuration key-value pairs.</param>
/// <param name="Timestamp">The timestamp when update was received.</param>
public sealed record UpdateConfigurationEvent(
    string DeviceId,
    Dictionary<string, object> Configuration,
    DateTimeOffset Timestamp);
