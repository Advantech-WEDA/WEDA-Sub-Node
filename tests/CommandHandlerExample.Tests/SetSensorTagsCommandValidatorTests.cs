using CommandHandlerExample.Commands.SetSensorTags;
using CommandHandlerExample.Commands.SetSensorTags.Models;

using Shouldly;

using Xunit;

namespace CommandHandlerExample.Tests;

/// <summary>
/// Unit tests for SetSensorTagsCommandValidator.
/// </summary>
public class SetSensorTagsCommandValidatorTests
{
    private readonly SetSensorTagsCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidParameters_ShouldSucceed()
    {
        var command = CreateCommand("temperature_sensor",
            new Dictionary<string, string> { ["location"] = "line-3" });

        var result = _validator.Validate(command);

        result.IsError.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptySensorName_ShouldFail(string sensorName)
    {
        var command = CreateCommand(sensorName,
            new Dictionary<string, string> { ["a"] = "b" });

        var result = _validator.Validate(command);

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe("Command.ValidationFailed");
    }

    [Fact]
    public void Validate_WithEmptyTags_ShouldFail()
    {
        var command = CreateCommand("temperature_sensor", []);

        var result = _validator.Validate(command);

        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("At least one tag");
    }

    [Fact]
    public void Validate_WithWhitespaceTagKey_ShouldFail()
    {
        var command = CreateCommand("temperature_sensor",
            new Dictionary<string, string> { ["  "] = "value" });

        var result = _validator.Validate(command);

        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("Tag keys");
    }

    [Fact]
    public void Validate_WithNullTagValue_ShouldFail()
    {
        var command = CreateCommand("temperature_sensor",
            new Dictionary<string, string> { ["location"] = null! });

        var result = _validator.Validate(command);

        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("location");
    }

    [Fact]
    public void Validate_WithNullParameters_ShouldFail()
    {
        var command = new SetSensorTagsCommand { DeviceCmd = "tag.set", Parameters = null! };

        var result = _validator.Validate(command);

        result.IsError.ShouldBeTrue();
    }

    private static SetSensorTagsCommand CreateCommand(
        string sensorName,
        Dictionary<string, string> tags) => new()
        {
            DeviceCmd = "tag.set",
            Timeout = 10,
            Parameters = new SetSensorTagsParameters
            {
                SensorName = sensorName,
                Tags = tags
            }
        };
}
