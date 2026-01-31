using Shouldly;
using Weda.SubNode.Core.Telemetry.Validation;
using Xunit;

namespace Weda.SubNode.Core.Tests.Telemetry.Validation;

/// <summary>
/// Unit tests for SchemaBasedValidator.
/// </summary>
public class SchemaBasedValidatorTests
{
    private readonly SchemaBasedValidator _validator = new();

    #region Null Value Tests

    [Fact]
    public void Validate_NullValue_ReturnsError()
    {
        // Act
        var result = _validator.Validate(null, "double");

        // Assert
        result.IsError.ShouldBeTrue();
    }

    #endregion

    #region Numeric Schema Tests

    [Theory]
    [InlineData("double")]
    [InlineData("float")]
    [InlineData("integer")]
    [InlineData("long")]
    [InlineData("DOUBLE")] // Case insensitive
    [InlineData("Float")]
    public void Validate_NumericSchema_WithDoubleValue_ReturnsSuccess(string schema)
    {
        // Act
        var result = _validator.Validate(25.5, schema);

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_NumericSchema_WithIntValue_ReturnsSuccess()
    {
        // Act
        var result = _validator.Validate(42, "integer");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_NumericSchema_WithLongValue_ReturnsSuccess()
    {
        // Act
        var result = _validator.Validate(9223372036854775807L, "long");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_NumericSchema_WithDecimalValue_ReturnsSuccess()
    {
        // Act
        var result = _validator.Validate(123.456m, "double");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_NumericSchema_WithNumericString_ReturnsSuccess()
    {
        // Act
        var result = _validator.Validate("123.45", "double");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_NumericSchema_WithNonNumericString_ReturnsError()
    {
        // Act
        var result = _validator.Validate("not a number", "double");

        // Assert
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NumericSchema_WithBoolean_ReturnsError()
    {
        // Act
        var result = _validator.Validate(true, "double");

        // Assert
        result.IsError.ShouldBeTrue();
    }

    #endregion

    #region Boolean Schema Tests

    [Fact]
    public void Validate_BooleanSchema_WithTrueValue_ReturnsSuccess()
    {
        // Act
        var result = _validator.Validate(true, "boolean");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_BooleanSchema_WithFalseValue_ReturnsSuccess()
    {
        // Act
        var result = _validator.Validate(false, "boolean");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("True")]
    [InlineData("False")]
    public void Validate_BooleanSchema_WithBooleanString_ReturnsSuccess(string value)
    {
        // Act
        var result = _validator.Validate(value, "boolean");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_BooleanSchema_WithNumericValue_ReturnsError()
    {
        // Act
        var result = _validator.Validate(1, "boolean");

        // Assert
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Validate_BooleanSchema_WithInvalidString_ReturnsError()
    {
        // Act
        var result = _validator.Validate("yes", "boolean");

        // Assert
        result.IsError.ShouldBeTrue();
    }

    #endregion

    #region String Schema Tests

    [Fact]
    public void Validate_StringSchema_WithStringValue_ReturnsSuccess()
    {
        // Act
        var result = _validator.Validate("hello world", "string");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_StringSchema_WithEmptyString_ReturnsSuccess()
    {
        // Act
        var result = _validator.Validate("", "string");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_StringSchema_WithNumericValue_ReturnsError()
    {
        // Act
        var result = _validator.Validate(123, "string");

        // Assert
        result.IsError.ShouldBeTrue();
    }

    #endregion

    #region JSON Schema Tests

    [Fact]
    public void Validate_JsonSchema_WithValidJson_ReturnsSuccess()
    {
        // Arrange
        var json = """{"name":"test","value":123}""";

        // Act
        var result = _validator.Validate(json, "application/json");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_JsonSchema_WithValidJsonArray_ReturnsSuccess()
    {
        // Arrange
        var json = """[1,2,3]""";

        // Act
        var result = _validator.Validate(json, "application/json");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_JsonSchema_WithInvalidJson_ReturnsError()
    {
        // Arrange
        var invalidJson = "{invalid json}";

        // Act
        var result = _validator.Validate(invalidJson, "application/json");

        // Assert
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Validate_JsonSchema_WithNonStringValue_ReturnsError()
    {
        // Act
        var result = _validator.Validate(123, "application/json");

        // Assert
        result.IsError.ShouldBeTrue();
    }

    #endregion

    #region MIME Type / Base64 Schema Tests

    [Fact]
    public void Validate_ImageJpegSchema_WithValidBase64_ReturnsSuccess()
    {
        // Arrange - Valid base64 string
        var base64 = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

        // Act
        var result = _validator.Validate(base64, "image/jpeg");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ImagePngSchema_WithValidBase64_ReturnsSuccess()
    {
        // Arrange
        var base64 = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        // Act
        var result = _validator.Validate(base64, "image/png");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_MimeTypeSchema_WithInvalidBase64_ReturnsError()
    {
        // Arrange - Invalid base64 string
        var invalidBase64 = "not-valid-base64!!!";

        // Act
        var result = _validator.Validate(invalidBase64, "image/jpeg");

        // Assert
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MimeTypeSchema_WithEmptyString_ReturnsError()
    {
        // Act
        var result = _validator.Validate("", "image/jpeg");

        // Assert
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MimeTypeSchema_WithNonStringValue_ReturnsError()
    {
        // Act
        var result = _validator.Validate(new byte[] { 1, 2, 3 }, "image/jpeg");

        // Assert
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ApplicationOctetStreamSchema_WithValidBase64_ReturnsSuccess()
    {
        // Arrange
        var base64 = Convert.ToBase64String(new byte[] { 0x00, 0x01, 0x02, 0x03 });

        // Act
        var result = _validator.Validate(base64, "application/octet-stream");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    #endregion

    #region Unknown Schema Tests

    [Fact]
    public void Validate_UnknownSchema_ReturnsSuccess()
    {
        // Unknown schemas pass through (fail open for extensibility)
        var result = _validator.Validate("any value", "unknown-schema");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_DateSchema_ReturnsSuccess()
    {
        // DTDL primitive "date" is not explicitly handled, should pass through
        var result = _validator.Validate("2024-01-15", "date");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    #endregion
}
