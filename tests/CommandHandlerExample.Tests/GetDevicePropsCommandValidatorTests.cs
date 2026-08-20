using CommandHandlerExample.Commands.GetDeviceProps;
using CommandHandlerExample.Commands.GetDeviceProps.Models;

using Shouldly;

using Xunit;

namespace CommandHandlerExample.Tests;

/// <summary>
/// Unit tests for GetDevicePropsCommandValidator.
/// </summary>
public class GetDevicePropsCommandValidatorTests
{
    private readonly GetDevicePropsCommandValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyOptions_ShouldSucceed()
    {
        var command = CreateCommand([]);

        var result = _validator.Validate(command);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WithKnownOptions_ShouldSucceed()
    {
        var command = CreateCommand(new Dictionary<string, string>
        {
            ["deviceName"] = "MyFirstDevice",
            ["includeSensors"] = "true"
        });

        var result = _validator.Validate(command);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    public void Validate_WithUnknownOption_ShouldFail()
    {
        var command = CreateCommand(new Dictionary<string, string>
        {
            ["notAnOption"] = "42"
        });

        var result = _validator.Validate(command);

        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("notAnOption");
    }

    [Fact]
    public void Validate_WithWhitespaceKey_ShouldFail()
    {
        var command = CreateCommand(new Dictionary<string, string>
        {
            ["  "] = "value"
        });

        var result = _validator.Validate(command);

        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("non-empty");
    }

    [Fact]
    public void Validate_WithNonBooleanIncludeSensors_ShouldFail()
    {
        var command = CreateCommand(new Dictionary<string, string>
        {
            ["includeSensors"] = "yes"
        });

        var result = _validator.Validate(command);

        result.IsError.ShouldBeTrue();
        result.FirstError.Description.ShouldContain("includeSensors");
    }

    private static GetDevicePropsCommand CreateCommand(Dictionary<string, string> options) => new()
    {
        DeviceCmd = "props.get",
        Timeout = 10,
        Parameters = new GetDevicePropsParameters { Options = options }
    };
}
