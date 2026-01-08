using System.Text;
using NSubstitute;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.ISensing;
using Weda.SubNode.Core.Protocols.ISensing.Models;
using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.ISensing;

public class ISensingProtocolParserTests
{
    private readonly ISensingProtocolParser _parser;
    private readonly SensorMapping _defaultMapping;
    private readonly IPubSub _mockPubSub;

    public ISensingProtocolParserTests()
    {
        _mockPubSub = Substitute.For<IPubSub>();
        _parser = new ISensingProtocolParser(_mockPubSub);
        _defaultMapping = new SensorMapping
        {
            FieldToResourceId = new Dictionary<string, string>
            {
                ["ai1"] = "AnalogInput1",
                ["ai2"] = "AnalogInput2",
                ["ai3"] = "AnalogInput3",
                ["ai4"] = "AnalogInput4",
                ["do1"] = "DigitalOutput1",
                ["do2"] = "DigitalOutput2"
            },
            FieldToSensorType = new Dictionary<string, SensorType>
            {
                ["ai1"] = SensorType.Analog,
                ["ai2"] = SensorType.Analog,
                ["ai3"] = SensorType.Analog,
                ["ai4"] = SensorType.Analog,
                ["do1"] = SensorType.Digital,
                ["do2"] = SensorType.Digital
            }
        };
    }

    #region Parse Tests

