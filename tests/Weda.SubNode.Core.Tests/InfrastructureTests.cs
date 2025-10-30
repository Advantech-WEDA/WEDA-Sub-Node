using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.TestBase.Builders;
using Xunit;

namespace Weda.SubNode.Core.Tests;

/// <summary>
/// Smoke tests to verify test infrastructure is working correctly
/// </summary>
public class InfrastructureTests
{
    [Fact]
    public void TestInfrastructure_Should_BeConfiguredCorrectly()
    {
        // Arrange & Act
        var configuration = DeviceConfigurationBuilder.Default().Build();

        // Assert
        configuration.ShouldNotBeNull();
        configuration.DeviceId.ShouldNotBeNullOrEmpty();
        configuration.Sensors.ShouldNotBeEmpty();
        configuration.DeviceName.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task NSubstitute_Should_WorkCorrectly()
    {
        // Arrange
        var mockComm = Substitute.For<ICommunication>();
        mockComm.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await mockComm.ConnectAsync();

        // Assert
        result.ShouldBeTrue();
        await mockComm.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Shouldly_Should_WorkCorrectly()
    {
        // Arrange
        var testValue = 42;

        // Act & Assert
        testValue.ShouldBe(42);
        testValue.ShouldBeGreaterThan(0);
        testValue.ShouldBeLessThan(100);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(5, 5, 10)]
    [InlineData(-1, 1, 0)]
    public void XUnit_Theory_Should_WorkCorrectly(int a, int b, int expected)
    {
        // Act
        var result = a + b;

        // Assert
        result.ShouldBe(expected);
    }
}
