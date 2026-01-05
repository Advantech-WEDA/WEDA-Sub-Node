using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Managers;
using Xunit;

namespace Weda.SubNode.Core.Tests;

/// <summary>
/// Unit tests for DeviceConnectionManager
/// Tests physical device connection management with Polly resilience policies.
/// Note: Cloud connection and event subscription are now handled by SubNodeManager.
/// </summary>
public class DeviceConnectionManagerTests
{
    private readonly ICommunication _mockCommunication;

    public DeviceConnectionManagerTests()
    {
        _mockCommunication = Substitute.For<ICommunication>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Should_ThrowException_When_CommunicationIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
        {
            new DeviceConnectionManager(null!);
        });
    }

    [Fact]
    public void Constructor_Should_InitializeWithDisconnectedState()
    {
        // Act
        var manager = new DeviceConnectionManager(_mockCommunication);

        // Assert
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);
    }

    #endregion

    #region EstablishPhysicalConnectionAsync Tests

    [Fact]
    public async Task EstablishPhysicalConnectionAsync_Should_ConnectSuccessfully_When_DeviceConnectionSucceeds()
    {
        // Arrange
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);

        var manager = new DeviceConnectionManager(_mockCommunication);

        // Act
        var result = await manager.EstablishPhysicalConnectionAsync();

        // Assert
        result.IsError.ShouldBeFalse();
        manager.CurrentState.ShouldBe(CommunicationState.Connected);

        await _mockCommunication.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EstablishPhysicalConnectionAsync_Should_ReturnError_When_DeviceConnectionFails()
    {
        // Arrange
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(false);

        var manager = new DeviceConnectionManager(_mockCommunication);

        // Act
        var result = await manager.EstablishPhysicalConnectionAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);
    }

    [Fact]
    public async Task EstablishPhysicalConnectionAsync_Should_SetConnectingState_DuringConnection()
    {
        // Arrange
        var tcs = new TaskCompletionSource<bool>();
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(_ => tcs.Task);

        var manager = new DeviceConnectionManager(_mockCommunication);

        // Act
        var connectTask = manager.EstablishPhysicalConnectionAsync();

        // Assert - during connection
        manager.CurrentState.ShouldBe(CommunicationState.Connecting);

        // Complete the connection
        tcs.SetResult(true);
        await connectTask;

        // Assert - after connection
        manager.CurrentState.ShouldBe(CommunicationState.Connected);
    }

    [Fact]
    public async Task EstablishPhysicalConnectionAsync_Should_HandleException_AndReturnError()
    {
        // Arrange
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException("Connection failed"));

        var manager = new DeviceConnectionManager(_mockCommunication);

        // Act
        var result = await manager.EstablishPhysicalConnectionAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);
    }

    #endregion

    #region DisconnectAsync Tests

    [Fact]
    public async Task DisconnectAsync_Should_DisconnectSuccessfully()
    {
        // Arrange
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>()).Returns(true);
        _mockCommunication.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var manager = new DeviceConnectionManager(_mockCommunication);
        await manager.EstablishPhysicalConnectionAsync();

        // Act
        await manager.DisconnectAsync();

        // Assert
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);
        await _mockCommunication.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
    }

    #endregion
}