    [Fact]
    public void ParseSensorData_WithValidPayload_ShouldParseMeasures()
    {
        // Arrange
        var payload = "{\"s\":6,\"t\":1617455339,\"q\":192,\"c\":0,\"ai1\":2559.090,\"ai2\":5718.928}";

        // Act
        var result = _parser.ParseSensorData(payload, _defaultMapping);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, m => m.ResourceId == "AnalogInput1" && Math.Abs((double)m.Value - 2559.090) < 0.001);
        Assert.Contains(result, m => m.ResourceId == "AnalogInput2" && Math.Abs((double)m.Value - 5718.928) < 0.001);
    }

    [Fact]
    public void ParseSensorData_ByteArray_ShouldParseMeasures()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("{\"s\":1,\"t\":1617455339,\"q\":192,\"c\":0,\"ai1\":100}");

        // Act
        var result = _parser.ParseSensorData(payload, _defaultMapping);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("AnalogInput1", result[0].ResourceId);
        Assert.Equal(100.0, result[0].Value);
    }

    [Fact]
    public void ParseConnectionStatus_WithValidPayload_ShouldParseStatus()
    {
        // Arrange
        var payload = "{\"status\":\"connect\",\"name\":\"WISE-4012SE\",\"macid\":\"00D0C9FAC80E\",\"ipaddr\":\"192.168.50.239\"}";

        // Act
        var result = _parser.ParseConnectionStatus(payload);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("connect", result.Status);
        Assert.Equal("WISE-4012SE", result.DeviceName);
        Assert.Equal("00D0C9FAC80E", result.MacAddress);
        Assert.Equal("192.168.50.239", result.IpAddress);
    }

    [Fact]
    public void IsConnectionMessage_ShouldReturnTrue()
    {
        // Arrange
        var payload = "{\"status\":\"connect\",\"name\":\"WISE-4012SE\",\"macid\":\"00D0C9FAC80E\"}";

        // Act
        var result = _parser.IsConnectionMessage(payload);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsConnectionMessage_WithSensorData_ShouldReturnFalse()
    {
        // Arrange
        var payload = "{\"s\":1,\"t\":1617455339,\"q\":192,\"c\":0,\"ai1\":100}";

        // Act
        var result = _parser.IsConnectionMessage(payload);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSensorDataMessage_ShouldReturnTrue()
    {
        // Arrange
        var payload = "{\"s\":1,\"t\":1617455339,\"q\":192,\"c\":0,\"ai1\":100}";

        // Act
        var result = _parser.IsSensorDataMessage(payload);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSensorDataMessage_WithConnectionMessage_ShouldReturnFalse()
    {
        // Arrange
        var payload = "{\"status\":\"connect\",\"name\":\"WISE-4012SE\",\"macid\":\"00D0C9FAC80E\"}";

        // Act
        var result = _parser.IsSensorDataMessage(payload);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Encode Tests

    [Fact]
    public void EncodeSensorData_ShouldEncodeToJson()
    {
        // Arrange
        var measures = new List<Abstractions.Telemetry.TelemetryMeasure>
        {
            new() { ResourceId = "AnalogInput1", Value = 2559.090, Timestamp = 1617455339000 }
        };

        // Act
        var result = _parser.EncodeSensorData(measures);

        // Assert
        Assert.NotNull(result);
        var json = Encoding.UTF8.GetString(result);
        Assert.Contains("AnalogInput1", json);
        Assert.Contains("2559.09", json);
    }

    [Fact]
    public void EncodeCommand_SetDigitalOutput_ShouldEncodeCorrectly()
    {
        // Arrange
        var command = new DeviceCommand
        {
            DeviceCmd = "SetDigitalOutput",
            Parameters = new Dictionary<string, object> { ["outputName"] = "do1", ["state"] = true }
        };

        // Act
        var result = _parser.EncodeCommand(command);

        // Assert
        Assert.NotNull(result);
        var json = Encoding.UTF8.GetString(result);
        Assert.Contains("\"cmd\"", json);
        Assert.Contains("\"do\"", json); // "do" is the JSON property name
        Assert.Contains("do1", json);    // "do1" is the value
    }

    [Fact]
    public void EncodeDigitalOutputCommand_ShouldEncodeCorrectly()
    {
        // Act
        var result = _parser.EncodeDigitalOutputCommand("do1", true);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("SetDO", result);
        Assert.Contains("do1", result);
        Assert.Contains("true", result);
    }

    [Fact]
    public void EncodeAnalogOutputCommand_ShouldEncodeCorrectly()
    {
        // Act
        var result = _parser.EncodeAnalogOutputCommand("ao1", 5.5);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("SetAO", result);
        Assert.Contains("ao1", result);
        Assert.Contains("5.5", result);
    }

    [Fact]
    public void EncodeConfigurationRequest_GET_ShouldEncodeCorrectly()
    {
        // Act
        var result = _parser.EncodeConfigurationRequest("GET", "ai1");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("GetConfig", result);
    }

    [Fact]
    public void EncodeConfigurationRequest_SET_ShouldEncodeCorrectly()
    {
        // Arrange
        var config = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var result = _parser.EncodeConfigurationRequest("SET", "ai1", config);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("SetConfig", result);
    }

    #endregion

    #region Quality Code Tests

    [Theory]
    [InlineData(192, "Good")]
    [InlineData(0, "Bad")]
    [InlineData(64, "Uncertain")]
    [InlineData(255, "NoQuality")]
    [InlineData(200, "Good")]
    [InlineData(10, "Bad")]
    [InlineData(80, "Uncertain")]
    public void ISensingQualityCode_MapToQuality_ShouldReturnCorrectString(byte code, string expected)
    {
        // Act
        var result = ISensingQualityCode.MapToQuality(code);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(192, true)]
    [InlineData(200, true)]
    [InlineData(255, true)]
    [InlineData(191, false)]
    [InlineData(0, false)]
    [InlineData(64, false)]
    public void ISensingQualityCode_IsGoodQuality_ShouldReturnCorrectResult(byte code, bool expected)
    {
        // Act
        var result = ISensingQualityCode.IsGoodQuality(code);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Model Tests

    [Fact]
    public void ISensingSensorData_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var data = new ISensingSensorData
        {
            SequenceNumber = 1,
            QualityCode = 192,
            ConfigurationIndex = 0
        };

        // Assert
        Assert.Equal(1u, data.SequenceNumber);
        Assert.Equal(192, data.QualityCode);
        Assert.Equal(0, data.ConfigurationIndex);
    }

    [Fact]
    public void ConnectionStatusMessage_ShouldHaveRequiredProperties()
    {
        // Arrange & Act
        var message = new ConnectionStatusMessage
        {
            Status = "connect",
            DeviceName = "WISE-4012SE",
            MacAddress = "00D0C9FAC80E",
            IpAddress = "192.168.50.239"
        };

        // Assert
        Assert.Equal("connect", message.Status);
        Assert.Equal("WISE-4012SE", message.DeviceName);
        Assert.Equal("00D0C9FAC80E", message.MacAddress);
        Assert.Equal("192.168.50.239", message.IpAddress);
    }

    #endregion

    #region Exception Tests

    [Fact]
    public void ISensingProtocolException_ShouldStorePayload()
    {
        // Arrange
        var payload = "{\"invalid\":\"json\"}";

        // Act
        var exception = new ISensingProtocolException("Parse failed", payload);

        // Assert
        Assert.Equal("Parse failed", exception.Message);
        Assert.Equal(payload, exception.Payload);
    }

    #endregion
}
