using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event: Cloud → SubNode (configuration update received from cloud).
/// Fired when cloud service sends a configuration update command.
/// </summary>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="Message">The configuration update message from cloud.</param>
/// <param name="Timestamp">The timestamp when update was received.</param>
public sealed record UpdateConfigurationEvent(
    string DeviceId,
    SubNodeConfigurationUpdateMessage Message,
    DateTimeOffset Timestamp);
