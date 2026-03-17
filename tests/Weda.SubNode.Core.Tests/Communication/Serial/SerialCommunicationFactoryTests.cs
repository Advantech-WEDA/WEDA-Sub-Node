using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication.Serial;
using Xunit;

namespace Weda.SubNode.Core.Tests.Communication.Serial;

/// <summary>
/// Tests for SerialCommunicationFactory
/// Verifies Multiton pattern, reference counting, and settings validation
/// </summary>
public class SerialCommunicationFactoryTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly SerialCommunicationFactory _factory;

    public SerialCommunicationFactoryTests()
    {
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>())
            .Returns(Substitute.For<ILogger>());

        _factory = new SerialCommunicationFactory(_loggerFactory);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    #region Multiton Pattern Tests

    [Fact]
    public void GetOrCreate_SamePort_ShouldReturnSameInstance()
    {
        // Arrange
        var settings = CreateSettings("/dev/ttyUSB0");

        // Act
        var instance1 = _factory.GetOrCreate("/dev/ttyUSB0", settings);
        var instance2 = _factory.GetOrCreate("/dev/ttyUSB0", settings);

        // Assert
        instance1.ShouldBeSameAs(instance2);
    }

    [Fact]
    public void GetOrCreate_DifferentPorts_ShouldReturnDifferentInstances()
    {
        // Arrange
        var settings1 = CreateSettings("/dev/ttyUSB0");
        var settings2 = CreateSettings("/dev/ttyUSB1");

        // Act
        var instance1 = _factory.GetOrCreate("/dev/ttyUSB0", settings1);
        var instance2 = _factory.GetOrCreate("/dev/ttyUSB1", settings2);

        // Assert
        instance1.ShouldNotBeSameAs(instance2);
    }

    #endregion

    #region Reference Counting Tests

    [Fact]
    public void GetOrCreate_MultipleCallsSamePort_ShouldIncrementRefCount()
    {
        // Arrange
        var settings = CreateSettings("/dev/ttyUSB0");

        // Act
        _factory.GetOrCreate("/dev/ttyUSB0", settings);
        _factory.GetOrCreate("/dev/ttyUSB0", settings);
        _factory.GetOrCreate("/dev/ttyUSB0", settings);

        // Assert
        _factory.GetReferenceCount("/dev/ttyUSB0").ShouldBe(3);
    }

    [Fact]
    public void Release_ShouldDecrementRefCount()
    {
        // Arrange
        var settings = CreateSettings("/dev/ttyUSB0");
        _factory.GetOrCreate("/dev/ttyUSB0", settings);
        _factory.GetOrCreate("/dev/ttyUSB0", settings);

        // Act
        _factory.Release("/dev/ttyUSB0");

        // Assert
        _factory.GetReferenceCount("/dev/ttyUSB0").ShouldBe(1);
    }

    [Fact]
    public void Release_WhenRefCountZero_ShouldRemoveInstance()
    {
        // Arrange
        var settings = CreateSettings("/dev/ttyUSB0");
        _factory.GetOrCreate("/dev/ttyUSB0", settings);

        // Act
        _factory.Release("/dev/ttyUSB0");

        // Assert
        _factory.Contains("/dev/ttyUSB0").ShouldBeFalse();
        _factory.GetReferenceCount("/dev/ttyUSB0").ShouldBe(0);
    }

    [Fact]
    public void Release_NonExistentPort_ShouldReturnFalse()
    {
        // Act
        var result = _factory.Release("/dev/nonexistent");

        // Assert
        result.ShouldBeFalse();
    }

    #endregion

    #region Settings Validation Tests

    [Fact]
    public void GetOrCreate_IncompatibleBaudRate_ShouldThrow()
    {
        // Arrange
        var settings1 = CreateSettings("/dev/ttyUSB0", baudRate: 9600);
        var settings2 = CreateSettings("/dev/ttyUSB0", baudRate: 115200);

        _factory.GetOrCreate("/dev/ttyUSB0", settings1);

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() =>
            _factory.GetOrCreate("/dev/ttyUSB0", settings2));

        ex.Message.ShouldContain("BaudRate");
    }

    [Fact]
    public void GetOrCreate_IncompatibleParity_ShouldThrow()
    {
        // Arrange
        var settings1 = CreateSettings("/dev/ttyUSB0");
        settings1.Parity = System.IO.Ports.Parity.None;

        var settings2 = CreateSettings("/dev/ttyUSB0");
        settings2.Parity = System.IO.Ports.Parity.Even;

        _factory.GetOrCreate("/dev/ttyUSB0", settings1);

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() =>
            _factory.GetOrCreate("/dev/ttyUSB0", settings2));

        ex.Message.ShouldContain("Parity");
    }

    [Fact]
    public void GetOrCreate_NullPortName_ShouldThrow()
    {
        // Arrange
        var settings = CreateSettings("/dev/ttyUSB0");

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            _factory.GetOrCreate(null!, settings));
    }

    [Fact]
    public void GetOrCreate_NullSettings_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            _factory.GetOrCreate("/dev/ttyUSB0", null!));
    }

    #endregion

    #region Contains Tests

    [Fact]
    public void Contains_ExistingPort_ShouldReturnTrue()
    {
        // Arrange
        var settings = CreateSettings("/dev/ttyUSB0");
        _factory.GetOrCreate("/dev/ttyUSB0", settings);

        // Act & Assert
        _factory.Contains("/dev/ttyUSB0").ShouldBeTrue();
    }

    [Fact]
    public void Contains_NonExistentPort_ShouldReturnFalse()
    {
        // Act & Assert
        _factory.Contains("/dev/nonexistent").ShouldBeFalse();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldClearAllInstances()
    {
        // Arrange
        var settings1 = CreateSettings("/dev/ttyUSB0");
        var settings2 = CreateSettings("/dev/ttyUSB1");
        _factory.GetOrCreate("/dev/ttyUSB0", settings1);
        _factory.GetOrCreate("/dev/ttyUSB1", settings2);

        // Act
        _factory.Dispose();

        // Assert
        _factory.Contains("/dev/ttyUSB0").ShouldBeFalse();
        _factory.Contains("/dev/ttyUSB1").ShouldBeFalse();
    }

    #endregion

    #region Helper Methods

    private static SerialCommunicationSettings CreateSettings(
        string portName,
        int baudRate = 9600)
    {
        return new SerialCommunicationSettings
        {
            PortName = portName,
            BaudRate = baudRate,
            DataBits = 8,
            Parity = System.IO.Ports.Parity.None,
            StopBits = System.IO.Ports.StopBits.One
        };
    }

    #endregion
}