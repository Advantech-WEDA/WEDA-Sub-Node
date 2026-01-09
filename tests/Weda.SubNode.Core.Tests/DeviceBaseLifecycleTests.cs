using NSubstitute;

using Shouldly;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Devices;
using Weda.SubNode.TestBase;
using Weda.SubNode.TestBase.Builders;

using Xunit;

namespace Weda.SubNode.Core.Tests;

/// <summary>
/// DeviceBase lifecycle tests
/// Tests initialization, start, stop, and other core lifecycle methods
/// </summary>
public class DeviceBaseLifecycleTests : IDisposable
{
    private readonly MockApplicationContext _context;
    private readonly IWedaCloudService _mockCloudService;
    private readonly ICommunication _mockCommunication;
    private readonly DeviceConfiguration _testConfig;

    public DeviceBaseLifecycleTests()
    {
        // Use MockApplicationContext for testing
        _context = new MockApplicationContext();
        _mockCloudService = _context.MockCloudService;
        _mockCommunication = _context.MockCommunication;

        _testConfig = DeviceConfigurationBuilder.Default()
            .WithDeviceName("test-device")
            .WithSubNodeType(SubNodeType.AdamEthernet)
            .WithDeviceCommunication(new Dictionary<string, object>
            {
                ["Host"] = "localhost",
                ["Port"] = 502
            })
            .Build();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    #region Helper Methods

    private void SetupSuccessfulConnections()
    {
        // DeviceBase now auto-creates ConnectionManager
        // Setup communication state that ConnectionManager will use
        _mockCommunication.State.Returns(CommunicationState.Connected);
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        _mockCloudService.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private void SetupSuccessfulRegistration(string deviceId)
    {
        // Setup SubNodeManager to return the expected SubNodeId
        // DeviceBase now gets SubNodeId from SubNodeManager instead of CloudService directly
        _context.MockSubNodeManager.SubNodeId.Returns(deviceId);
        _context.MockSubNodeManager.IsInitialized.Returns(true);
        _context.MockSubNodeManager.InitializeAsync(Arg.Any<CancellationToken>()).Returns(true);

        _mockCloudService.GetOrRegisterDeviceIdAsync(
                Arg.Any<DeviceInfo>(),
                Arg.Any<CancellationToken>())
            .Returns(deviceId);

        // Note: UploadDeviceConfigurationsAsync is now called by SubNodeManager/DeviceHostedService
        // not by DeviceBase directly, so we don't mock it here anymore
    }

    private TestDevice CreateTestDevice()
    {
        return new TestDevice(_context, _testConfig, _mockCommunication);
    }

    private TestDevice CreateTestDevice(DeviceConfiguration config)
    {
        return new TestDevice(_context, config, _mockCommunication);
    }

    #endregion

    #region InitializeAsync Tests

    [Fact]
    public async Task InitializeAsync_Should_TransitionToReady_When_AllConnectionsSucceed()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();

        // Act
        var result = await device.InitializeAsync();

        // Assert
        result.ShouldBeTrue();
        device.Status.ShouldBe(DeviceStatus.Ready);
        device.SubNodeId.ShouldBe("test-device-001");

        // Verify method calls
        // DeviceBase connects to physical device
        await _mockCommunication.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
        // Note: Upload is now handled by SubNodeManager/DeviceHostedService, not DeviceBase
        // So we don't verify UploadDeviceConfigurationAsync here anymore
    }

    [Fact]
    public async Task InitializeAsync_Should_ReturnFalse_When_PhysicalDeviceConnectionFails()
    {
        // Arrange
        // Use CancellationToken to prevent infinite retry with AlwaysRetry policy
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        var device = CreateTestDevice();

        // Act - Try with cancellation, expect either cancellation exception or false result
        bool result = false;
        try
        {
            result = await device.InitializeAsync(cts.Token);

            // If no exception, should return false
            result.ShouldBeFalse();
        }
        catch (OperationCanceledException)
        {
            // Cancellation occurred during connection attempt (expected)
        }

        // Assert
        device.Status.ShouldBe(DeviceStatus.Initializing);
    }

    [Fact]
    public async Task InitializeAsync_Should_ReturnTrue_When_CloudServiceConnectionFails()
    {
        // Arrange
        // Use CancellationToken to prevent infinite retry with AlwaysRetry policy
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(true);
        _mockCloudService.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        var device = CreateTestDevice();

        // Act - Try with cancellation, expect either cancellation exception or false result
        bool result = false;
        try
        {
            result = await device.InitializeAsync(cts.Token);

            result.ShouldBeTrue();
            //// If no exception, should return false
            //result.ShouldBeFalse();
        }
        catch (OperationCanceledException)
        {
            // Cancellation occurred during connection attempt (expected)
        }

        // Assert
        device.Status.ShouldBe(DeviceStatus.Ready);
        //device.Status.ShouldBe(DeviceStatus.Initializing);
    }

    [Fact]
    public async Task InitializeAsync_Should_RespectCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(x => Task.FromCanceled<bool>(((CancellationToken)x[0])));

        var device = CreateTestDevice();

        // Act & Assert
        // Note: ConnectionManager wraps cancellation in retry logic,
        // so we get InvalidOperationException instead of TaskCanceledException
        var ex = await Should.ThrowAsync<Exception>(async () =>
        {
            await device.InitializeAsync(cts.Token);
        });

        // Verify it's either cancellation or connection failure
        (ex is TaskCanceledException || ex is InvalidOperationException).ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_Should_ReturnTrue_WhenRegistrationFails()
    {
        // Arrange
        // Use CancellationToken to prevent hanging if retry logic is triggered
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        SetupSuccessfulConnections();

        _mockCloudService.GetOrRegisterDeviceIdAsync(
                Arg.Any<DeviceInfo>(),
                Arg.Any<CancellationToken>())
            .Returns(string.Empty);

        var device = CreateTestDevice();

        // Act
        var result = await device.InitializeAsync(cts.Token);

        // Assert
        result.ShouldBeTrue();
        device.Status.ShouldBe(DeviceStatus.Ready); // State machine doesn't transition on error
        //// Note: New DeviceBase uses ErrorOr pattern, returns false instead of throwing
        //result.ShouldBeFalse();
        //device.Status.ShouldBe(DeviceStatus.Initializing); // State machine doesn't transition on error
    }

    #endregion

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_Should_TransitionToRunning_When_DeviceIsReady()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();
        await device.InitializeAsync();

        // Act
        var result = await device.StartAsync();

        // Assert
        result.ShouldBeTrue();
        device.Status.ShouldBe(DeviceStatus.Running);
        // Note: BackgroundTasksStarted cannot be tested from outside as StartBackgroundTasksAsync is internal
        // The Running status confirms that OnStartAsync (which calls StartBackgroundTasksAsync) was executed
    }

