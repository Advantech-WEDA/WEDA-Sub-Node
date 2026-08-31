using Shouldly;

using Weda.SubNode.Core.Cloud;

using Xunit;

namespace Weda.SubNode.Core.Tests.Cloud;

/// <summary>
/// Tests for <see cref="MockCloudService"/>, the in-process fake used by SDK
/// consumers to run devices/managers offline and in their own unit tests.
/// </summary>
public class MockCloudServiceTests
{
    /// <summary>
    /// A .NET event can only be raised from inside its declaring type, so consumers
    /// wiring a <c>ConnectionRestored</c> handler against the mock have no way to fire
    /// it. <see cref="MockCloudService.SimulateConnectionRestoredAsync"/> is that seam.
    /// This test exercises it and, in doing so, is the in-repo caller that keeps it live.
    /// </summary>
    [Fact]
    public async Task SimulateConnectionRestoredAsync_InvokesAllSubscribedHandlers()
    {
        // Arrange
        using var cloud = new MockCloudService();
        var firstCalled = 0;
        var secondCalled = 0;
        cloud.ConnectionRestored += () => { firstCalled++; return Task.CompletedTask; };
        cloud.ConnectionRestored += () => { secondCalled++; return Task.CompletedTask; };

        // Act
        await cloud.SimulateConnectionRestoredAsync();

        // Assert
        firstCalled.ShouldBe(1);
        secondCalled.ShouldBe(1);
    }

    [Fact]
    public async Task SimulateConnectionRestoredAsync_NoSubscribers_DoesNotThrow()
    {
        using var cloud = new MockCloudService();

        await Should.NotThrowAsync(() => cloud.SimulateConnectionRestoredAsync());
    }
}
