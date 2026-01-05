using ErrorOr;
using Weda.SubNode.Abstractions.Communication;

namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Interface for managing device connections (physical device only).
/// Cloud connection is handled by SubNodeManager at the SubNode level.
/// Extracted from DeviceBase to follow Single Responsibility Principle.
/// </summary>
public interface IDeviceConnectionManager
{
    /// <summary>
    /// Establishes connection to the physical device only.
    /// Cloud connection is handled by SubNodeManager at the SubNode level.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or error result</returns>
    Task<ErrorOr<Success>> EstablishPhysicalConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from physical device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Current connection state.
    /// </summary>
    CommunicationState CurrentState { get; }
}
