using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.Cfx;

using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

public class CfxSensorParametersTests
{
    private const string MessageName = "CFX.Production.WorkStarted";

    [Fact]
    public void GetMessageName_WithParameter_ReturnsIt() =>
        Assert.Equal(MessageName, SensorWith(MessageName).GetMessageName());

    [Fact]
    public void GetMessageName_MissingParameter_ThrowsNamingTheSensor()
    {
        // Arrange
        var sensor = SensorWith(null);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => sensor.GetMessageName());

        // Assert
        Assert.Contains(CfxSensorParameters.MessageNameKey, ex.Message);
        Assert.Contains("test_sensor", ex.Message);
    }

    [Fact]
    public void GetMessageName_NullParameterBag_ThrowsRatherThanDereferencing()
    {
        // Arrange
        var sensor = SensorWith(null);
        sensor.Parameters = null!;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => sensor.GetMessageName());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetMessageName_BlankParameter_Throws(string value)
    {
        // Arrange
        var sensor = SensorWith(value);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => sensor.GetMessageName());

        // Assert
        Assert.Contains("blank", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetMessageName_NullSensor_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ((Sensor)null!).GetMessageName());

    [Fact]
    public void TryGetMessageName_WithParameter_ReturnsTrueAndValue()
    {
        Assert.True(SensorWith(MessageName).TryGetMessageName(out var messageName));
        Assert.Equal(MessageName, messageName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetMessageName_WithoutUsableParameter_ReturnsFalse(string? value)
    {
        Assert.False(SensorWith(value).TryGetMessageName(out var messageName));
        Assert.Equal(string.Empty, messageName);
    }

    [Fact]
    public void TryGetMessageName_NullSensor_ReturnsFalse()
    {
        Assert.False(((Sensor)null!).TryGetMessageName(out var messageName));
        Assert.Equal(string.Empty, messageName);
    }

    [Fact]
    public void TryGetMessageName_NullParameterBag_ReturnsFalse()
    {
        // Arrange
        var sensor = SensorWith(MessageName);
        sensor.Parameters = null!;

        // Act & Assert
        Assert.False(sensor.TryGetMessageName(out _));
    }

    [Fact]
    public void GetMessageName_NonStringParameter_IsCoerced()
    {
        // Arrange — configuration bound from JSON can deliver a boxed value rather than a string.
        var sensor = SensorWith(null);
        sensor.Parameters[CfxSensorParameters.MessageNameKey] = new StringWrapper(MessageName);

        // Act & Assert
        Assert.Equal(MessageName, sensor.GetMessageName());
    }

    private static Sensor SensorWith(string? messageName)
    {
        var parameters = new Dictionary<string, object>();

        if (messageName is not null)
        {
            parameters[CfxSensorParameters.MessageNameKey] = messageName;
        }

        return new Sensor
        {
            ResourceId = "test-resource",
            Name = "test_sensor",
            Dtmi = "dtmi:advantech:EdgeSync:Cfx;1",
            DeviceResourceId = "test-device",
            SensorGroup = SensorGroup.SYS,
            Parameters = parameters,
            Report = new SensorReport { Enabled = true },
        };
    }

    private sealed record StringWrapper(string Value)
    {
        public override string ToString() => Value;
    }
}
