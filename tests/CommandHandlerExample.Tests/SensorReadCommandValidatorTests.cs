using CommandHandlerExample.Commands.SensorRead;
using CommandHandlerExample.Commands.SensorRead.Models;

using Shouldly;

using Xunit;

namespace CommandHandlerExample.Tests;

/// <summary>
/// Unit tests for SensorReadCommandValidator.
/// </summary>
public class SensorReadCommandValidatorTests
{
    private readonly SensorReadCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidSensorName_ShouldSucceed()
    {
        var command = CreateCommand("temperature_sensor");

        var result = _validator.Validate(command);

        result.IsError.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyOrWhitespaceSensorName_ShouldFail(string sensorName)
    {
        var command = CreateCommand(sensorName);

        var result = _validator.Validate(command);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Command.ValidationFailed");
    }

    [Fact]
    public void Validate_WithNullParameters_ShouldFail()
    {
        var command = new SensorReadCommand { DeviceCmd = "sensor.read", Parameters = null! };

        var result = _validator.Validate(command);

        result.IsError.ShouldBeTrue();
    }

    private static SensorReadCommand CreateCommand(string sensorName) => new()
    {
        DeviceCmd = "sensor.read",
        Timeout = 10,
        Parameters = new SensorReadParameters { SensorName = sensorName }
    };
}
