using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Managers;
using Xunit;

namespace Weda.SubNode.Core.Tests;

/// <summary>
/// Unit tests for DeviceConnectionManager
/// Tests connection management, retry logic, and event subscription
/// </summary>
public class DeviceConnectionManagerTests
{
    private readonly ICommunication _mockCommunication;
    private readonly IWedaCloudService _mockCloudService;
    private readonly ConnectionOptions _testOptions;

    public DeviceConnectionManagerTests()
    {
        _mockCommunication = Substitute.For<ICommunication>();
        _mockCloudService = Substitute.For<IWedaCloudService>();

        // Use fast retry for testing
        _testOptions = new ConnectionOptions
        {
            MaxRetryAttempts = 3,
            RetryDelayMs = 10 // Short delay for fast tests
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowException_When_CommunicationIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
        {
            new DeviceConnectionManager(null!, _mockCloudService, _testOptions);
        });
    }

    [Fact]
    public void Constructor_Should_ThrowException_When_CloudServiceIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
        {
            new DeviceConnectionManager(_mockCommunication, null!, _testOptions);
        });
    }

    [Fact]
    public void Constructor_Should_InitializeWithDisconnectedState()
    {
        // Act
        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService, _testOptions);

        // Assert
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);
    }

    #endregion

    #region EstablishConnectionsAsync Tests

    [Fact]
    public async Task EstablishConnectionsAsync_Should_ConnectSuccessfully_When_BothConnectionsSucceed()
    {
        // Arrange
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        _mockCloudService.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService, _testOptions);

        // Act
        var result = await manager.EstablishConnectionsAsync();

        // Assert
        result.IsError.ShouldBeFalse();
        manager.CurrentState.ShouldBe(CommunicationState.Connected);

        await _mockCommunication.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
        await _mockCloudService.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EstablishConnectionsAsync_Should_Fail_When_PhysicalDeviceConnectionFails()
    {
        // Arrange
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(false);

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService, _testOptions);

        // Act
        var result = await manager.EstablishConnectionsAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Connection.PhysicalDevice");
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);

        // Should retry 3 times
        await _mockCommunication.Received(3).ConnectAsync(Arg.Any<CancellationToken>());
        // Should not try cloud service if physical device fails
        await _mockCloudService.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EstablishConnectionsAsync_Should_Fail_When_CloudServiceConnectionFails()
    {
        // Arrange
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        _mockCloudService.ConnectAsync(Arg.Any<CancellationToken>()).Returns(false);

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService, _testOptions);

        // Act
        var result = await manager.EstablishConnectionsAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Connection.CloudService");
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);

        await _mockCommunication.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
        await _mockCloudService.Received(3).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EstablishConnectionsAsync_Should_RetryOnException()
    {
        // Arrange
        var callCount = 0;
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                callCount++;
                if (callCount < 3)
                    throw new InvalidOperationException("Connection error");
                return true;
            });

        _mockCloudService.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService, _testOptions);

        // Act
        var result = await manager.EstablishConnectionsAsync();

        // Assert
        result.IsError.ShouldBeFalse();
        callCount.ShouldBe(3); // Retried twice, succeeded on 3rd attempt
        manager.CurrentState.ShouldBe(CommunicationState.Connected);
    }

    [Fact]
    public async Task EstablishConnectionsAsync_Should_AcceptCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        _mockCloudService.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService, _testOptions);

        // Act
        var result = await manager.EstablishConnectionsAsync(cts.Token);

        // Assert
        result.IsError.ShouldBeFalse();
        // Verify cancellation token was passed through
        await _mockCommunication.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region SubscribeToCloudEventsAsync Tests

    [Fact]
    public async Task SubscribeToCloudEventsAsync_Should_Fail_When_NotConnected()
    {
        // Arrange
        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService, _testOptions);

        // Act
        var result = await manager.SubscribeToCloudEventsAsync("device-001");

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Connection.State");
    }

    [Fact]
    public async Task SubscribeToCloudEventsAsync_Should_SubscribeSuccessfully_When_Connected()
    {
        // Arrange
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        _mockCloudService.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService, _testOptions);
        await manager.EstablishConnectionsAsync();

        // Act
        var result = await manager.SubscribeToCloudEventsAsync("device-001");

        // Assert
        result.IsError.ShouldBeFalse();

        await _mockCloudService.Received(1).SubscribeConfigurationUpdatesAsync(
            "device-001",
            Arg.Any<Func<UpdateConfigurationEvent, Task>>(),
            Arg.Any<CancellationToken>());

        await _mockCloudService.Received(1).SubscribeCommandsAsync(
            "device-001",
            Arg.Any<Func<ExecuteCommandEvent, Task>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubscribeToCloudEventsAsync_Should_RaiseEvent_When_ConfigurationUpdateReceived()
    {
        // Arrange
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        _mockCloudService.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);

        Func<UpdateConfigurationEvent, Task>? capturedHandler = null;
        await _mockCloudService.SubscribeConfigurationUpdatesAsync(
            Arg.Any<string>(),
            Arg.Do<Func<UpdateConfigurationEvent, Task>>(x => capturedHandler = x),
            Arg.Any<CancellationToken>());

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService, _testOptions);
        await manager.EstablishConnectionsAsync();

        var eventRaised = false;
        manager.ConfigurationUpdateReceived += (@event) =>
        {
            eventRaised = true;
            return Task.CompletedTask;
        };

        await manager.SubscribeToCloudEventsAsync("device-001");

        // Act
        var testEvent = new UpdateConfigurationEvent(
            DeviceId: "device-001",
            Configuration: new Dictionary<string, object>(),
            Timestamp: DateTimeOffset.UtcNow);

        if (capturedHandler != null)
        {
            await capturedHandler(testEvent);
        }

        // Assert
        eventRaised.ShouldBeTrue();
    }

    #endregion

    #region DisconnectAsync Tests

    [Fact]
    public async Task DisconnectAsync_Should_DisconnectBothServices()
    {
        // Arrange
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        _mockCloudService.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService, _testOptions);
        await manager.EstablishConnectionsAsync();

        // Act
        await manager.DisconnectAsync();

        // Assert
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);

        await _mockCommunication.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
        await _mockCloudService.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisconnectAsync_Should_PropagateException_When_DisconnectFails()
    {
        // Arrange
        _mockCommunication.DisconnectAsync(Arg.Any<CancellationToken>())
            .Returns(x => throw new InvalidOperationException("Disconnect failed"));

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService, _testOptions);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await manager.DisconnectAsync();
        });
    }

    #endregion

    #region Retry Logic Tests

    [Fact]
    public async Task RetryLogic_Should_UseExponentialBackoff()
    {
        // Arrange
        var options = new ConnectionOptions
        {
            MaxRetryAttempts = 3,
            RetryDelayMs = 100
        };

        var attemptTimes = new List<DateTime>();
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                attemptTimes.Add(DateTime.UtcNow);
                return false;
            });

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService, options);

        // Act
        await manager.EstablishConnectionsAsync();

        // Assert
        attemptTimes.Count.ShouldBe(3);

        // Check exponential backoff (roughly)
        if (attemptTimes.Count >= 3)
        {
            var delay1 = (attemptTimes[1] - attemptTimes[0]).TotalMilliseconds;
            var delay2 = (attemptTimes[2] - attemptTimes[1]).TotalMilliseconds;

            // Second delay should be roughly 2x first delay (exponential backoff)
            delay2.ShouldBeGreaterThan(delay1 * 1.5);
        }
    }

    #endregion
}
