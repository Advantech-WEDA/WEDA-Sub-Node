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
    private readonly IMessageBroker _mockBroker;

    public ISensingProtocolParserTests()
    {
        _mockBroker = Substitute.For<IMessageBroker>();
        _parser = new ISensingProtocolParser(_mockBroker);
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
    public void ParseSensorData_WithValidPayload_ShouldThrowNotImplemented()
    {
        // Arrange
        var payload = "{\"s\":6,\"t\":1617455339,\"q\":192,\"c\":0,\"ai1\":2559.090,\"ai2\":5718.928}";

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _parser.ParseSensorData(payload, _defaultMapping));
    }

    [Fact]
    public void ParseSensorData_ByteArray_ShouldThrowNotImplemented()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("{\"s\":1,\"t\":1617455339,\"q\":192,\"c\":0,\"ai1\":100}");

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _parser.ParseSensorData(payload, _defaultMapping));
    }

    [Fact]
    public void ParseConnectionStatus_WithValidPayload_ShouldThrowNotImplemented()
    {
        // Arrange
        var payload = "{\"status\":\"connect\",\"name\":\"WISE-4012SE\",\"macid\":\"00D0C9FAC80E\",\"ipaddr\":\"192.168.50.239\"}";

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _parser.ParseConnectionStatus(payload));
    }

    [Fact]
    public void IsConnectionMessage_ShouldThrowNotImplemented()
    {
        // Arrange
        var payload = "{\"status\":\"connect\",\"name\":\"WISE-4012SE\",\"macid\":\"00D0C9FAC80E\"}";

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _parser.IsConnectionMessage(payload));
    }

    [Fact]
    public void IsSensorDataMessage_ShouldThrowNotImplemented()
    {
        // Arrange
        var payload = "{\"s\":1,\"t\":1617455339,\"q\":192,\"c\":0,\"ai1\":100}";

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _parser.IsSensorDataMessage(payload));
    }

    #endregion

    #region Encode Tests

    [Fact]
    public void EncodeSensorData_ShouldThrowNotImplemented()
    {
        // Arrange
        var measures = new List<Abstractions.Telemetry.TelemetryMeasure>
        {
            new() { ResourceId = "AnalogInput1", Value = 2559.090, Timestamp = 1617455339000 }
        };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _parser.EncodeSensorData(measures));
    }

    [Fact]
    public void EncodeCommand_ShouldThrowNotImplemented()
    {
        // Arrange
        var command = new DeviceCommand
        {
            DeviceCmd = "SetDigitalOutput",
            Parameters = new Dictionary<string, object> { ["do1"] = true }
        };

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _parser.EncodeCommand(command));
    }

    [Fact]
    public void EncodeDigitalOutputCommand_ShouldThrowNotImplemented()
    {
        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _parser.EncodeDigitalOutputCommand("do1", true));
    }

    [Fact]
    public void EncodeAnalogOutputCommand_ShouldThrowNotImplemented()
    {
        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _parser.EncodeAnalogOutputCommand("ao1", 5.5));
    }

    [Fact]
    public void EncodeConfigurationRequest_ShouldThrowNotImplemented()
    {
        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _parser.EncodeConfigurationRequest("GET", "ai1"));
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
