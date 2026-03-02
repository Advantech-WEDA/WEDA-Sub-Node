using Shouldly;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Configuration;
using Xunit;

namespace Weda.SubNode.Core.Tests.Configuration;

/// <summary>
/// Unit tests for SensorNameValidator.
/// Tests IoTDB identifier validation rules for sensor names.
/// </summary>
public class SensorNameValidatorTests
{
    #region IsValidSensorName Tests

    [Theory]
    [InlineData("temperature")]
    [InlineData("Temperature")]
    [InlineData("TEMPERATURE")]
    [InlineData("temp1")]
    [InlineData("temp_sensor")]
    [InlineData("_temp")]
    [InlineData("_123")]
    [InlineData("sensor_1_value")]
    [InlineData("A")]
    [InlineData("_")]
    [InlineData("abc123")]
    [InlineData("ABC_123_def")]
    public void IsValidSensorName_Should_ReturnTrue_ForValidNames(string sensorName)
    {
        // Act
        var result = SensorNameValidator.IsValidSensorName(sensorName);

        // Assert
        result.ShouldBeTrue($"Sensor name '{sensorName}' should be valid");
    }

    [Theory]
    [InlineData("channel.0")]
    [InlineData("sensor-1")]
    [InlineData("sensor value")]
    [InlineData("1sensor")]
    [InlineData("123")]
    [InlineData("sensor@home")]
    [InlineData("sensor#1")]
    [InlineData("sensor$value")]
    [InlineData("sensor%")]
    [InlineData("sensor/path")]
    [InlineData("sensor\\path")]
    [InlineData("sensor:value")]
    [InlineData("sensor[0]")]
    [InlineData("sensor(1)")]
    [InlineData("temp+sensor")]
    [InlineData("temp=sensor")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValidSensorName_Should_ReturnFalse_ForInvalidNames(string? sensorName)
    {
        // Act
        var result = SensorNameValidator.IsValidSensorName(sensorName!);

        // Assert
        result.ShouldBeFalse($"Sensor name '{sensorName}' should be invalid");
    }

    #endregion

    #region TryValidate Tests

    [Theory]
    [InlineData("valid_sensor")]
    [InlineData("_sensor")]
    [InlineData("sensor123")]
    public void TryValidate_Should_ReturnTrue_AndNullErrorMessage_ForValidNames(string sensorName)
    {
        // Act
        var result = SensorNameValidator.TryValidate(sensorName, out var errorMessage);

        // Assert
        result.ShouldBeTrue();
        errorMessage.ShouldBeNull();
    }

    [Fact]
    public void TryValidate_Should_ReturnFalse_AndErrorMessage_ForEmptyName()
    {
        // Act
        var result = SensorNameValidator.TryValidate("", out var errorMessage);

        // Assert
        result.ShouldBeFalse();
        errorMessage.ShouldNotBeNull();
        errorMessage.ShouldContain("cannot be empty");
    }

    [Fact]
    public void TryValidate_Should_ReturnFalse_AndErrorMessage_ForInvalidName()
    {
        // Act
        var result = SensorNameValidator.TryValidate("channel.0", out var errorMessage);

        // Assert
        result.ShouldBeFalse();
        errorMessage.ShouldNotBeNull();
        errorMessage.ShouldContain("invalid for IoTDB");
        errorMessage.ShouldContain("channel.0");
    }

    #endregion

    #region ValidateAll Tests

    [Fact]
    public void ValidateAll_Should_ReturnSuccess_ForValidSensorNames()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "temperature" },
            new() { Name = "humidity" },
            new() { Name = "pressure_sensor" }
        };

        // Act
        var result = SensorNameValidator.ValidateAll(sensors, "TestDevice");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void ValidateAll_Should_ReturnError_ForInvalidSensorName()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "valid_sensor" },
            new() { Name = "channel.0" },  // Invalid
            new() { Name = "another_valid" }
        };

        // Act
        var result = SensorNameValidator.ValidateAll(sensors, "TestDevice");

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].Description.ShouldContain("TestDevice");
        result.Errors[0].Description.ShouldContain("channel.0");
        result.Errors[0].Description.ShouldContain("invalid for IoTDB");
    }

    [Fact]
    public void ValidateAll_Should_ReturnError_ForEmptySensorName()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "" }
        };

        // Act
        var result = SensorNameValidator.ValidateAll(sensors, "TestDevice");

        // Assert
        result.IsError.ShouldBeTrue();
        result.Errors[0].Description.ShouldContain("TestDevice");
        result.Errors[0].Description.ShouldContain("cannot be empty");
    }

    [Fact]
    public void ValidateAll_Should_ReturnSuccess_ForEmptySensorList()
    {
        // Arrange
        var sensors = new List<Sensor>();

        // Act
        var result = SensorNameValidator.ValidateAll(sensors, "TestDevice");

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void ValidateAll_Should_ReturnAllErrors_ForMultipleInvalidSensors()
    {
        // Arrange
        var sensors = new List<Sensor>
        {
            new() { Name = "first.invalid" },
            new() { Name = "second-invalid" }
        };

        // Act
        var result = SensorNameValidator.ValidateAll(sensors, "TestDevice");

        // Assert - Should return all errors, not just the first one
        result.IsError.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        result.Errors[0].Description.ShouldContain("first.invalid");
        result.Errors[1].Description.ShouldContain("second-invalid");
    }

    #endregion
}