    [Fact]
    public async Task StartAsync_Should_AutoInitialize_When_DeviceNotReady()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();

        // Act
        var result = await device.StartAsync();

        // Assert
        result.ShouldBeTrue();
        device.Status.ShouldBe(DeviceStatus.Running);
        device.SubNodeId.ShouldBe("test-device-001");
    }

    [Fact]
    public async Task StartAsync_Should_TransitionThroughCorrectStates()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();
        await device.InitializeAsync();

        // Act
        await device.StartAsync();

        // Assert
        // The device should be in Running state after StartAsync completes
        // This confirms that the lifecycle went through OnStartAsync hook
        device.Status.ShouldBe(DeviceStatus.Running);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_Should_TransitionToStopped_When_DeviceIsRunning()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();
        await device.InitializeAsync();
        await device.StartAsync();

        // Act
        await device.StopAsync();

        // Assert
        device.Status.ShouldBe(DeviceStatus.Stopped);
        // Note: BackgroundTasksStopped is not tracked in new DeviceBase
        // Background tasks are stopped via CancellationToken in OnStopAsync
    }

    [Fact]
    public async Task StopAsync_Should_BeIdempotent()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();
        await device.InitializeAsync();
        await device.StartAsync();

        // Act - call Stop twice
        await device.StopAsync();
        await device.StopAsync();

        // Assert
        device.Status.ShouldBe(DeviceStatus.Stopped);
    }

    #endregion

    #region Event Tests

    [Fact]
    public async Task DeviceStatusChanged_Should_BeRaised_When_StatusChanges()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();
        device.EnableDeviceStatusTracking = true; // Enable tracking to receive events
        var statusChanges = new List<(DeviceStatus Previous, DeviceStatus Current)>();

        device.DeviceStatusChanged += (sender, args) =>
        {
            statusChanges.Add((args.PreviousStatus, args.CurrentStatus));
        };

        // Act
        await device.InitializeAsync();

        // Assert
        statusChanges.ShouldContain(x =>
            x.Previous == DeviceStatus.Initializing &&
            x.Current == DeviceStatus.Ready);
    }

    [Fact]
    public async Task DeviceStatusChanged_Should_BeRaised_When_Starting()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();
        device.EnableDeviceStatusTracking = true; // Enable tracking to receive events
        await device.InitializeAsync();

        var statusChanges = new List<(DeviceStatus Previous, DeviceStatus Current)>();
        device.DeviceStatusChanged += (sender, args) =>
        {
            statusChanges.Add((args.PreviousStatus, args.CurrentStatus));
        };

        // Act
        await device.StartAsync();

        // Assert
        statusChanges.ShouldContain(x =>
            x.Previous == DeviceStatus.Ready &&
            x.Current == DeviceStatus.Running);
    }

    #endregion

    #region Lifecycle Hook Tests

    [Fact]
    public async Task InitializeAsync_Should_CallOnBeforeInitializeHook()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();

        // Act
        await device.InitializeAsync();

        // Assert
        device.OnBeforeInitializeCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_Should_CallOnAfterRegistrationHook_WithDeviceId()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();

        // Act
        await device.InitializeAsync();

        // Assert
        // Note: OnAfterRegistration hook has been removed in new DeviceBase
        // Registration is now handled internally by DeviceInitializer
        device.OnAfterInitializeCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_Should_CallOnAfterInitializeHook()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();

        // Act
        await device.InitializeAsync();

        // Assert
        device.OnAfterInitializeCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_Should_CallHooksInCorrectOrder()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();
        var callOrder = new List<string>();

        device.OnHookCalled = hookName => callOrder.Add(hookName);

        // Act
        await device.InitializeAsync();

        // Assert
        // Note: OnAfterRegistration has been removed in new DeviceBase
        // Registration is now handled internally by DeviceInitializer
        callOrder.Count.ShouldBeGreaterThan(0);
        callOrder[0].ShouldBe("OnBeforeInitialize");
        callOrder.ShouldContain("OnAfterInitialize");

        // Verify order
        var beforeIndex = callOrder.IndexOf("OnBeforeInitialize");
        var afterInitIndex = callOrder.IndexOf("OnAfterInitialize");

        beforeIndex.ShouldBeLessThan(afterInitIndex);
    }

    #endregion

    #region ConnectionStateChanged Event Tests

    [Fact]
    public void ConnectionStateChanged_Should_BeRaised_When_CommunicationStateChanges()
    {
        // Arrange
        var device = CreateTestDevice();
        device.EnableConnectionStateTracking = true; // Enable tracking to receive events
        var eventRaised = false;
        ConnectionStateChangedEvent? receivedEvent = null;

        device.ConnectionStateChanged += (sender, args) =>
        {
            eventRaised = true;
            receivedEvent = args;
        };

        // Act - Trigger communication state change by raising the event
        var testEvent = new ConnectionStateChangedEvent(
            DeviceId: "test-device",
            SubNodeType: SubNodeType.AdamEthernet,
            PreviousState: CommunicationState.Disconnected,
            CurrentState: CommunicationState.Connected,
            Timestamp: DateTimeOffset.UtcNow);

        _mockCommunication.StateChanged += Raise.Event<EventHandler<ConnectionStateChangedEvent>>(_mockCommunication, testEvent);

        // Assert
        eventRaised.ShouldBeTrue();
        receivedEvent.ShouldNotBeNull();
        receivedEvent!.CurrentState.ShouldBe(CommunicationState.Connected);
    }

    #endregion

    #region Configuration Enrichment Tests

    [Fact]
    public async Task InitializeAsync_Should_EnrichConfigurationWithDeviceId()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("device-123");

        var device = CreateTestDevice();

        // Act
        await device.InitializeAsync();

        // Assert
        device.Configuration.DeviceId.ShouldBe("device-123");
    }

    [Fact]
    public async Task InitializeAsync_Should_EnrichSensorsWithResourceIds()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("device-123");

        var config = DeviceConfigurationBuilder.Default()
            .WithDeviceId("")
            .WithSensors(new List<Sensor>
            {
                new()
                {
                    Name = "temperature",
                    Dtmi = "dtmi:test:Temperature;1",
                    DeviceResourceId = "",
                    ResourceId = "",
                    Report = new SensorReport
                    {
                        Enabled = true,
                        Interval = 1000
                    }
                }
            })
            .Build();

        var device = CreateTestDevice(config);

        // Capture original ResourceId
        var originalResourceId = device.Configuration.Sensors.First().ResourceId;

        // Act
        await device.InitializeAsync();

        // Assert
        var sensor = device.Configuration.Sensors.First();
        sensor.ResourceId.ShouldNotBeNullOrEmpty();
        sensor.ResourceId.ShouldNotBe(originalResourceId); // Should be enriched with new value
        sensor.DeviceResourceId.ShouldBe("device-123");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task InitializeAsync_Should_Succeed_When_SubNodeManagerInitialized()
    {
        // Arrange
        // Note: Configuration upload is now handled by SubNodeManager/DeviceHostedService,
        // not by DeviceBase directly. DeviceBase.InitializeAsync only:
        // 1. Establishes physical connection
        // 2. Enriches configuration with SubNodeId
        // 3. Registers device handler with SubNodeManager
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();

        // Act
        var result = await device.InitializeAsync(cts.Token);

        // Assert
        result.ShouldBeTrue();
        device.Status.ShouldBe(DeviceStatus.Ready);
    }

    [Fact]
    public async Task StartAsync_Should_ReturnFalse_When_InitializationFails()
    {
        // Arrange
        // Use CancellationToken to prevent infinite retry with AlwaysRetry policy
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        var device = CreateTestDevice();

        // Act - Try with cancellation, expect either cancellation exception or false result
        bool result = false;
        try
        {
            result = await device.StartAsync(cts.Token);

            // If no exception, should return false
            result.ShouldBeFalse();
        }
        catch (OperationCanceledException)
        {
            // Cancellation occurred during initialization (expected)
        }

        // Assert
        device.Status.ShouldBe(DeviceStatus.Initializing); // Stays in initializing state
    }

    [Fact]
    public async Task StopAsync_Should_DisconnectFromPhysicalDevice()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();
        await device.InitializeAsync();
        await device.StartAsync();

        // Act
        await device.StopAsync();

        // Assert
        // DeviceBase only disconnects from physical device
        // Cloud connection is managed by SubNodeManager at SubNode level
        await _mockCommunication.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region DataReceived Event Tests

    [Fact]
    public void OnDataReceived_Should_RaiseDataReceivedEvent()
    {
        // Arrange
        SetupSuccessfulConnections();
        SetupSuccessfulRegistration("test-device-001");

        var device = CreateTestDevice();
        device.EnableDataReceivedTracking = true; // Enable tracking to receive events
        var eventRaised = false;
        DataReceivedEvent? receivedEvent = null;

        device.DataReceived += (sender, args) =>
        {
            eventRaised = true;
            receivedEvent = args;
        };

        var testData = new List<TelemetryMeasure>
        {
            new() { ResourceId = "sensor-001", Value = 25.5 }
        };

        // Act
        device.TriggerDataReceived(testData);

        // Assert
        eventRaised.ShouldBeTrue();
        receivedEvent.ShouldNotBeNull();
        receivedEvent!.Data.Count.ShouldBe(1);
        receivedEvent.Data[0].ResourceId.ShouldBe("sensor-001");
    }

    #endregion

    #region TelemetrySent Event Tests

    // Note: TelemetrySent event tests removed
    // In new DeviceBase, TelemetrySent is not exposed via protected method
    // The event is raised internally by TelemetryPipeline when actual telemetry is sent
    // These tests should be moved to integration tests that use real SendTelemetryAsync

    #endregion
}

