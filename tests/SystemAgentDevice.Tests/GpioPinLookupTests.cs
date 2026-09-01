using Shouldly;
using Xunit;

using SystemAgentExample.Devices;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentDevice.Tests;

/// <summary>
/// Tests the pin-name lookup that lets di.get / do.get / do.set address a pin as
/// "UIO_GPIO2" — the name gpio.list reports — instead of the resolved sensor name
/// "gpio_pinState_UIO_GPIO2".
/// </summary>
public class GpioPinLookupTests
{
    private static Sensor Sensor(string name, string metricName = "pinState", string? pinId = null)
    {
        var parameters = new Dictionary<string, object>
        {
            ["MetricType"] = "gpio",
            ["MetricName"] = metricName
        };

        if (pinId != null)
            parameters["PinId"] = pinId;

        return new Sensor
        {
            Name = name,
            SensorInfo = new SensorInfo { Schema = "integer" },
            Parameters = parameters
        };
    }

    /// <summary>What SensorResolver produces from a "PinIds": [] template.</summary>
    private static List<Sensor> ResolvedPins() =>
    [
        Sensor("gpio_pinState_UIO_GPIO2", pinId: "UIO_GPIO2"),
        Sensor("gpio_pinState_UIO_GPIO10", pinId: "UIO_GPIO10"),
        Sensor("gpio_isSupported", metricName: "isSupported"),
        Sensor("gpio_pinState")   // unresolved capability template: no PinId
    ];

    [Fact]
    public void FindByPinName_ResolvesBarePinNameToSensor()
    {
        var found = GpioPinLookup.FindByPinName(ResolvedPins(), "UIO_GPIO2", out var ambiguous);

        found.ShouldNotBeNull();
        found.Name.ShouldBe("gpio_pinState_UIO_GPIO2");
        ambiguous.ShouldBeEmpty();
    }

    [Fact]
    public void FindByPinName_IsCaseInsensitive()
    {
        GpioPinLookup.FindByPinName(ResolvedPins(), "uio_gpio10", out _).ShouldNotBeNull();
    }

    [Fact]
    public void FindByPinName_UnknownPin_ReturnsNull()
    {
        GpioPinLookup.FindByPinName(ResolvedPins(), "UIO_GPIO99", out var ambiguous).ShouldBeNull();
        ambiguous.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FindByPinName_BlankName_ReturnsNull(string name)
    {
        GpioPinLookup.FindByPinName(ResolvedPins(), name, out _).ShouldBeNull();
    }

    [Fact]
    public void FindByPinName_IgnoresSensorsNotBoundToAPin()
    {
        // gpio_isSupported is device-wide; the bare gpio_pinState template is unresolved.
        // Neither is addressable by a di/do command.
        GpioPinLookup.BoundPinOf(Sensor("gpio_isSupported", metricName: "isSupported")).ShouldBeNull();
        GpioPinLookup.BoundPinOf(Sensor("gpio_pinState")).ShouldBeNull();
    }

    [Fact]
    public void FindByPinName_TwoSensorsOnOnePin_RefusesAndReportsBoth()
    {
        List<Sensor> sensors =
        [
            Sensor("gpio_pinState_UIO_GPIO2", pinId: "UIO_GPIO2"),
            Sensor("my_relay", pinId: "UIO_GPIO2")
        ];

        var found = GpioPinLookup.FindByPinName(sensors, "UIO_GPIO2", out var ambiguous);

        found.ShouldBeNull();
        ambiguous.Count.ShouldBe(2);
        ambiguous.ShouldContain("gpio_pinState_UIO_GPIO2");
        ambiguous.ShouldContain("my_relay");
    }

    #region FindByNameOrPin — what the command handlers gate on

    [Fact]
    public void FindByNameOrPin_SensorName_StillResolves()
    {
        // The spelling that worked before this change must keep working.
        var found = GpioPinLookup.FindByNameOrPin(ResolvedPins(), "gpio_pinState_UIO_GPIO2", out _);

        found.ShouldNotBeNull();
        found.Name.ShouldBe("gpio_pinState_UIO_GPIO2");
    }

    [Fact]
    public void FindByNameOrPin_BarePinName_Resolves()
    {
        var found = GpioPinLookup.FindByNameOrPin(ResolvedPins(), "UIO_GPIO2", out _);

        found.ShouldNotBeNull();
        found.Name.ShouldBe("gpio_pinState_UIO_GPIO2");
    }

    [Fact]
    public void FindByNameOrPin_ConfiguredNameWinsOverPinAlias()
    {
        // A sensor literally named "UIO_GPIO2" that is bound to a *different* pin must
        // win: an alias may never shadow a real sensor.
        List<Sensor> sensors =
        [
            Sensor("UIO_GPIO2", pinId: "CN13_GPIO1"),
            Sensor("gpio_pinState_UIO_GPIO2", pinId: "UIO_GPIO2")
        ];

        var found = GpioPinLookup.FindByNameOrPin(sensors, "UIO_GPIO2", out _);

        found.ShouldNotBeNull();
        found.Name.ShouldBe("UIO_GPIO2");
        GpioPinLookup.ResolveHardwarePin(sensors, "UIO_GPIO2").ShouldBe("CN13_GPIO1");
    }

    [Fact]
    public void FindByNameOrPin_UnknownName_ReturnsNull()
    {
        // This is the gate that used to reject a bare pin name outright.
        GpioPinLookup.FindByNameOrPin(ResolvedPins(), "NOT_A_PIN", out _).ShouldBeNull();
    }

    #endregion

    #region ResolveHardwarePin — the name handed to the driver

    [Theory]
    [InlineData("gpio_pinState_UIO_GPIO2", "UIO_GPIO2")]  // sensor name -> its PinId
    [InlineData("UIO_GPIO2", "UIO_GPIO2")]                // bare pin name -> itself
    [InlineData("uio_gpio2", "UIO_GPIO2")]                // case-insensitive
    [InlineData("gpio_pinState_UIO_GPIO10", "UIO_GPIO10")]
    public void ResolveHardwarePin_MapsBothSpellingsToTheSamePin(string requested, string expectedPin)
    {
        GpioPinLookup.ResolveHardwarePin(ResolvedPins(), requested).ShouldBe(expectedPin);
    }

    [Fact]
    public void ResolveHardwarePin_UnboundName_PassesThroughUnchanged()
    {
        // Only reachable once the handler's FindSensor gate accepted the name;
        // the driver rejects it if it is not a real pin.
        GpioPinLookup.ResolveHardwarePin(ResolvedPins(), "NOT_A_PIN").ShouldBe("NOT_A_PIN");
    }

    [Fact]
    public void ResolveHardwarePin_SensorWithoutPinId_PassesThroughUnchanged()
    {
        GpioPinLookup.ResolveHardwarePin(ResolvedPins(), "gpio_isSupported").ShouldBe("gpio_isSupported");
    }

    #endregion

    [Fact]
    public void BoundPinOf_ReturnsThePinIdParameter()
    {
        GpioPinLookup.BoundPinOf(Sensor("gpio_pinState_UIO_GPIO2", pinId: "UIO_GPIO2"))
            .ShouldBe("UIO_GPIO2");
    }
}
