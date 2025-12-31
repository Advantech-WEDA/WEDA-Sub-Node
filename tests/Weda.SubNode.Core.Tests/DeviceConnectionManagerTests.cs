using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Cloud.Subscriptions;
using Weda.SubNode.Abstractions.Communication;
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

    public DeviceConnectionManagerTests()
    {
        _mockCommunication = Substitute.For<ICommunication>();
        _mockCloudService = Substitute.For<IWedaCloudService>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowException_When_CommunicationIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
        {
            new DeviceConnectionManager(null!, _mockCloudService);
        });
    }

    [Fact]
    public void Constructor_Should_ThrowException_When_CloudServiceIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
        {
            new DeviceConnectionManager(_mockCommunication, null!);
        });
    }

    [Fact]
    public void Constructor_Should_InitializeWithDisconnectedState()
    {
        // Act
        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService);

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

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService);

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
        // Use CancellationToken to prevent infinite retry with AlwaysRetry policy
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(false);

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService);

        // Act - Try with cancellation, expect either cancellation exception or error result
        try
        {
            var result = await manager.EstablishConnectionsAsync(cts.Token);

            // If no exception, must be an error result (cancellation during retry delay)
            result.IsError.ShouldBeTrue();
        }
        catch (OperationCanceledException)
        {
            // Cancellation occurred during connection attempt (expected)
        }

        // Assert
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);

        // Should have attempted connection with Polly policies
        await _mockCommunication.Received().ConnectAsync(Arg.Any<CancellationToken>());
        // Should not try cloud service if physical device fails
        await _mockCloudService.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EstablishConnectionsAsync_Should_Fail_When_CloudServiceConnectionFails()
    {
        // Arrange
        // Use CancellationToken to prevent infinite retry with AlwaysRetry policy
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        _mockCloudService.ConnectAsync(Arg.Any<CancellationToken>()).Returns(false);

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService);

        // Act - Try with cancellation, expect either cancellation exception or error result
        try
        {
            var result = await manager.EstablishConnectionsAsync(cts.Token);

            // If no exception, must be an error result (cancellation during retry delay)
            result.IsError.ShouldBeTrue();
        }
        catch (OperationCanceledException)
        {
            // Cancellation occurred during connection attempt (expected)
        }

        // Assert
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);

        await _mockCommunication.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
        await _mockCloudService.Received().ConnectAsync(Arg.Any<CancellationToken>());
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

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService);

        // Act
        var result = await manager.EstablishConnectionsAsync();

        // Assert
        result.IsError.ShouldBeFalse();
        callCount.ShouldBeGreaterThanOrEqualTo(3); // Retried with Polly policies, succeeded eventually
        manager.CurrentState.ShouldBe(CommunicationState.Connected);
    }

    [Fact]
    public async Task EstablishConnectionsAsync_Should_AcceptCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        _mockCloudService.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService);

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
        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService);

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

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService);
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

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService);
        await manager.EstablishConnectionsAsync();

        var eventRaised = false;
        manager.ConfigurationUpdateReceived += (@event) =>
        {
            eventRaised = true;
            return Task.CompletedTask;
        };

        await manager.SubscribeToCloudEventsAsync("device-001");

        // Act
        var testMessage = new SubNodeConfigurationUpdateMessage
        {
            DeviceId = "device-001",
            Cmd = "updateCmd",
            SeqId = 1
        };
        var testEvent = new UpdateConfigurationEvent(
            DeviceId: "device-001",
            ConfigType: SubscriptionTypes.DeviceConfig,
            Message: testMessage,
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

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService);
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

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await manager.DisconnectAsync();
        });
    }

    #endregion

    #region Retry Logic Tests

    [Fact]
    public async Task RetryLogic_Should_RetryWithPollyPipeline()
    {
        // Arrange
        // Use CancellationToken to prevent infinite retry with AlwaysRetry policy
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var attemptCount = 0;
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                attemptCount++;
                return false;
            });

        var manager = new DeviceConnectionManager(_mockCommunication, _mockCloudService);

        // Act - Try with cancellation, expect either cancellation exception or error result
        try
        {
            var result = await manager.EstablishConnectionsAsync(cts.Token);

            // If no exception, must be an error result (cancellation during retry delay)
            result.IsError.ShouldBeTrue();
        }
        catch (OperationCanceledException)
        {
            // Cancellation occurred during connection attempt (expected)
        }

        // Assert
        // Polly will retry multiple times before cancellation or timeout occurs
        attemptCount.ShouldBeGreaterThan(1);
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);
    }

    #endregion
}
