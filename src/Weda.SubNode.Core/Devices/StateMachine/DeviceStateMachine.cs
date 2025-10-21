using System.Collections.Immutable;
using System.Diagnostics;
using ErrorOr;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Core.Devices.StateMachine;

/// <summary>
/// Thread-safe device state machine implementation.
/// Enforces valid state transitions and provides event notifications.
/// </summary>
public sealed class DeviceStateMachine : IDeviceStateMachine
{
    private readonly string _deviceId;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DeviceStatus _currentStatus;

    /// <summary>
    /// Defines valid state transitions.
    /// Key: current status, Value: set of valid target statuses.
    /// </summary>
    private static readonly ImmutableDictionary<DeviceStatus, ImmutableHashSet<DeviceStatus>> ValidTransitions =
        new Dictionary<DeviceStatus, ImmutableHashSet<DeviceStatus>>
        {
            [DeviceStatus.Initializing] = ImmutableHashSet.Create(
                DeviceStatus.Ready,
                DeviceStatus.Error,
                DeviceStatus.Stopped),

            [DeviceStatus.Ready] = ImmutableHashSet.Create(
                DeviceStatus.Running,
                DeviceStatus.Stopped,
                DeviceStatus.Error),

            [DeviceStatus.Running] = ImmutableHashSet.Create(
                DeviceStatus.Paused,
                DeviceStatus.Stopped,
                DeviceStatus.Error),

            [DeviceStatus.Paused] = ImmutableHashSet.Create(
                DeviceStatus.Running,
                DeviceStatus.Stopped,
                DeviceStatus.Error),

            [DeviceStatus.Error] = ImmutableHashSet.Create(
                DeviceStatus.Initializing,
                DeviceStatus.Stopped),

            [DeviceStatus.Stopped] = ImmutableHashSet.Create(
                DeviceStatus.Initializing)
        }.ToImmutableDictionary();

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceStateMachine"/> class.
    /// </summary>
    /// <param name="deviceId">The device identifier for tracking and events.</param>
    /// <param name="initialStatus">The initial device status (defaults to Initializing).</param>
    public DeviceStateMachine(string deviceId, DeviceStatus initialStatus = DeviceStatus.Initializing)
    {
        _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _currentStatus = initialStatus;
    }

    /// <inheritdoc/>
    public DeviceStatus CurrentStatus => _currentStatus;

    /// <inheritdoc/>
    public event EventHandler<DeviceStatusTransitionEvent>? StatusTransitioned;

    /// <inheritdoc/>
    public ErrorOr<Success> TryTransition(DeviceStatus newStatus)
    {
        _lock.Wait();
        try
        {
            // Check if transition is valid
            if (!CanTransitionTo(newStatus))
            {
                return Errors.Device.InvalidStateTransition(_currentStatus, newStatus);
            }

            // Same status - no transition needed
            if (_currentStatus == newStatus)
            {
                return Result.Success;
            }

            var previousStatus = _currentStatus;
            var startTime = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            // Execute transition
            _currentStatus = newStatus;

            stopwatch.Stop();

            // Fire event after successful transition
            var transitionEvent = new DeviceStatusTransitionEvent(
                DeviceId: _deviceId,
                FromStatus: previousStatus,
                ToStatus: newStatus,
                Timestamp: startTime)
            {
                TransitionDuration = stopwatch.Elapsed
            };

            StatusTransitioned?.Invoke(this, transitionEvent);

            return Result.Success;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public bool CanTransitionTo(DeviceStatus newStatus)
    {
        // Allow transition to same status (no-op)
        if (_currentStatus == newStatus)
        {
            return true;
        }

        // Check if transition is defined in the valid transitions map
        if (ValidTransitions.TryGetValue(_currentStatus, out var validTargets))
        {
            return validTargets.Contains(newStatus);
        }

        // No transitions defined for current status
        return false;
    }

    /// <inheritdoc/>
    public IReadOnlyList<DeviceStatus> GetValidTransitions()
    {
        if (ValidTransitions.TryGetValue(_currentStatus, out var validTargets))
        {
            return validTargets.ToList();
        }

        return Array.Empty<DeviceStatus>();
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _lock.Wait();
        try
        {
            var previousStatus = _currentStatus;
            _currentStatus = DeviceStatus.Initializing;

            // Fire reset event
            var resetEvent = new DeviceStatusTransitionEvent(
                DeviceId: _deviceId,
                FromStatus: previousStatus,
                ToStatus: DeviceStatus.Initializing,
                Timestamp: DateTimeOffset.UtcNow)
            {
                TransitionDuration = TimeSpan.Zero,
                Reason = "State machine reset"
            };

            StatusTransitioned?.Invoke(this, resetEvent);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets a human-readable description of valid transitions for the current status.
    /// </summary>
    public string GetTransitionDescription()
    {
        var validTransitions = GetValidTransitions();
        if (!validTransitions.Any())
        {
            return $"Current: {_currentStatus} (no valid transitions)";
        }

        return $"Current: {_currentStatus} → Valid: [{string.Join(", ", validTransitions)}]";
    }
}
