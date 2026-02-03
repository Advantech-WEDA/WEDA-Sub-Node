using NSubstitute;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Core.Protocols.Image;
using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Image;

public class ImageProtocolParserTests
{
    private readonly ImageProtocolParser _parser;
    private readonly ICommunication _mockCommunication;

    public ImageProtocolParserTests()
    {
        _mockCommunication = Substitute.For<ICommunication>();
        _parser = new ImageProtocolParser(_mockCommunication);
    }

    #region ParseSensorData(byte[]) Tests

    [Fact]
    public void ParseSensorData_WithPngBytes_ShouldReturnBase64WithPngContentType()
    {
        // Arrange - PNG magic bytes + some data
        byte[] pngData = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

        // Act
        var result = _parser.ParseSensorData(pngData);

        // Assert
        Assert.Single(result);
        var measure = result[0];
        Assert.Equal("image", measure.ResourceId);
        Assert.Equal(Convert.ToBase64String(pngData), measure.Value);
        Assert.NotNull(measure.Metadata);
        Assert.Equal(pngData.Length, measure.Metadata["size"]);
        Assert.Equal("image/png", measure.Metadata["contentType"]);
    }

    [Fact]
    public void ParseSensorData_WithJpegBytes_ShouldReturnBase64WithJpegContentType()
    {
        // Arrange - JPEG magic bytes + some data
        byte[] jpegData = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

        // Act
        var result = _parser.ParseSensorData(jpegData);

        // Assert
        Assert.Single(result);
        var measure = result[0];
        Assert.Equal("image", measure.ResourceId);
        Assert.Equal(Convert.ToBase64String(jpegData), measure.Value);
        Assert.NotNull(measure.Metadata);
        Assert.Equal(jpegData.Length, measure.Metadata["size"]);
        Assert.Equal("image/jpeg", measure.Metadata["contentType"]);
    }

    [Fact]
    public void ParseSensorData_WithUnknownBytes_ShouldReturnOctetStream()
    {
        // Arrange - arbitrary bytes (not PNG or JPEG)
        byte[] unknownData = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05];

        // Act
        var result = _parser.ParseSensorData(unknownData);

