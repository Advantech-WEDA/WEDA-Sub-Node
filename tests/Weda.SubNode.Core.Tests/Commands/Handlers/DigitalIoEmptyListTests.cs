using NSubstitute;
using Shouldly;
using Xunit;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Devices.Capabilities;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Commands.Handlers.GetDigitalInput;
using Weda.SubNode.Core.Commands.Handlers.GetDigitalInput.Models;
using Weda.SubNode.Core.Commands.Handlers.GetDigitalOutput;
using Weda.SubNode.Core.Commands.Handlers.GetDigitalOutput.Models;
using Weda.SubNode.TestBase;

namespace Weda.SubNode.Core.Tests.Commands.Handlers;

/// <summary>
/// Bug 58206: <c>di.get</c> with <c>inputs: []</c> and <c>do.get</c> with <c>outputs: []</c>
/// must read every input / output the device has.
/// </summary>
/// <remarks>
/// The handlers used to pick the pins themselves, keeping only sensor names that start with
/// <c>di</c> / <c>do</c>. A System Agent names its GPIO sensors <c>gpio_pinState_*</c> and only
/// the device knows each pin's direction, so an empty list found nothing and the command failed
/// with HARDWARE_ERROR "No inputs available to read". The handlers now ask each device for its
/// input / output names.
/// </remarks>
public class DigitalIoEmptyListTests : IDisposable
{
    private const string DeviceName = "SystemAgentDeviceConfig";

    private readonly MockApplicationContext _context = new();

    /// <summary>Two input pins and one output pin, as a System Agent configures them.</summary>
    private readonly Dictionary<string, bool> _levels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpio_pinState_CN13_GPIO1"] = true,
        ["gpio_pinState_UIO_GPIO10"] = false,
        ["gpio_pinState_UIO_GPIO4"] = true
    };

    private static readonly string[] InputNames = ["gpio_pinState_CN13_GPIO1", "gpio_pinState_UIO_GPIO10"];
    private static readonly string[] OutputNames = ["gpio_pinState_UIO_GPIO4"];

    public void Dispose() => _context.Dispose();

    private DeviceConfiguration Configuration() => new()
    {
        DeviceName = DeviceName,
        Sensors = [.. _levels.Keys.Select(name => new Sensor { Name = name })]
    };

    /// <summary>
    /// A device that reads both directions and reports its own input and output names.
    /// </summary>
    private IDevice RegisterGpioDevice()
    {
        var configuration = Configuration();
        var device = Substitute.For<IDigitalInputReadable, IDigitalOutputReadable>();
        device.DeviceName.Returns(DeviceName);
        device.Configuration.Returns(configuration);
        device.FindSensor(Arg.Any<string>())
            .Returns(call => configuration.Sensors.FirstOrDefault(s =>
                s.Name.Equals(call.Arg<string>(), StringComparison.OrdinalIgnoreCase)));

        device.GetDigitalInputNames().Returns(InputNames);
        device.GetDigitalInputAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<bool?>(_levels[call.Arg<string>()]));

        var output = (IDigitalOutputReadable)device;
        output.GetDigitalOutputNames().Returns(OutputNames);
        output.GetDigitalOutputAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<bool?>(_levels[call.Arg<string>()]));

        _context.DeviceRegistry.Register(device);
        return device;
    }

    [Fact]
    public async Task DiGet_EmptyInputs_ReadsEveryInputTheDeviceReports()
    {
        RegisterGpioDevice();
        var command = new GetDigitalInputCommand { Parameters = new GetDigitalInputParameters { Inputs = [] } };

        var result = await new GetDigitalInputCommandHandler().HandleAsync(command, _context);

        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(CommandStatusCode.Success, result.Value.Message);
        result.Value.ResultData.ShouldNotBeNull();
        result.Value.ResultData.Values.Select(v => (v.Name, v.State))
            .ShouldBe([("gpio_pinState_CN13_GPIO1", true), ("gpio_pinState_UIO_GPIO10", false)]);
        result.Value.ResultData.Errors.ShouldBeNull();
    }

    [Fact]
    public async Task DoGet_EmptyOutputs_ReadsEveryOutputTheDeviceReports()
    {
        RegisterGpioDevice();
        var command = new GetDigitalOutputCommand { Parameters = new GetDigitalOutputParameters { Outputs = [] } };

        var result = await new GetDigitalOutputCommandHandler().HandleAsync(command, _context);

        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(CommandStatusCode.Success, result.Value.Message);
        result.Value.ResultData.ShouldNotBeNull();
        result.Value.ResultData.Values.Select(v => (v.Name, v.State))
            .ShouldBe([("gpio_pinState_UIO_GPIO4", true)]);
    }

    [Fact]
    public async Task DiGet_NamedInputs_StillReadOnlyThoseInputs()
    {
        // The device's input list is only the default for an empty request.
        RegisterGpioDevice();
        var command = new GetDigitalInputCommand
        {
            Parameters = new GetDigitalInputParameters { Inputs = ["gpio_pinState_UIO_GPIO10"] }
        };

        var result = await new GetDigitalInputCommandHandler().HandleAsync(command, _context);

        result.Value.Status.ShouldBe(CommandStatusCode.Success, result.Value.Message);
        result.Value.ResultData!.Values.Select(v => v.Name).ShouldBe(["gpio_pinState_UIO_GPIO10"]);
    }

    [Fact]
    public void DigitalIoNames_DefaultRule_KeepsSensorsWithThePrefix()
    {
        // The rule every device keeps unless it knows better (Modbus, iSensing): sensor names
        // that start with "di" / "do", case-insensitively, each once.
        var configuration = new DeviceConfiguration
        {
            DeviceName = "adam",
            Sensors =
            [
                new Sensor { Name = "di_0" },
                new Sensor { Name = "DI_1" },
                new Sensor { Name = "do_0" },
                new Sensor { Name = "temperature" },
                new Sensor { Name = "di_0" }
            ]
        };

        DigitalIoNames.WithPrefix(configuration, DigitalIoNames.InputPrefix).ShouldBe(["di_0", "DI_1"]);
        DigitalIoNames.WithPrefix(configuration, DigitalIoNames.OutputPrefix).ShouldBe(["do_0"]);
    }
}
