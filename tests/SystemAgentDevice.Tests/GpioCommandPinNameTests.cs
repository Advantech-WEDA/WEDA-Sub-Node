using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;
using Xunit;

using SystemAgentExample.Devices;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentDevice.Tests;

/// <summary>
/// Covers the di.get / do.get / do.set path end to end over a fake pin table:
/// the command name is resolved to a hardware pin exactly as SystemAgentDeviceBase
/// does it, then handed to GpioDigitalIo.
///
/// The device wires GpioDigitalIo with delegates that pre-resolve the name
/// (<c>pin =&gt; GetGpioPinLevel(ResolveHardwarePinName(pin))</c>); this reproduces that
/// composition so both spellings can be proven to reach the same pin without hardware
/// or an application context.
/// </summary>
public class GpioCommandPinNameTests
{
    private readonly Dictionary<string, bool> _levels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _directions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _pinsWritten = [];

    /// <summary>The 19-pin EPC-R7300 layout in miniature: inputs plus one output.</summary>
    private readonly List<Sensor> _sensors =
    [
        PinSensor("gpio_pinState_UIO_GPIO2", "UIO_GPIO2"),
        PinSensor("gpio_pinState_UIO_GPIO10", "UIO_GPIO10"),
        PinSensor("gpio_pinState_CN13_GPIO1", "CN13_GPIO1")
    ];

    public GpioCommandPinNameTests()
    {
        _directions["UIO_GPIO2"] = "input";
        _directions["CN13_GPIO1"] = "input";
        _directions["UIO_GPIO10"] = "output";

        _levels["UIO_GPIO2"] = true;
        _levels["CN13_GPIO1"] = false;
        _levels["UIO_GPIO10"] = false;
    }

    private static Sensor PinSensor(string name, string pinId) => new()
    {
        Name = name,
        SensorInfo = new SensorInfo { Schema = "integer" },
        Parameters = new Dictionary<string, object>
        {
            ["MetricType"] = "gpio",
            ["MetricName"] = "pinState",
            ["PinId"] = pinId
        }
    };

    /// <summary>Mirrors how SystemAgentDeviceBase composes GpioDigitalIo.</summary>
    private GpioDigitalIo CreateIo() => new(
        name => _levels.TryGetValue(Resolve(name), out var level) ? level : null,
        (name, state) =>
        {
            var pin = Resolve(name);
            _pinsWritten.Add(pin);
            _levels[pin] = state;
            return true;
        },
        name => _directions.GetValueOrDefault(Resolve(name)),
        NullLogger.Instance,
        verifyDelay: TimeSpan.Zero);

    private string Resolve(string name) => GpioPinLookup.ResolveHardwarePin(_sensors, name);

    #region di.get

    [Theory]
    [InlineData("UIO_GPIO2")]                  // bare pin name, as gpio.list reports it
    [InlineData("gpio_pinState_UIO_GPIO2")]    // configured sensor name
    public void DiGet_BothSpellings_ReadTheSamePin(string requestedName)
    {
        CreateIo().GetLevel(requestedName).ShouldBe(true);
    }

    [Fact]
    public void DiGet_UnknownName_ReturnsNull()
    {
        CreateIo().GetLevel("NOT_A_PIN").ShouldBeNull();
    }

    #endregion

    #region do.get

    [Theory]
    [InlineData("UIO_GPIO10")]
    [InlineData("gpio_pinState_UIO_GPIO10")]
    public void DoGet_BothSpellings_ReadTheSameOutputPin(string requestedName)
    {
        _levels["UIO_GPIO10"] = true;

        CreateIo().GetLevel(requestedName).ShouldBe(true);
    }

    #endregion

    #region do.set

    [Theory]
    [InlineData("UIO_GPIO10")]
    [InlineData("gpio_pinState_UIO_GPIO10")]
    public async Task DoSet_BothSpellings_WriteTheSamePin(string requestedName)
    {
        var result = await CreateIo().SetOutputAsync(requestedName, true);

        result.ShouldBeTrue();
        _pinsWritten.ShouldBe(["UIO_GPIO10"]);
        _levels["UIO_GPIO10"].ShouldBeTrue();
    }

    [Theory]
    [InlineData("UIO_GPIO2")]
    [InlineData("gpio_pinState_UIO_GPIO2")]
    public async Task DoSet_InputPin_IsRefusedByEitherSpelling(string requestedName)
    {
        // The direction guard must not be bypassable by addressing the pin differently.
        var result = await CreateIo().SetOutputAsync(requestedName, true);

        result.ShouldBeFalse();
        _pinsWritten.ShouldBeEmpty();
        _levels["UIO_GPIO2"].ShouldBeTrue("the refused write must not change the pin");
    }

    [Fact]
    public async Task DoSet_UnknownName_IsRefused()
    {
        var result = await CreateIo().SetOutputAsync("NOT_A_PIN", true);

        result.ShouldBeFalse();
        _pinsWritten.ShouldBeEmpty();
    }

    #endregion
}
