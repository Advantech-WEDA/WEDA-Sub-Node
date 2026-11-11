using Shouldly;
using Xunit;

using SystemAgentExample.Devices;
using SystemAgentExample.Models;

using Weda.SubNode.Abstractions.Devices.Capabilities;
using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentDevice.Tests;

/// <summary>
/// Bug 58206: the names a System Agent gives for an empty <c>di.get</c> / <c>do.get</c>.
/// </summary>
/// <remarks>
/// GPIO sensors are named <c>gpio_pinState_*</c>, so the SDK's <c>di</c> / <c>do</c> name rule
/// found none of them. Only the pin's live direction says whether it is an input or an output.
/// </remarks>
public class GpioDigitalIoNamesTests
{
    private static Sensor Named(string name) => new() { Name = name };

    private static GpioPinDescriptor Pin(string name, string? direction, string? sensorName) =>
        new() { Name = name, Direction = direction, SensorName = sensorName };

    /// <summary>Pins as gpio.list reports them on an EPC-R7300, sensor names already bound.</summary>
    private static readonly GpioPinDescriptor[] Pins =
    [
        Pin("UIO_GPIO2", null, "gpio_pinState_UIO_GPIO2"),        // direction unreadable
        Pin("UIO_GPIO4", "output", "gpio_pinState_UIO_GPIO4"),
        Pin("UIO_GPIO10", "input", "gpio_pinState_UIO_GPIO10"),
        Pin("CN13_GPIO1", "input", "gpio_pinState_CN13_GPIO1"),
        Pin("CN13_GPIO7", "output", "gpio_pinState_CN13_GPIO7"),
        Pin("CN13_GPIO9", "input", null)                            // no sensor bound
    ];

    private static readonly Sensor[] Sensors =
    [
        Named("gpio_pinState_UIO_GPIO2"),
        Named("gpio_pinState_UIO_GPIO4"),
        Named("gpio_pinState_UIO_GPIO10"),
        Named("gpio_pinState_CN13_GPIO1"),
        Named("gpio_pinState_CN13_GPIO7"),
        Named("cpu_usage")
    ];

    [Fact]
    public void DigitalInputNames_AreTheBoundSensorsOfInputPins()
    {
        GpioPinLookup.DigitalInputNames(Pins, Sensors)
            .ShouldBe(["gpio_pinState_UIO_GPIO10", "gpio_pinState_CN13_GPIO1"]);
    }

    [Fact]
    public void DigitalOutputNames_AreTheBoundSensorsOfOutputPins()
    {
        GpioPinLookup.DigitalOutputNames(Pins, Sensors)
            .ShouldBe(["gpio_pinState_UIO_GPIO4", "gpio_pinState_CN13_GPIO7"]);
    }

    [Fact]
    public void DigitalNames_SkipAPinWhoseDirectionIsUnknown()
    {
        // UIO_GPIO2: reading it as either direction would guess.
        GpioPinLookup.DigitalInputNames(Pins, Sensors).ShouldNotContain("gpio_pinState_UIO_GPIO2");
        GpioPinLookup.DigitalOutputNames(Pins, Sensors).ShouldNotContain("gpio_pinState_UIO_GPIO2");
    }

    [Fact]
    public void DigitalNames_SkipAPinWithNoBoundSensor()
    {
        // The command handlers address a pin through its sensor; an unbound pin cannot be read.
        GpioPinLookup.DigitalInputNames(Pins, Sensors).ShouldNotContain("CN13_GPIO9");
    }

    [Fact]
    public void DigitalNames_KeepDiAndDoSensorsOfTheSdkRule()
    {
        // A di_* / do_* sensor configured on the System Agent is still read, once.
        Sensor[] sensors = [.. Sensors, Named("di_0"), Named("do_0")];
        GpioPinDescriptor[] pins = [.. Pins, Pin("di_0", "input", "di_0")];

        GpioPinLookup.DigitalInputNames(pins, sensors)
            .ShouldBe(["gpio_pinState_UIO_GPIO10", "gpio_pinState_CN13_GPIO1", "di_0"]);
        GpioPinLookup.DigitalOutputNames(pins, sensors)
            .ShouldBe(["gpio_pinState_UIO_GPIO4", "gpio_pinState_CN13_GPIO7", "do_0"]);
    }

    [Theory]
    [InlineData(typeof(IDigitalInputReadable), nameof(IDigitalInputReadable.GetDigitalInputNames))]
    [InlineData(typeof(IDigitalOutputReadable), nameof(IDigitalOutputReadable.GetDigitalOutputNames))]
    public void SystemAgentDevice_ReplacesTheSdkNameRule(Type capability, string member)
    {
        // Without its own implementation the device silently inherits the SDK's di/do name
        // rule, and an empty di.get / do.get fails again. No hardware is needed to check this.
        var map = typeof(SystemAgentDeviceBase).GetInterfaceMap(capability);
        var target = map.TargetMethods[Array.FindIndex(map.InterfaceMethods, m => m.Name == member)];

        target.DeclaringType.ShouldBe(typeof(SystemAgentDeviceBase));
    }

    [Fact]
    public void DigitalNames_NoGpio_FallBackToTheSdkRule()
    {
        // No SUSI GPIO on this board: only the configured di_* / do_* sensors remain.
        Sensor[] sensors = [Named("di_0"), Named("do_0"), Named("cpu_usage")];

        GpioPinLookup.DigitalInputNames([], sensors).ShouldBe(["di_0"]);
        GpioPinLookup.DigitalOutputNames([], sensors).ShouldBe(["do_0"]);
    }
}
