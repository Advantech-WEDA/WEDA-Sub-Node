using CommandHandlerExample.Commands.GetDeviceProps;
using CommandHandlerExample.Commands.GetDeviceProps.Models;

using NSubstitute;

using Shouldly;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.TestBase;

using Xunit;

namespace CommandHandlerExample.Tests;

/// <summary>
/// Unit tests for GetDevicePropsCommandHandler — the command whose
/// ICommandHandler input and result data are both Dictionary&lt;string, T&gt;
/// maps (options: string → string, result: string → DevicePropsEntry).
/// </summary>
public class GetDevicePropsCommandHandlerTests : IDisposable
{
    private readonly MockApplicationContext _context;
    private readonly GetDevicePropsCommandHandler _handler;

    public GetDevicePropsCommandHandlerTests()
    {
        _context = new MockApplicationContext();
        _handler = new GetDevicePropsCommandHandler();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Success Scenarios

    [Fact]
    public async Task HandleAsync_WithEmptyOptions_ShouldReturnAllDevices()
    {
        // Arrange
        RegisterMockDevice("DeviceA", sensorCount: 2);
        RegisterMockDevice("DeviceB", sensorCount: 1);

        var command = CreateCommand([]);

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(CommandStatusCode.Success);
        result.Value.ResultData.ShouldNotBeNull();
        result.Value.ResultData.Count.ShouldBe(2);
        result.Value.ResultData.Keys.ShouldContain("DeviceA");
        result.Value.ResultData.Keys.ShouldContain("DeviceB");

        var propsA = result.Value.ResultData["DeviceA"];
        propsA.SensorCount.ShouldBe(2);
        propsA.SubNodeType.ShouldBe("AdamEthernet");
        propsA.Sensors.ShouldBeNull(); // includeSensors defaults to false
    }

    [Fact]
    public async Task HandleAsync_WithOptions_ShouldFilterAndIncludeSensors()
    {
        // Arrange
        RegisterMockDevice("DeviceA", sensorCount: 1);
        RegisterMockDevice("DeviceB", sensorCount: 1);

        var command = CreateCommand(new Dictionary<string, string>
        {
            ["deviceName"] = "devicea",     // case-insensitive
            ["IncludeSensors"] = "true"     // option keys case-insensitive too
        });

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.Value.Status.ShouldBe(CommandStatusCode.Success);
        result.Value.ResultData!.Count.ShouldBe(1);
        var props = result.Value.ResultData["DeviceA"];
        props.Sensors.ShouldNotBeNull();
        props.Sensors.Count.ShouldBe(1);
        props.Sensors[0].Name.ShouldBe("sensor_0");
        props.Sensors[0].Unit.ShouldBe("celsius");
        props.Sensors[0].IntervalMs.ShouldBe(1000);
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task HandleAsync_WhenNoDevicesRegistered_ShouldReturnNotFound()
    {
        // Arrange - no device registered
        var command = CreateCommand([]);

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.IsError.ShouldBeFalse(); // Handler reports failure via result status
        result.Value.Status.ShouldBe(CommandStatusCode.NotFound);
        result.Value.Message.ShouldBe("No devices are registered");
    }

    [Fact]
    public async Task HandleAsync_WhenDeviceNameUnknown_ShouldReturnNotFound()
    {
        // Arrange
        RegisterMockDevice("DeviceA", sensorCount: 1);
        var command = CreateCommand(new Dictionary<string, string>
        {
            ["deviceName"] = "NoSuchDevice"
        });

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.Value.Status.ShouldBe(CommandStatusCode.NotFound);
        result.Value.Message.ShouldContain("NoSuchDevice");
    }

    #endregion

    [Fact]
    public async Task HandleAsync_WithBlankDeviceNameOption_ShouldReturnAllDevices()
    {
        // Arrange - blank option values are treated as absent
        RegisterMockDevice("DeviceA", sensorCount: 1);
        RegisterMockDevice("DeviceB", sensorCount: 1);
        var command = CreateCommand(new Dictionary<string, string> { ["deviceName"] = "   " });

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.Value.Status.ShouldBe(CommandStatusCode.Success);
        result.Value.ResultData!.Count.ShouldBe(2);
    }

    #region Helpers

    private static GetDevicePropsCommand CreateCommand(Dictionary<string, string> options) => new()
    {
        DeviceCmd = "props.get",
        Timeout = 10,
        Parameters = new GetDevicePropsParameters { Options = options }
    };

    private void RegisterMockDevice(string deviceName, int sensorCount)
    {
        var sensors = Enumerable.Range(0, sensorCount)
            .Select(i => new Sensor
            {
                ResourceId = $"00000000-0000-0000-0000-0000000{i:00}a",
                Name = $"sensor_{i}",
                SensorGroup = SensorGroup.TEMP,
                Report = new SensorReport { Enabled = true, Interval = 1000, Unit = "celsius" }
            })
            .ToList();

        var configuration = new DeviceConfiguration
        {
            DeviceName = deviceName,
            Enabled = true,
            SubNodeInfo = _context.SubNodeInfo,
            Sensors = sensors
        };

        var device = Substitute.For<IDevice>();
        device.Configuration.Returns(configuration);
        device.DeviceName.Returns(deviceName);
        device.SubNodeType.Returns(SubNodeType.AdamEthernet);

        _context.DeviceRegistry.Register(device);
    }

    #endregion
}
