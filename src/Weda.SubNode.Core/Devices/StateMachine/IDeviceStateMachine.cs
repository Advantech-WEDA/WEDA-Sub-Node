using ErrorOr;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core.Devices.StateMachine;

/// <summary>
/// Thread-safe device state machine with event notifications.
/// Manages device status transitions and ensures only valid state changes occur.
/// </summary>
public interface IDeviceStateMachine
{
    /// <summary>
    /// Gets the current device status (thread-safe read).
    /// </summary>
    DeviceStatus CurrentStatus { get; }

    /// <summary>
    /// Attempts to transition to a new status.
    /// </summary>
    /// <param name="newStatus">The target device status.</param>
    /// <returns>Success if transition is valid; error otherwise.</returns>
    ErrorOr<Success> TryTransition(DeviceStatus newStatus);

    /// <summary>
    /// Checks if transition to the specified status is valid without executing it.
    /// </summary>
    /// <param name="newStatus">The target device status to validate.</param>
    /// <returns>True if the transition is valid; false otherwise.</returns>
    bool CanTransitionTo(DeviceStatus newStatus);

    /// <summary>
    /// Gets all valid transitions from the current status.
    /// </summary>
    /// <returns>A read-only list of valid target statuses.</returns>
    IReadOnlyList<DeviceStatus> GetValidTransitions();

    /// <summary>
    /// Resets the state machine to its initial state (Initializing).
    /// Use with caution - primarily for testing or error recovery.
    /// </summary>
    void Reset();

    /// <summary>
    /// Event raised after a successful status transition.
    /// Handlers receive detailed information about the transition.
    /// </summary>
    event EventHandler<DeviceStatusTransitionEvent>? StatusTransitioned;
}
