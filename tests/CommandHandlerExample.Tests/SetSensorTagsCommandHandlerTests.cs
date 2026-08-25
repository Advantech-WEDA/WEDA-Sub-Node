using CommandHandlerExample.Commands.SetSensorTags;
using CommandHandlerExample.Commands.SetSensorTags.Models;

using NSubstitute;

using Shouldly;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.TestBase;

using Xunit;

namespace CommandHandlerExample.Tests;

/// <summary>
/// Unit tests for SetSensorTagsCommandHandler.
/// Tests the custom "tag.set" command whose parameters carry a
/// map-typed (Dictionary&lt;string, string&gt;) tags payload.
/// </summary>
public class SetSensorTagsCommandHandlerTests : IDisposable
{
    private const string SensorName = "temperature_sensor";
    private const string ResourceId = "21af0dc4-9254-5389-a7dd-df64d7cf782c";

    private readonly MockApplicationContext _context;
    private readonly SetSensorTagsCommandHandler _handler;
    private readonly IDevice _mockDevice;

    public SetSensorTagsCommandHandlerTests()
    {
        _context = new MockApplicationContext();
        _handler = new SetSensorTagsCommandHandler();
        _mockDevice = Substitute.For<IDevice>();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Success Scenarios

    [Fact]
    public async Task HandleAsync_WithTags_ShouldMergeIntoSensorMetadata()
    {
        // Arrange
        var sensor = CreateSensor();
        SetupMockDevice("TestDevice", sensor);

        var command = CreateCommand(SensorName, new Dictionary<string, string>
        {
            ["location"] = "line-3",
            ["zone"] = "assembly"
        });

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(CommandStatusCode.Success);
        result.Value.ResultData.ShouldNotBeNull();
        result.Value.ResultData.AppliedCount.ShouldBe(2);
        result.Value.ResultData.Tags["location"].ShouldBe("line-3");
        result.Value.ResultData.TotalMetadataCount.ShouldBe(2);

        sensor.Metadata.ShouldNotBeNull();
        sensor.Metadata["location"].ShouldBe("line-3");
        sensor.Metadata["zone"].ShouldBe("assembly");
    }

    [Fact]
    public async Task HandleAsync_WithExistingMetadata_ShouldPreserveUnrelatedAndOverwriteMatching()
    {
        // Arrange
        var sensor = CreateSensor();
        sensor.Metadata = new Dictionary<string, object>
        {
            ["calibration"] = 1.05,
            ["location"] = "line-1"
        };
        var original = sensor.Metadata;
        SetupMockDevice("TestDevice", sensor);

        var command = CreateCommand(SensorName, new Dictionary<string, string>
        {
            ["location"] = "line-3"
        });

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.Value.Status.ShouldBe(CommandStatusCode.Success);
        sensor.Metadata.ShouldNotBeNull();
        sensor.Metadata["location"].ShouldBe("line-3");     // overwritten
        sensor.Metadata["calibration"].ShouldBe(1.05);       // preserved
        result.Value.ResultData!.TotalMetadataCount.ShouldBe(2);

        // The merge must swap in a new dictionary, not mutate the old one
        sensor.Metadata.ShouldNotBeSameAs(original);
        original["location"].ShouldBe("line-1");
    }

    [Fact]
    public async Task HandleAsync_WithExplicitDeviceName_ShouldTargetThatDevice()
    {
        // Arrange
        var sensor = CreateSensor();
        SetupMockDevice("TestDevice", sensor);

        var command = CreateCommand(SensorName, new Dictionary<string, string>
        {
            ["owner"] = "demo"
        }, deviceName: "testdevice"); // case-insensitive

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.Value.Status.ShouldBe(CommandStatusCode.Success);
        result.Value.ResultData!.DeviceName.ShouldBe("TestDevice");
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task HandleAsync_WhenNoDevicesRegistered_ShouldReturnNotFound()
    {
        // Arrange - no device registered
        var command = CreateCommand(SensorName, new Dictionary<string, string> { ["a"] = "b" });

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
        SetupMockDevice("TestDevice", CreateSensor());
        var command = CreateCommand(SensorName,
            new Dictionary<string, string> { ["a"] = "b" }, deviceName: "NoSuchDevice");

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.Value.Status.ShouldBe(CommandStatusCode.NotFound);
        result.Value.Message.ShouldContain("NoSuchDevice");
    }

    [Fact]
    public async Task HandleAsync_WhenSensorNotFound_ShouldReturnNotFound()
    {
        // Arrange - device registered but sensor lookup misses
        SetupMockDevice("TestDevice", sensor: null);
        var command = CreateCommand("no_such_sensor", new Dictionary<string, string> { ["a"] = "b" });

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.Value.Status.ShouldBe(CommandStatusCode.NotFound);
        result.Value.Message.ShouldContain("no_such_sensor");
    }

    #endregion

    #region Helpers

    private static SetSensorTagsCommand CreateCommand(
        string sensorName,
        Dictionary<string, string> tags,
        string? deviceName = null) => new()
        {
            DeviceCmd = "tag.set",
            Timeout = 10,
            Parameters = new SetSensorTagsParameters
            {
                SensorName = sensorName,
                DeviceName = deviceName,
                Tags = tags
            }
        };

    private static Sensor CreateSensor() => new()
    {
        ResourceId = ResourceId,
        Name = SensorName,
        Report = new SensorReport { Enabled = true, Interval = 1000 }
    };

    private void SetupMockDevice(string deviceName, Sensor? sensor)
    {
        var configuration = new DeviceConfiguration
        {
            DeviceName = deviceName,
            SubNodeInfo = _context.SubNodeInfo,
            Sensors = sensor is null ? [] : [sensor]
        };

        _mockDevice.Configuration.Returns(configuration);
        _mockDevice.DeviceName.Returns(deviceName);
        _mockDevice.FindSensor(Arg.Any<string>()).Returns((Sensor?)null);

        if (sensor is not null)
        {
            _mockDevice.FindSensor(sensor.Name).Returns(sensor);
        }

        _context.DeviceRegistry.Register(_mockDevice);
    }

    #endregion
}
