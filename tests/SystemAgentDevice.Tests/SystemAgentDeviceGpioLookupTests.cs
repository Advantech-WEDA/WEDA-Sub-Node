using Shouldly;
using Xunit;

using SystemAgentExample.Communication;
using SystemAgentExample.Devices;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.TestBase;

namespace SystemAgentDevice.Tests;

/// <summary>
/// Covers <see cref="SystemAgentDeviceBase.FindSensor"/> on the real device class.
///
/// This is the lookup the built-in di.get / do.get / do.set handlers call to pick the
/// target device before they delegate — see <c>GetDigitalInputCommandHandler</c>,
/// <c>GetDigitalOutputCommandHandler</c> and <c>SetDigitalOutputCommandHandler</c>, each
/// of which skips a device whose <c>FindSensor</c> returns null. A bare hardware pin name
/// therefore has to resolve *here*, or the command fails as "not found on any device"
/// before any GPIO code runs.
///
/// These tests instantiate the device itself rather than the lookup helper, so deleting
/// the override fails them.
/// </summary>
public class SystemAgentDeviceGpioLookupTests
{
    private static Sensor PinSensor(string name, string pinId) => new()
    {
        Name = name,
        SensorInfo = new SensorInfo
        {
            Schema = "integer",
            DisplayName = name,
            Description = "GPIO pin state"
        },
        Parameters = new Dictionary<string, object>
        {
            ["MetricType"] = "gpio",
            ["MetricName"] = "pinState",
            ["PinId"] = pinId
        }
    };

    /// <summary>
    /// Builds the real device over the local communication stack. No Advantech hardware is
    /// needed: the HAL simply reports as unavailable, which does not affect sensor lookup.
    /// </summary>
    private static SystemAgentDeviceBase CreateDevice(params Sensor[] sensors)
    {
        var configuration = new DeviceConfiguration
        {
            DeviceName = "SystemAgentDeviceConfig",
            Dtdl = new DtdlConfig { AutoGenEnabled = true },
            Sensors = [.. sensors]
        };

        return new SystemAgentDeviceBase(
            new MockApplicationContext(), configuration, new LocalSystemCommunication());
    }

    [Fact]
    public void FindSensor_BarePinName_ResolvesToTheBoundSensor()
    {
        // The name gpio.list reports. Before the override this returned null and every
        // di/do command against the pin failed as "not found on any device".
        using var device = CreateDevice(PinSensor("gpio_pinState_UIO_GPIO2", "UIO_GPIO2"));

        var sensor = device.FindSensor("UIO_GPIO2");

        sensor.ShouldNotBeNull();
        sensor.Name.ShouldBe("gpio_pinState_UIO_GPIO2");
    }

    [Fact]
    public void FindSensor_BarePinName_IsCaseInsensitive()
    {
        using var device = CreateDevice(PinSensor("gpio_pinState_UIO_GPIO2", "UIO_GPIO2"));

        device.FindSensor("uio_gpio2").ShouldNotBeNull();
    }

    [Fact]
    public void FindSensor_ConfiguredSensorName_StillResolves()
    {
        using var device = CreateDevice(PinSensor("gpio_pinState_UIO_GPIO2", "UIO_GPIO2"));

        device.FindSensor("gpio_pinState_UIO_GPIO2").ShouldNotBeNull();
    }

    [Fact]
    public void FindSensor_ConfiguredNameWinsOverPinAlias()
    {
        // A sensor named after a pin, but bound to a different one, must not be shadowed.
        using var device = CreateDevice(
            PinSensor("UIO_GPIO2", "CN13_GPIO1"),
            PinSensor("gpio_pinState_UIO_GPIO2", "UIO_GPIO2"));

        device.FindSensor("UIO_GPIO2")!.Name.ShouldBe("UIO_GPIO2");
    }

    [Fact]
    public void FindSensor_PinBoundByTwoSensors_ResolvesToNull()
    {
        // Refuse rather than guess: driving the wrong pin is worse than failing.
        using var device = CreateDevice(
            PinSensor("gpio_pinState_UIO_GPIO2", "UIO_GPIO2"),
            PinSensor("my_relay", "UIO_GPIO2"));

        device.FindSensor("UIO_GPIO2").ShouldBeNull();
    }

    [Fact]
    public void FindSensor_UnknownName_ReturnsNull()
    {
        using var device = CreateDevice(PinSensor("gpio_pinState_UIO_GPIO2", "UIO_GPIO2"));

        device.FindSensor("NOT_A_PIN").ShouldBeNull();
    }
}