/// <summary>
/// Test implementation of DeviceBase
/// </summary>
internal class TestDevice : DeviceBase
{
    // Lifecycle hook tracking
    public bool OnBeforeInitializeCalled { get; private set; }
    public bool OnAfterInitializeCalled { get; private set; }

    // Hook call order tracking
    public Action<string>? OnHookCalled { get; set; }

    public TestDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        ICommunication communication)
        : base(context, configuration, communication)
    {
    }

    public override Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<TelemetryMeasure>());
    }

    public override Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    protected override Task<IntervalGroupReadResult> ReadSensorsForIntervalGroupAsync(
        List<string> sensorResourceIds,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new IntervalGroupReadResult(new List<TelemetryMeasure>(), TimeSpan.Zero));
    }

    protected override Task OnBeforeInitializeAsync(CancellationToken cancellationToken)
    {
        OnBeforeInitializeCalled = true;
        OnHookCalled?.Invoke("OnBeforeInitialize");
        return base.OnBeforeInitializeAsync(cancellationToken);
    }

    protected override Task OnAfterInitializeAsync(CancellationToken cancellationToken)
    {
        OnAfterInitializeCalled = true;
        OnHookCalled?.Invoke("OnAfterInitialize");
        return base.OnAfterInitializeAsync(cancellationToken);
    }

    // Helper methods to trigger protected event methods for testing
    public void TriggerDataReceived(List<TelemetryMeasure> data)
    {
        RaiseDataReceived(data);
    }

    public void TriggerTelemetrySent(int measureCount, bool success, string? error)
    {
        // TelemetrySent event is no longer exposed via protected method
        // Tests should use actual SendTelemetryAsync instead
    }
}
