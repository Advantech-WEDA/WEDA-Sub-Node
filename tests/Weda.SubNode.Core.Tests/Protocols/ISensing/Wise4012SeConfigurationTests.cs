using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.ISensing;
using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.ISensing;

public class Wise4012SeConfigurationTests
{
    private readonly DeviceConfiguration _configuration;

    public Wise4012SeConfigurationTests()
    {
        _configuration = new DeviceConfiguration
        {
            DeviceName = "WISE-4012SE Test",
            DeviceType = DeviceType.CustomDevice,
            DeviceCapabilities = new DeviceCapabilities
            {
                Manufacturer = "Advantech",
                Model = "WISE-4012SE",
                SubNodeSwVersion = "1.0",
                DeviceInfo = new Dictionary<string, object>()
            },
            Communication = new Dictionary<string, object>
            {
                ["BrokerUrl"] = "mqtt://localhost:1883",
                ["MacAddress"] = "00D0C9FAC80E",
                ["Manufacturer"] = "Advantech"
            },
            Sensors = new List<Sensor>
            {
                new()
                {
                    ResourceId = "ai0-resource",
                    Name = "AI0",
                    Dtmi = "dtmi:advantech:EdgeSync:AI;1",
                    DeviceResourceId = "test-device",
                    SensorGroup = SensorGroup.AI,
                    Parameters = new Dictionary<string, object>
                    {
                        ["FieldName"] = "ai0"
                    },
                    Config = new SensorConfig
                    {
                        Enabled = true,
                        Unit = "mA"
                    }
                }
            }
        };
    }

    [Fact]
    public void GetMacAddress_ShouldReturnMacAddress()
    {
        // Act
        var macAddress = _configuration.GetMacAddress();

        // Assert
        Assert.Equal("00D0C9FAC80E", macAddress);
    }

    [Fact]
    public void GetMacAddress_WithoutMacAddress_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var config = new DeviceConfiguration
        {
            DeviceName = "Test",
            DeviceType = DeviceType.CustomDevice,
            DeviceCapabilities = new DeviceCapabilities
            {
                Manufacturer = "Advantech",
                Model = "Test",
                SubNodeSwVersion = "1.0",
                DeviceInfo = new Dictionary<string, object>()
            },
            Communication = new Dictionary<string, object>()
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.GetMacAddress());
    }

    [Fact]
    public void GetManufacturer_ShouldReturnManufacturer()
    {
        // Act
        var manufacturer = _configuration.GetManufacturer();

        // Assert
        Assert.Equal("Advantech", manufacturer);
    }

    [Fact]
    public void GetManufacturer_WithoutManufacturer_ShouldReturnDefaultValue()
    {
        // Arrange
        var config = new DeviceConfiguration
        {
            DeviceName = "Test",
            DeviceType = DeviceType.CustomDevice,
            DeviceCapabilities = new DeviceCapabilities
            {
                Manufacturer = "Advantech",
                Model = "Test",
                SubNodeSwVersion = "1.0",
                DeviceInfo = new Dictionary<string, object>()
            },
            Communication = new Dictionary<string, object>()
        };

        // Act
        var manufacturer = config.GetManufacturer();

        // Assert
        Assert.Equal("Advantech", manufacturer);
    }

    [Fact]
    public void ToWise4012SeSensor_ShouldMapSensorCorrectly()
    {
        // Arrange
        var sensor = _configuration.Sensors[0];

        // Act
        var wise4012SeSensor = sensor.ToWise4012SeSensor();

        // Assert
        Assert.NotNull(wise4012SeSensor);
        Assert.Equal("ai0-resource", wise4012SeSensor.ResourceId);
        Assert.Equal("AI0", wise4012SeSensor.Name);
        Assert.Equal("dtmi:advantech:EdgeSync:AI;1", wise4012SeSensor.Dtmi);
        Assert.Equal(SensorGroup.AI, wise4012SeSensor.SensorGroup);
        Assert.Equal("ai0", wise4012SeSensor.FieldName);
        Assert.Equal(Abstractions.Protocols.SensorType.Analog, wise4012SeSensor.SensorType);
        Assert.Equal("mA", wise4012SeSensor.Unit);
    }

    [Fact]
    public void ToWise4012SeSensor_WithoutFieldName_ShouldUseNameAsFieldName()
    {
        // Arrange
        var sensor = new Sensor
        {
            ResourceId = "di1-resource",
            Name = "DI1",
            Dtmi = "dtmi:advantech:EdgeSync:DI;1",
            DeviceResourceId = "test-device",
            SensorGroup = SensorGroup.DI,
            Config = new SensorConfig { Enabled = true }
        };

        // Act
        var wise4012SeSensor = sensor.ToWise4012SeSensor();

        // Assert
        Assert.Equal("DI1", wise4012SeSensor.FieldName);
    }

    [Theory]
    [InlineData(SensorGroup.AI, Abstractions.Protocols.SensorType.Analog)]
    [InlineData(SensorGroup.AO, Abstractions.Protocols.SensorType.Analog)]
    [InlineData(SensorGroup.DI, Abstractions.Protocols.SensorType.Digital)]
    [InlineData(SensorGroup.DO, Abstractions.Protocols.SensorType.Digital)]
    [InlineData(SensorGroup.TEMP, Abstractions.Protocols.SensorType.Temperature)]
    [InlineData(SensorGroup.PWR, Abstractions.Protocols.SensorType.Other)]
    [InlineData(SensorGroup.SYS, Abstractions.Protocols.SensorType.Other)]
    public void ToWise4012SeSensor_ShouldMapSensorGroupToSensorType(
        SensorGroup sensorGroup,
        Abstractions.Protocols.SensorType expectedType)
    {
        // Arrange
        var sensor = new Sensor
        {
            ResourceId = "test-resource",
            Name = "Test",
            Dtmi = "dtmi:test;1",
            DeviceResourceId = "test-device",
            SensorGroup = sensorGroup,
            Config = new SensorConfig { Enabled = true }
        };

        // Act
        var wise4012SeSensor = sensor.ToWise4012SeSensor();

        // Assert
        Assert.Equal(expectedType, wise4012SeSensor.SensorType);
    }

    [Fact]
    public void Wise4012SeSensor_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var sensor = new Wise4012SeSensor
        {
            ResourceId = "test-resource",
            Name = "Test",
            Dtmi = "dtmi:test;1",
            FieldName = "test_field"
        };

        // Assert
        Assert.Equal("test-resource", sensor.ResourceId);
        Assert.Equal("Test", sensor.Name);
        Assert.Equal("dtmi:test;1", sensor.Dtmi);
        Assert.Equal("test_field", sensor.FieldName);
    }
}
