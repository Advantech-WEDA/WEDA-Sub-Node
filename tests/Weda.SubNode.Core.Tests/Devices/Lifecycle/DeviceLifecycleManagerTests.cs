using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Devices.Lifecycle;
using Weda.SubNode.Core.Devices.StateMachine;
using Xunit;

namespace Weda.SubNode.Core.Tests.Devices.Lifecycle;

/// <summary>
/// Unit tests for DeviceLifecycleManager.
/// Tests sealed lifecycle template methods with hook integration and state coordination.
/// </summary>
public sealed class DeviceLifecycleManagerTests
{
    private const string TestDeviceId = "test-lifecycle-device";
    private readonly IDeviceStateMachine _mockStateMachine;
    private readonly ILifecycleHooks _mockHooks;
    private readonly DeviceLifecycleManager _manager;

    public DeviceLifecycleManagerTests()
    {
        _mockStateMachine = Substitute.For<IDeviceStateMachine>();
        _mockHooks = Substitute.For<ILifecycleHooks>();
        _manager = new DeviceLifecycleManager(
            TestDeviceId,
            _mockStateMachine,
            _mockHooks,
            NullLogger<DeviceLifecycleManager>.Instance);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullDeviceId_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DeviceLifecycleManager(
                null!,
                _mockStateMachine,
                _mockHooks,
                NullLogger<DeviceLifecycleManager>.Instance));
    }

    [Fact]
    public void Constructor_WithNullStateMachine_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DeviceLifecycleManager(
                TestDeviceId,
                null!,
                _mockHooks,
                NullLogger<DeviceLifecycleManager>.Instance));
    }

    [Fact]
    public void Constructor_WithNullHooks_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DeviceLifecycleManager(
                TestDeviceId,
                _mockStateMachine,
                null!,
                NullLogger<DeviceLifecycleManager>.Instance));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        Should.Throw<ArgumentNullException>(() =>
            new DeviceLifecycleManager(
                TestDeviceId,
                _mockStateMachine,
                _mockHooks,
                null!));
    }

    #endregion

    #region Initialize Tests

    [Fact]
    public async Task InitializeAsync_FromInitializingState_ShouldSucceed()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Initializing);
        _mockHooks.OnInitializeAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        _mockStateMachine.TryTransition(DeviceStatus.Ready)
            .Returns(Result.Success);

        // Act
        var result = await _manager.InitializeAsync();

        // Assert
        result.IsError.ShouldBeFalse();
        await _mockHooks.Received(1).OnInitializeAsync(Arg.Any<CancellationToken>());
        _mockStateMachine.Received(1).TryTransition(DeviceStatus.Ready);

        var stats = _manager.GetStatistics();
        stats.InitializeCount.ShouldBe(1);
    }

    [Fact]
    public async Task InitializeAsync_FromWrongState_ShouldReturnError()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Running);

        // Act
        var result = await _manager.InitializeAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldContain("InvalidState");
        await _mockHooks.DidNotReceive().OnInitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_HookFails_ShouldReturnError()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Initializing);
        _mockHooks.OnInitializeAsync(Arg.Any<CancellationToken>())
            .Returns(Error.Failure("Hook.Failed", "Hook failed"));

        // Act
        var result = await _manager.InitializeAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Hook.Failed");
        _mockStateMachine.DidNotReceive().TryTransition(Arg.Any<DeviceStatus>());
    }

    [Fact]
    public async Task InitializeAsync_StateTransitionFails_ShouldReturnError()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Initializing);
        _mockHooks.OnInitializeAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        _mockStateMachine.TryTransition(DeviceStatus.Ready)
            .Returns(Error.Validation("Transition.Invalid", "Invalid transition"));

        // Act
        var result = await _manager.InitializeAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Transition.Invalid");
    }

    [Fact]
    public async Task InitializeAsync_HookThrows_ShouldReturnError()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Initializing);
        _mockHooks.OnInitializeAsync(Arg.Any<CancellationToken>())
            .Returns<Task<ErrorOr<Success>>>(_ => throw new InvalidOperationException("Test exception"));

        // Act
        var result = await _manager.InitializeAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldContain("InitializeFailed");
    }

    #endregion

    #region Start Tests

    [Fact]
    public async Task StartAsync_FromReadyState_ShouldSucceed()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Ready);
        _mockHooks.OnStartAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        _mockStateMachine.TryTransition(DeviceStatus.Running)
            .Returns(Result.Success);

        // Act
        var result = await _manager.StartAsync();

        // Assert
        result.IsError.ShouldBeFalse();
        await _mockHooks.Received(1).OnStartAsync(Arg.Any<CancellationToken>());
        _mockStateMachine.Received(1).TryTransition(DeviceStatus.Running);

        var stats = _manager.GetStatistics();
        stats.StartCount.ShouldBe(1);
    }

    [Fact]
    public async Task StartAsync_FromWrongState_ShouldReturnError()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Running);

        // Act
        var result = await _manager.StartAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldContain("InvalidState");
        await _mockHooks.DidNotReceive().OnStartAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_HookFails_ShouldReturnError()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Ready);
        _mockHooks.OnStartAsync(Arg.Any<CancellationToken>())
            .Returns(Error.Failure("Hook.Failed", "Start hook failed"));

        // Act
        var result = await _manager.StartAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Hook.Failed");
        _mockStateMachine.DidNotReceive().TryTransition(Arg.Any<DeviceStatus>());
    }

    #endregion

    #region Stop Tests

    [Fact]
    public async Task StopAsync_FromRunningState_ShouldSucceed()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Running);
        _mockHooks.OnStopAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        _mockStateMachine.TryTransition(DeviceStatus.Stopped)
            .Returns(Result.Success);

        // Act
        var result = await _manager.StopAsync();

        // Assert
        result.IsError.ShouldBeFalse();
        await _mockHooks.Received(1).OnStopAsync(Arg.Any<CancellationToken>());
        _mockStateMachine.Received(1).TryTransition(DeviceStatus.Stopped);

        var stats = _manager.GetStatistics();
        stats.StopCount.ShouldBe(1);
    }

    [Fact]
    public async Task StopAsync_FromWrongState_ShouldReturnError()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Ready);

        // Act
        var result = await _manager.StopAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldContain("InvalidState");
        await _mockHooks.DidNotReceive().OnStopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAsync_HookFails_ShouldReturnError()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Running);
        _mockHooks.OnStopAsync(Arg.Any<CancellationToken>())
            .Returns(Error.Failure("Hook.Failed", "Stop hook failed"));

        // Act
        var result = await _manager.StopAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Hook.Failed");
        _mockStateMachine.DidNotReceive().TryTransition(Arg.Any<DeviceStatus>());
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task DisposeAsync_FirstCall_ShouldSucceed()
    {
        // Arrange
        _mockHooks.OnDisposeAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);

        // Act
        var result = await _manager.DisposeAsync();

        // Assert
        result.IsError.ShouldBeFalse();
        await _mockHooks.Received(1).OnDisposeAsync(Arg.Any<CancellationToken>());
        _manager.IsDisposed.ShouldBeTrue();

        var stats = _manager.GetStatistics();
        stats.DisposeCount.ShouldBe(1);
        stats.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldBeIdempotent()
    {
        // Arrange
        _mockHooks.OnDisposeAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);

        // Act
        var result1 = await _manager.DisposeAsync();
        var result2 = await _manager.DisposeAsync();

        // Assert
        result1.IsError.ShouldBeFalse();
        result2.IsError.ShouldBeFalse();
        await _mockHooks.Received(1).OnDisposeAsync(Arg.Any<CancellationToken>());
        _manager.IsDisposed.ShouldBeTrue();

        var stats = _manager.GetStatistics();
        stats.DisposeCount.ShouldBe(1); // Only counted once
    }

    [Fact]
    public async Task DisposeAsync_HookFails_ShouldStillDispose()
    {
        // Arrange
        _mockHooks.OnDisposeAsync(Arg.Any<CancellationToken>())
            .Returns(Error.Failure("Hook.Failed", "Dispose hook failed"));

        // Act
        var result = await _manager.DisposeAsync();

        // Assert
        result.IsError.ShouldBeFalse(); // Still succeeds despite hook failure
        _manager.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task DisposeAsync_HookThrows_ShouldStillDispose()
    {
        // Arrange
        _mockHooks.OnDisposeAsync(Arg.Any<CancellationToken>())
            .Returns<Task<ErrorOr<Success>>>(_ => throw new InvalidOperationException("Dispose failed"));

        // Act
        var result = await _manager.DisposeAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        _manager.IsDisposed.ShouldBeTrue(); // Still marked as disposed
    }

    [Fact]
    public async Task InitializeAsync_AfterDispose_ShouldThrow()
    {
        // Arrange
        _mockHooks.OnDisposeAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        await _manager.DisposeAsync();

        // Act & Assert
        await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await _manager.InitializeAsync());
    }

    [Fact]
    public async Task StartAsync_AfterDispose_ShouldThrow()
    {
        // Arrange
        _mockHooks.OnDisposeAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        await _manager.DisposeAsync();

        // Act & Assert
        await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await _manager.StartAsync());
    }

    [Fact]
    public async Task StopAsync_AfterDispose_ShouldThrow()
    {
        // Arrange
        _mockHooks.OnDisposeAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        await _manager.DisposeAsync();

        // Act & Assert
        await Should.ThrowAsync<ObjectDisposedException>(async () =>
            await _manager.StopAsync());
    }

    #endregion

    #region Event Tests

    [Fact]
    public async Task InitializeAsync_ShouldEmitBeforeAndAfterEvents()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Initializing);
        _mockHooks.OnInitializeAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        _mockStateMachine.TryTransition(DeviceStatus.Ready)
            .Returns(Result.Success);

        var events = new List<DeviceLifecycleEvent>();
        _manager.LifecycleExecuting += (s, e) => events.Add(e);

        // Act
        await _manager.InitializeAsync();

        // Assert
        events.Count.ShouldBe(2);
        events[0].Stage.ShouldBe(LifecycleStage.Initialize);
        events[0].Phase.ShouldBe(LifecyclePhase.Before);
        events[0].Duration.ShouldBeNull();

        events[1].Stage.ShouldBe(LifecycleStage.Initialize);
        events[1].Phase.ShouldBe(LifecyclePhase.After);
        events[1].Duration.ShouldNotBeNull();
        events[1].Error.ShouldBeNull();
    }

    [Fact]
    public async Task StartAsync_ShouldEmitBeforeAndAfterEvents()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Ready);
        _mockHooks.OnStartAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Success);
        _mockStateMachine.TryTransition(DeviceStatus.Running)
            .Returns(Result.Success);

        var events = new List<DeviceLifecycleEvent>();
        _manager.LifecycleExecuting += (s, e) => events.Add(e);

        // Act
        await _manager.StartAsync();

        // Assert
        events.Count.ShouldBe(2);
        events[0].Stage.ShouldBe(LifecycleStage.Start);
        events[0].Phase.ShouldBe(LifecyclePhase.Before);

        events[1].Stage.ShouldBe(LifecycleStage.Start);
        events[1].Phase.ShouldBe(LifecyclePhase.After);
        events[1].Duration.ShouldNotBeNull();
    }

    [Fact]
    public async Task InitializeAsync_WhenFails_ShouldEmitErrorEvent()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Initializing);
        _mockHooks.OnInitializeAsync(Arg.Any<CancellationToken>())
            .Returns(Error.Failure("Hook.Failed", "Hook failed"));

        var events = new List<DeviceLifecycleEvent>();
        _manager.LifecycleExecuting += (s, e) => events.Add(e);

        // Act
        await _manager.InitializeAsync();

        // Assert
        events.Count.ShouldBe(2);
        events[1].Phase.ShouldBe(LifecyclePhase.After);
        events[1].Error.ShouldNotBeNull();
        events[1].Error.ShouldContain("Hook failed");
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public void GetStatistics_InitialState_ShouldReturnZeros()
    {
        // Act
        var stats = _manager.GetStatistics();

        // Assert
        stats.DeviceId.ShouldBe(TestDeviceId);
        stats.InitializeCount.ShouldBe(0);
        stats.StartCount.ShouldBe(0);
        stats.StopCount.ShouldBe(0);
        stats.DisposeCount.ShouldBe(0);
        stats.AverageInitializeDuration.ShouldBe(TimeSpan.Zero);
        stats.IsDisposed.ShouldBeFalse();
    }

    [Fact]
    public async Task GetStatistics_AfterOperations_ShouldTrackCorrectly()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Initializing, DeviceStatus.Ready, DeviceStatus.Running);
        _mockHooks.OnInitializeAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);
        _mockHooks.OnStartAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);
        _mockHooks.OnStopAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);
        _mockStateMachine.TryTransition(Arg.Any<DeviceStatus>()).Returns(Result.Success);

        // Act
        await _manager.InitializeAsync();
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Ready);
        await _manager.StartAsync();
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Running);
        await _manager.StopAsync();

        // Assert
        var stats = _manager.GetStatistics();
        stats.InitializeCount.ShouldBe(1);
        stats.StartCount.ShouldBe(1);
        stats.StopCount.ShouldBe(1);
        stats.DisposeCount.ShouldBe(0);
        stats.AverageInitializeDuration.ShouldBeGreaterThan(TimeSpan.Zero);
        stats.AverageStartDuration.ShouldBeGreaterThan(TimeSpan.Zero);
        stats.AverageStopDuration.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    #endregion

    #region Full Lifecycle Tests

    [Fact]
    public async Task FullLifecycle_InitializeStartStopDispose_ShouldSucceed()
    {
        // Arrange
        _mockStateMachine.CurrentStatus.Returns(
            DeviceStatus.Initializing,
            DeviceStatus.Ready,
            DeviceStatus.Running,
            DeviceStatus.Stopped);
        _mockHooks.OnInitializeAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);
        _mockHooks.OnStartAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);
        _mockHooks.OnStopAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);
        _mockHooks.OnDisposeAsync(Arg.Any<CancellationToken>()).Returns(Result.Success);
        _mockStateMachine.TryTransition(Arg.Any<DeviceStatus>()).Returns(Result.Success);

        // Act
        var initResult = await _manager.InitializeAsync();
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Ready);
        var startResult = await _manager.StartAsync();
        _mockStateMachine.CurrentStatus.Returns(DeviceStatus.Running);
        var stopResult = await _manager.StopAsync();
        var disposeResult = await _manager.DisposeAsync();

        // Assert
        initResult.IsError.ShouldBeFalse();
        startResult.IsError.ShouldBeFalse();
        stopResult.IsError.ShouldBeFalse();
        disposeResult.IsError.ShouldBeFalse();

        await _mockHooks.Received(1).OnInitializeAsync(Arg.Any<CancellationToken>());
        await _mockHooks.Received(1).OnStartAsync(Arg.Any<CancellationToken>());
        await _mockHooks.Received(1).OnStopAsync(Arg.Any<CancellationToken>());
        await _mockHooks.Received(1).OnDisposeAsync(Arg.Any<CancellationToken>());

        var stats = _manager.GetStatistics();
        stats.InitializeCount.ShouldBe(1);
        stats.StartCount.ShouldBe(1);
        stats.StopCount.ShouldBe(1);
        stats.DisposeCount.ShouldBe(1);
        stats.IsDisposed.ShouldBeTrue();
    }

    #endregion

    #region Thread-Safety Tests

    [Fact]
    public async Task ConcurrentInitialize_ShouldBeThreadSafe()
    {
        // Arrange
        var callCount = 0;
        var stateChecked = 0;
        _mockStateMachine.CurrentStatus.Returns(_ =>
        {
            // Return Initializing for first call, Ready for subsequent calls
            return Interlocked.Increment(ref stateChecked) == 1
                ? DeviceStatus.Initializing
                : DeviceStatus.Ready;
        });
        _mockHooks.OnInitializeAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                Interlocked.Increment(ref callCount);
                return Task.Delay(50).ContinueWith(_ => (ErrorOr<Success>)Result.Success);
            });
        _mockStateMachine.TryTransition(DeviceStatus.Ready)
            .Returns(Result.Success);

        // Act - Try to initialize concurrently
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(() => _manager.InitializeAsync()))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - Only one should succeed (because of lock and state check)
        var successCount = results.Count(r => !r.IsError);
        var errorCount = results.Count(r => r.IsError);

        // Due to locking, only one initialize should complete successfully
        // The others will fail because state is not Initializing
        successCount.ShouldBe(1);
        errorCount.ShouldBe(4);
        callCount.ShouldBe(1); // Hook should only be called once
    }

    #endregion
}
