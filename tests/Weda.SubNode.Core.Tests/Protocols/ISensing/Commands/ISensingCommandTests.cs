using System.Text.Json;
using Weda.SubNode.Core.Protocols.ISensing.Commands;
using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.ISensing.Commands;

public class ISensingCommandTests
{
    [Fact]
    public void DigitalOutputCommand_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var command = new DigitalOutputCommand
        {
            OutputName = "do0",
            State = true
        };

        // Assert
        Assert.Equal("SetDO", command.Command);
        Assert.Equal("do0", command.OutputName);
        Assert.True(command.State);
    }

    [Fact]
    public void DigitalOutputCommand_ShouldSerializeToJson()
    {
        // Arrange
        var command = new DigitalOutputCommand
        {
            OutputName = "do1",
            State = false
        };

        // Act
        var json = JsonSerializer.Serialize(command);

        // Assert
        Assert.Contains("\"cmd\":\"SetDO\"", json);
        Assert.Contains("\"do\":\"do1\"", json);
        Assert.Contains("\"state\":false", json);
    }

    [Fact]
    public void AnalogOutputCommand_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var command = new AnalogOutputCommand
        {
            OutputName = "ao0",
            Value = 4.5
        };

        // Assert
        Assert.Equal("SetAO", command.Command);
        Assert.Equal("ao0", command.OutputName);
        Assert.Equal(4.5, command.Value);
    }

    [Fact]
    public void AnalogOutputCommand_ShouldSerializeToJson()
    {
        // Arrange
        var command = new AnalogOutputCommand
        {
            OutputName = "ao1",
            Value = 10.25
        };

        // Act
        var json = JsonSerializer.Serialize(command);

        // Assert
        Assert.Contains("\"cmd\":\"SetAO\"", json);
        Assert.Contains("\"ao\":\"ao1\"", json);
        Assert.Contains("\"value\":10.25", json);
    }

    [Fact]
    public void ConfigurationRequestCommand_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var command = new ConfigurationRequestCommand
        {
            Index = 1
        };

        // Assert
        Assert.Equal("GetConfig", command.Command);
        Assert.Equal((ushort)1, command.Index);
    }

    [Fact]
    public void ConfigurationRequestCommand_DefaultIndex_ShouldBeZero()
    {
        // Arrange & Act
        var command = new ConfigurationRequestCommand();

        // Assert
        Assert.Equal((ushort)0, command.Index);
    }

    [Fact]
    public void ConfigurationUpdateCommand_ShouldHaveCorrectProperties()
    {
        // Arrange
        var configData = new Dictionary<string, object>
        {
            ["interval"] = 5000,
            ["enabled"] = true
        };

        // Act
        var command = new ConfigurationUpdateCommand
        {
            Index = 2,
            ConfigData = configData
        };

        // Assert
        Assert.Equal("SetConfig", command.Command);
        Assert.Equal((ushort)2, command.Index);
        Assert.Equal(configData, command.ConfigData);
    }

    [Fact]
    public void SensorEnableCommand_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var command = new SensorEnableCommand
        {
            SensorName = "ai0",
            Enabled = true
        };

        // Assert
        Assert.Equal("SetSensorEnable", command.Command);
        Assert.Equal("ai0", command.SensorName);
        Assert.True(command.Enabled);
    }

    [Fact]
    public void SensorEnableCommand_ShouldSerializeToJson()
    {
        // Arrange
        var command = new SensorEnableCommand
        {
            SensorName = "di1",
            Enabled = false
        };

        // Act
        var json = JsonSerializer.Serialize(command);

        // Assert
        Assert.Contains("\"cmd\":\"SetSensorEnable\"", json);
        Assert.Contains("\"sensor\":\"di1\"", json);
        Assert.Contains("\"enabled\":false", json);
    }

    [Fact]
    public void ISensingCommand_SupportsAdditionalData()
    {
        // Arrange
        var command = new DigitalOutputCommand
        {
            OutputName = "do0",
            State = true,
            AdditionalData = new Dictionary<string, object>
            {
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["priority"] = "high"
            }
        };

        // Act & Assert
        Assert.NotNull(command.AdditionalData);
        Assert.Equal(2, command.AdditionalData.Count);
    }
}
