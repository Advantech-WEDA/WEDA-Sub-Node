using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Managers;
using Weda.SubNode.Core.Policies;
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
    public async Task EstablishPhysicalConnectionAsync_Should_RetryUntilCancelled_When_DeviceConnectionFails()
    {
        // Arrange
        // Default policy uses AlwaysRetry (unlimited retries), so we use CancellationToken to stop
        var connectionAttempts = 0;
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                connectionAttempts++;
                return false;
            });

        var manager = new DeviceConnectionManager(_mockCommunication);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // Act
        var result = await manager.EstablishPhysicalConnectionAsync(cts.Token);

        // Assert
        // When cancelled, Polly returns error result instead of throwing
        result.IsError.ShouldBeTrue();
        // Verify multiple connection attempts were made (retry behavior)
        connectionAttempts.ShouldBeGreaterThan(1);
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);
    }

    [Fact]
    public async Task EstablishPhysicalConnectionAsync_Should_ReturnError_AfterMaxRetries_WithLimitedRetryPolicy()
    {
        // Arrange
        var connectionAttempts = 0;
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                connectionAttempts++;
                return false;
            });

        // Create limited retry pipeline directly (2 retries = 3 total attempts)
        var limitedPipeline = RetryPolicyFactory.CreateNTimeRetryBool(
            NullLogger.Instance,
            maxRetries: 2,
            initialDelay: TimeSpan.FromMilliseconds(10),
            maxDelay: TimeSpan.FromMilliseconds(50));

        var manager = new DeviceConnectionManager(
            _mockCommunication,
            limitedPipeline);

        // Act
        var result = await manager.EstablishPhysicalConnectionAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);
        // Should have attempted: 1 initial + 2 retries = 3 total attempts
        connectionAttempts.ShouldBe(3);
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
    public async Task EstablishPhysicalConnectionAsync_Should_SucceedAfterRetry()
    {
        // Arrange - fail twice, then succeed on 3rd attempt
        var attempts = 0;
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;
                return attempts >= 3; // Succeed on 3rd attempt
            });

        var manager = new DeviceConnectionManager(_mockCommunication);

        // Act
        var result = await manager.EstablishPhysicalConnectionAsync();

        // Assert
        result.IsError.ShouldBeFalse();
        attempts.ShouldBe(3);
        manager.CurrentState.ShouldBe(CommunicationState.Connected);
    }

    [Fact]
    public async Task EstablishPhysicalConnectionAsync_Should_HandleException_WithLimitedRetryPolicy()
    {
        // Arrange
        var exceptionCount = 0;
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns<bool>(_ =>
            {
                exceptionCount++;
                throw new InvalidOperationException("Connection failed");
            });

        // Create limited retry pipeline directly (2 retries = 3 total attempts)
        var limitedPipeline = RetryPolicyFactory.CreateNTimeRetryBool(
            NullLogger.Instance,
            maxRetries: 2,
            initialDelay: TimeSpan.FromMilliseconds(10),
            maxDelay: TimeSpan.FromMilliseconds(50));

        var manager = new DeviceConnectionManager(
            _mockCommunication,
            limitedPipeline);

        // Act
        var result = await manager.EstablishPhysicalConnectionAsync();

        // Assert
        result.IsError.ShouldBeTrue();
        manager.CurrentState.ShouldBe(CommunicationState.Disconnected);
        exceptionCount.ShouldBe(3); // 1 initial + 2 retries
    }

    [Fact]
    public async Task EstablishPhysicalConnectionAsync_Should_RecoverFromException_OnRetry()
    {
        // Arrange - throw exception twice, then succeed
        var attempts = 0;
        _mockCommunication.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns<bool>(_ =>
            {
                attempts++;
                if (attempts < 3)
                    throw new InvalidOperationException("Connection failed");
                return true; // Succeed on 3rd attempt
            });

        var manager = new DeviceConnectionManager(_mockCommunication);

        // Act
        var result = await manager.EstablishPhysicalConnectionAsync();

        // Assert
        result.IsError.ShouldBeFalse();
        attempts.ShouldBe(3);
        manager.CurrentState.ShouldBe(CommunicationState.Connected);
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
