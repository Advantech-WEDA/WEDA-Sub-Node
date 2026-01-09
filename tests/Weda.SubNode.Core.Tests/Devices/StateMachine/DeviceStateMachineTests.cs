using Shouldly;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Devices.StateMachine;
using Xunit;

namespace Weda.SubNode.Core.Tests.Devices.StateMachine;

public class DeviceStateMachineTests
{
    private const string TestDeviceId = "test-device-001";

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultStatus()
    {
        // Arrange & Act
        var stateMachine = new DeviceStateMachine(TestDeviceId);

        // Assert
        stateMachine.CurrentStatus.ShouldBe(DeviceStatus.Initializing);
    }

    [Theory]
    [InlineData(DeviceStatus.Ready)]
    [InlineData(DeviceStatus.Running)]
    [InlineData(DeviceStatus.Stopped)]
    public void Constructor_ShouldInitializeWithCustomStatus(DeviceStatus initialStatus)
    {
        // Arrange & Act
        var stateMachine = new DeviceStateMachine(TestDeviceId, initialStatus);

        // Assert
        stateMachine.CurrentStatus.ShouldBe(initialStatus);
    }

    [Fact]
    public void TryTransition_ValidTransition_ShouldSucceed()
    {
        // Arrange
        var stateMachine = new DeviceStateMachine(TestDeviceId, DeviceStatus.Initializing);

        // Act
        var result = stateMachine.TryTransition(DeviceStatus.Ready);

        // Assert
        result.IsError.ShouldBeFalse();
        stateMachine.CurrentStatus.ShouldBe(DeviceStatus.Ready);
    }

    [Fact]
    public void TryTransition_InvalidTransition_ShouldReturnError()
    {
        // Arrange
        var stateMachine = new DeviceStateMachine(TestDeviceId, DeviceStatus.Initializing);

        // Act
        var result = stateMachine.TryTransition(DeviceStatus.Running);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Device.InvalidStateTransition");
        stateMachine.CurrentStatus.ShouldBe(DeviceStatus.Initializing); // Should remain unchanged
    }

    [Fact]
    public void TryTransition_SameStatus_ShouldSucceedWithoutEvent()
    {
        // Arrange
        var stateMachine = new DeviceStateMachine(TestDeviceId, DeviceStatus.Ready);
        var eventFired = false;
        stateMachine.StatusTransitioned += (s, e) => eventFired = true;

        // Act
        var result = stateMachine.TryTransition(DeviceStatus.Ready);

        // Assert
        result.IsError.ShouldBeFalse();
        stateMachine.CurrentStatus.ShouldBe(DeviceStatus.Ready);
        eventFired.ShouldBeFalse(); // Event should not fire for same-status transitions
    }

    [Fact]
    public void TryTransition_ValidTransition_ShouldFireEvent()
    {
        // Arrange
        var stateMachine = new DeviceStateMachine(TestDeviceId, DeviceStatus.Initializing);
        DeviceStatusTransitionEvent? capturedEvent = null;
        stateMachine.StatusTransitioned += (s, e) => capturedEvent = e;

        // Act
        var result = stateMachine.TryTransition(DeviceStatus.Ready);

        // Assert
        result.IsError.ShouldBeFalse();
        capturedEvent.ShouldNotBeNull();
        capturedEvent.DeviceId.ShouldBe(TestDeviceId);
        capturedEvent.FromStatus.ShouldBe(DeviceStatus.Initializing);
        capturedEvent.ToStatus.ShouldBe(DeviceStatus.Ready);
        capturedEvent.TransitionDuration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(DeviceStatus.Initializing, DeviceStatus.Ready, true)]
    [InlineData(DeviceStatus.Initializing, DeviceStatus.Error, true)]
    [InlineData(DeviceStatus.Initializing, DeviceStatus.Stopped, true)]
    [InlineData(DeviceStatus.Initializing, DeviceStatus.Running, false)]
    [InlineData(DeviceStatus.Ready, DeviceStatus.Running, true)]
    [InlineData(DeviceStatus.Ready, DeviceStatus.Stopped, true)]
    [InlineData(DeviceStatus.Ready, DeviceStatus.Initializing, false)]
    [InlineData(DeviceStatus.Running, DeviceStatus.Paused, true)]
    [InlineData(DeviceStatus.Running, DeviceStatus.Stopped, true)]
    [InlineData(DeviceStatus.Running, DeviceStatus.Ready, false)]
    [InlineData(DeviceStatus.Error, DeviceStatus.Initializing, true)]
    [InlineData(DeviceStatus.Error, DeviceStatus.Stopped, true)]
    [InlineData(DeviceStatus.Error, DeviceStatus.Ready, false)]
    [InlineData(DeviceStatus.Stopped, DeviceStatus.Initializing, true)]
    [InlineData(DeviceStatus.Stopped, DeviceStatus.Running, false)]
    public void CanTransitionTo_ShouldReturnCorrectValidation(
        DeviceStatus fromStatus,
        DeviceStatus toStatus,
        bool expectedResult)
    {
        // Arrange
        var stateMachine = new DeviceStateMachine(TestDeviceId, fromStatus);

        // Act
        var canTransition = stateMachine.CanTransitionTo(toStatus);

        // Assert
        canTransition.ShouldBe(expectedResult);
    }