        // Assert
        Assert.Single(result);
        var measure = result[0];
        Assert.Equal("application/octet-stream", measure.Metadata!["contentType"]);
    }

    [Fact]
    public void ParseSensorData_WithSensorMapping_ShouldUseCustomResourceId()
    {
        // Arrange
        byte[] data = [0x89, 0x50, 0x4E, 0x47, 0x00, 0x01, 0x02, 0x03];
        var mapping = new SensorMapping
        {
            FieldToResourceId = new Dictionary<string, string>
            {
                ["image"] = "camera-front-001"
            }
        };

        // Act
        var result = _parser.ParseSensorData(data, mapping);

        // Assert
        Assert.Single(result);
        Assert.Equal("camera-front-001", result[0].ResourceId);
    }

    [Fact]
    public void ParseSensorData_WithoutSensorMapping_ShouldDefaultToImage()
    {
        // Arrange
        byte[] data = [0xFF, 0xD8, 0xFF, 0xE1, 0x00, 0x00];

        // Act
        var result = _parser.ParseSensorData(data, sensorMapping: null);

        // Assert
        Assert.Single(result);
        Assert.Equal("image", result[0].ResourceId);
    }

    #endregion

    #region ParseSensorData(string) Tests

    [Fact]
    public void ParseSensorData_WithValidBase64String_ShouldPassThrough()
    {
        // Arrange - valid Base64 of PNG magic bytes
        byte[] pngData = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var base64 = Convert.ToBase64String(pngData);

        // Act
        var result = _parser.ParseSensorData(base64);

        // Assert
        Assert.Single(result);
        var measure = result[0];
        Assert.Equal("image", measure.ResourceId);
        Assert.Equal(base64, measure.Value); // pass-through, not re-encoded
        Assert.NotNull(measure.Metadata);
        Assert.Equal(pngData.Length, measure.Metadata["size"]);
        Assert.Equal("image/png", measure.Metadata["contentType"]);
    }

    [Fact]
    public void ParseSensorData_WithInvalidBase64String_ShouldThrowImageProtocolException()
    {
        // Arrange
        var invalidBase64 = "not-valid-base64!!!";

        // Act & Assert
        var ex = Assert.Throws<ImageProtocolException>(() => _parser.ParseSensorData(invalidBase64));
        Assert.Contains("Invalid Base64 string", ex.Message);
    }

    [Fact]
    public void ParseSensorData_StringWithSensorMapping_ShouldUseCustomResourceId()
    {
        // Arrange
        byte[] data = [0xFF, 0xD8, 0xFF, 0xE0];
        var base64 = Convert.ToBase64String(data);
        var mapping = new SensorMapping
        {
            FieldToResourceId = new Dictionary<string, string>
            {
                ["image"] = "thermal-camera-01"
            }
        };

        // Act
        var result = _parser.ParseSensorData(base64, mapping);

        // Assert
        Assert.Single(result);
        Assert.Equal("thermal-camera-01", result[0].ResourceId);
    }

    #endregion

    #region Low-Level Parse/Encode Tests

    [Fact]
    public void Parse_ShouldConvertBytesToBase64()
    {
        // Arrange
        byte[] data = [0x01, 0x02, 0x03];

        // Act
        var result = _parser.Parse(data);

        // Assert
        Assert.IsType<string>(result);
        Assert.Equal(Convert.ToBase64String(data), result);
    }

    [Fact]
    public void Encode_WithBase64String_ShouldConvertToBytes()
    {
        // Arrange
        byte[] original = [0x01, 0x02, 0x03];
        var base64 = Convert.ToBase64String(original);

        // Act
        var result = _parser.Encode(base64);

        // Assert
        Assert.Equal(original, result);
    }

    [Fact]
    public void Encode_WithNonStringValue_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _parser.Encode(12345));
    }

    #endregion

    #region EncodeSensorData / EncodeCommand Tests

    [Fact]
    public void EncodeSensorData_WithBase64Measure_ShouldDecodeToBytes()
    {
        // Arrange
        byte[] original = [0x89, 0x50, 0x4E, 0x47];
        var base64 = Convert.ToBase64String(original);
        var measures = new List<Abstractions.Telemetry.TelemetryMeasure>
        {
            new() { ResourceId = "image", Value = base64 }
        };

        // Act
        var result = _parser.EncodeSensorData(measures);

        // Assert
        Assert.Equal(original, result);
    }

    [Fact]
    public void EncodeSensorData_WithEmptyMeasures_ShouldThrowImageProtocolException()
    {
        // Arrange
        var measures = new List<Abstractions.Telemetry.TelemetryMeasure>();

        // Act & Assert
        Assert.Throws<ImageProtocolException>(() => _parser.EncodeSensorData(measures));
    }

    [Fact]
    public void EncodeCommand_ShouldThrowNotSupportedException()
    {
        // Arrange
        var command = new Abstractions.Commands.Contracts.DeviceCommand
        {
            DeviceCmd = "SomeCommand",
            Parameters = new Dictionary<string, object>()
        };

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => _parser.EncodeCommand(command));
    }

    #endregion

    #region DetectContentType Edge Cases (tested via ParseSensorData)

    [Fact]
    public void ParseSensorData_WithLessThan4Bytes_ShouldReturnOctetStream()
    {
        // Arrange - only 2 bytes, too short for magic byte detection
        byte[] smallData = [0x89, 0x50];

        // Act
        var result = _parser.ParseSensorData(smallData);

        // Assert
        Assert.Single(result);
        Assert.Equal("application/octet-stream", result[0].Metadata!["contentType"]);
    }

    [Fact]
    public void ParseSensorData_WithEmptyArray_ShouldReturnOctetStream()
    {
        // Arrange
        byte[] emptyData = [];

        // Act
        var result = _parser.ParseSensorData(emptyData);

        // Assert
        Assert.Single(result);
        Assert.Equal("application/octet-stream", result[0].Metadata!["contentType"]);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullCommunication_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ImageProtocolParser(null!));
    }

    [Fact]
    public void Communication_ShouldReturnInjectedInstance()
    {
        // Assert
        Assert.Same(_mockCommunication, _parser.Communication);
    }

    #endregion

    #region Exception Tests

    [Fact]
    public void ImageProtocolException_ShouldStoreMessage()
    {
        // Act
        var ex = new ImageProtocolException("test error");

        // Assert
        Assert.Equal("test error", ex.Message);
    }

    [Fact]
    public void ImageProtocolException_WithInnerException_ShouldStoreInnerException()
    {
        // Arrange
        var inner = new FormatException("bad format");

        // Act
        var ex = new ImageProtocolException("test error", inner);

        // Assert
        Assert.Equal("test error", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    #endregion
}