    [Fact]
    public void GetValidTransitions_ShouldReturnCorrectTransitions()
    {
        // Arrange
        var stateMachine = new DeviceStateMachine(TestDeviceId, DeviceStatus.Initializing);

        // Act
        var validTransitions = stateMachine.GetValidTransitions();

        // Assert
        validTransitions.Count.ShouldBe(3);
        validTransitions.ShouldContain(DeviceStatus.Ready);
        validTransitions.ShouldContain(DeviceStatus.Error);
        validTransitions.ShouldContain(DeviceStatus.Stopped);
    }

    [Fact]
    public void Reset_ShouldResetToInitializing()
    {
        // Arrange
        var stateMachine = new DeviceStateMachine(TestDeviceId, DeviceStatus.Initializing);
        stateMachine.TryTransition(DeviceStatus.Ready);
        stateMachine.TryTransition(DeviceStatus.Running);

        // Act
        stateMachine.Reset();

        // Assert
        stateMachine.CurrentStatus.ShouldBe(DeviceStatus.Initializing);
    }

    [Fact]
    public void Reset_ShouldFireEventWithReason()
    {
        // Arrange
        var stateMachine = new DeviceStateMachine(TestDeviceId, DeviceStatus.Running);
        DeviceStatusTransitionEvent? capturedEvent = null;
        stateMachine.StatusTransitioned += (s, e) => capturedEvent = e;

        // Act
        stateMachine.Reset();

        // Assert
        capturedEvent.ShouldNotBeNull();
        capturedEvent.FromStatus.ShouldBe(DeviceStatus.Running);
        capturedEvent.ToStatus.ShouldBe(DeviceStatus.Initializing);
        capturedEvent.Reason.ShouldBe("State machine reset");
    }

    [Fact]
    public async Task ConcurrentTransitions_ShouldBeSerialized()
    {
        // Arrange
        var stateMachine = new DeviceStateMachine(TestDeviceId, DeviceStatus.Initializing);
        var transitionCount = 0;
        var eventFiredCount = 0;

        stateMachine.StatusTransitioned += (s, e) =>
        {
            Interlocked.Increment(ref eventFiredCount);
            Thread.Sleep(10); // Simulate slow event handler
        };

        // Act - Execute concurrent transitions
        var tasks = new List<Task<bool>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var result = stateMachine.TryTransition(DeviceStatus.Ready);
                if (!result.IsError)
                {
                    Interlocked.Increment(ref transitionCount);
                }
                return !result.IsError;
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        stateMachine.CurrentStatus.ShouldBe(DeviceStatus.Ready);
        // First transition succeeds, rest are no-ops (same status)
        eventFiredCount.ShouldBe(1); // Only first actual transition fires event
    }

    [Fact]
    public void FullLifecycle_ShouldFollowValidPath()
    {
        // Arrange
        var stateMachine = new DeviceStateMachine(TestDeviceId, DeviceStatus.Initializing);
        var transitionLog = new List<string>();

        stateMachine.StatusTransitioned += (s, e) =>
        {
            transitionLog.Add($"{e.FromStatus} -> {e.ToStatus}");
        };

        // Act - Simulate full device lifecycle
        stateMachine.TryTransition(DeviceStatus.Ready).IsError.ShouldBeFalse();
        stateMachine.TryTransition(DeviceStatus.Running).IsError.ShouldBeFalse();
        stateMachine.TryTransition(DeviceStatus.Paused).IsError.ShouldBeFalse();
        stateMachine.TryTransition(DeviceStatus.Running).IsError.ShouldBeFalse();
        stateMachine.TryTransition(DeviceStatus.Stopped).IsError.ShouldBeFalse();

        // Assert
        stateMachine.CurrentStatus.ShouldBe(DeviceStatus.Stopped);
        transitionLog.Count.ShouldBe(5);
        transitionLog[0].ShouldBe("Initializing -> Ready");
        transitionLog[1].ShouldBe("Ready -> Running");
        transitionLog[2].ShouldBe("Running -> Paused");
        transitionLog[3].ShouldBe("Paused -> Running");
        transitionLog[4].ShouldBe("Running -> Stopped");
    }

    [Fact]
    public void ErrorRecovery_ShouldAllowReinitialization()
    {
        // Arrange
        var stateMachine = new DeviceStateMachine(TestDeviceId, DeviceStatus.Initializing);
        stateMachine.TryTransition(DeviceStatus.Ready);
        stateMachine.TryTransition(DeviceStatus.Running);

        // Act - Simulate error and recovery
        stateMachine.TryTransition(DeviceStatus.Error).IsError.ShouldBeFalse();
        stateMachine.CurrentStatus.ShouldBe(DeviceStatus.Error);

        // Try to recover
        stateMachine.TryTransition(DeviceStatus.Initializing).IsError.ShouldBeFalse();
        stateMachine.TryTransition(DeviceStatus.Ready).IsError.ShouldBeFalse();
        stateMachine.TryTransition(DeviceStatus.Running).IsError.ShouldBeFalse();

        // Assert
        stateMachine.CurrentStatus.ShouldBe(DeviceStatus.Running);
    }
}
