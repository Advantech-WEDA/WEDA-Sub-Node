using CommandHandlerExample.Commands.SensorRead;
using CommandHandlerExample.Commands.SensorRead.Models;

using NSubstitute;

using Shouldly;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.TestBase;

using Xunit;

namespace CommandHandlerExample.Tests;

/// <summary>
/// Unit tests for SensorReadCommandHandler.
/// Tests the custom "sensor.read" command that reads the latest cached
/// telemetry value of a named sensor.
/// </summary>
public class SensorReadCommandHandlerTests : IDisposable
{
    private const string SensorName = "temperature_sensor";
    private const string ResourceId = "21af0dc4-9254-5389-a7dd-df64d7cf782c";

    private readonly MockApplicationContext _context;
    private readonly SensorReadCommandHandler _handler;
    private readonly IDevice _mockDevice;

    public SensorReadCommandHandlerTests()
    {
        _context = new MockApplicationContext();
        _handler = new SensorReadCommandHandler();
        _mockDevice = Substitute.For<IDevice>();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Success Scenarios

    [Fact]
    public async Task HandleAsync_WithCachedValue_ShouldReturnSuccessWithLatestReading()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SetupMockDevice("TestDevice", CreateSensor(unit: "celsius"), new List<TelemetryMeasure>
        {
            new() { ResourceId = ResourceId, Value = 24.5f, Timestamp = timestamp - 1000 },
            new() { ResourceId = ResourceId, Value = 25.5f, Timestamp = timestamp }
        });

        var command = CreateCommand(SensorName);

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(CommandStatusCode.Success);
        result.Value.ResultData.ShouldNotBeNull();
        result.Value.ResultData.DeviceName.ShouldBe("TestDevice");
        result.Value.ResultData.SensorName.ShouldBe(SensorName);
        result.Value.ResultData.Value.ShouldNotBeNull();
        result.Value.ResultData.Value.Value.ShouldBe(25.5, tolerance: 0.001);
        result.Value.ResultData.Timestamp.ShouldBe(timestamp);
        result.Value.ResultData.Unit.ShouldBe("celsius");
    }

    [Fact]
    public async Task HandleAsync_WithExplicitDeviceName_ShouldReadFromThatDevice()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SetupMockDevice("TestDevice", CreateSensor(), new List<TelemetryMeasure>
        {
            new() { ResourceId = ResourceId, Value = 22.0, Timestamp = timestamp }
        });

        var command = CreateCommand(SensorName, deviceName: "testdevice"); // case-insensitive

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(CommandStatusCode.Success);
        result.Value.ResultData!.Value.ShouldBe(22.0);
    }

    [Fact]
    public async Task HandleAsync_WithNonNumericValue_ShouldReturnDisplayValueOnly()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SetupMockDevice("TestDevice", CreateSensor(), new List<TelemetryMeasure>
        {
            new() { ResourceId = ResourceId, Value = "running", Timestamp = timestamp }
        });

        var command = CreateCommand(SensorName);

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(CommandStatusCode.Success);
        result.Value.ResultData!.Value.ShouldBeNull();
        result.Value.ResultData.DisplayValue.ShouldBe("running");
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task HandleAsync_WhenNoDevicesRegistered_ShouldReturnNotFound()
    {
        // Arrange - no device registered
        var command = CreateCommand(SensorName);

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
        SetupMockDevice("TestDevice", CreateSensor(), []);
        var command = CreateCommand(SensorName, deviceName: "NoSuchDevice");

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
        SetupMockDevice("TestDevice", sensor: null, []);
        var command = CreateCommand("no_such_sensor");

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.Value.Status.ShouldBe(CommandStatusCode.NotFound);
        result.Value.Message.ShouldContain("no_such_sensor");
    }

    [Fact]
    public async Task HandleAsync_WhenNoCachedData_ShouldReturnNotFound()
    {
        // Arrange - sensor exists but the telemetry cache is empty
        SetupMockDevice("TestDevice", CreateSensor(), []);
        var command = CreateCommand(SensorName);

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.Value.Status.ShouldBe(CommandStatusCode.NotFound);
        result.Value.Message.ShouldContain("No data available");
    }

    [Fact]
    public async Task HandleAsync_WhenDeviceReadThrows_ShouldReturnHardwareError()
    {
        // Arrange
        SetupMockDevice("TestDevice", CreateSensor(), []);
        _mockDevice
            .ReadSensorTelemetryAsync(ResourceId, Arg.Any<CancellationToken>())
            .Returns<List<TelemetryMeasure>>(_ => throw new InvalidOperationException("bus fault"));

        var command = CreateCommand(SensorName);

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.Value.Status.ShouldBe(CommandStatusCode.HardwareError);
        result.Value.Message.ShouldContain("bus fault");
    }

    #endregion

    #region Helpers

    private static SensorReadCommand CreateCommand(string sensorName, string? deviceName = null) => new()
    {
        DeviceCmd = "sensor.read",
        Timeout = 10,
        Parameters = new SensorReadParameters
        {
            SensorName = sensorName,
            DeviceName = deviceName
        }
    };

    private static Sensor CreateSensor(string? unit = null) => new()
    {
        ResourceId = ResourceId,
        Name = SensorName,
        Report = new SensorReport { Enabled = true, Interval = 1000, Unit = unit }
    };

    private void SetupMockDevice(string deviceName, Sensor? sensor, List<TelemetryMeasure> measures)
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
            _mockDevice
                .ReadSensorTelemetryAsync(sensor.ResourceId, Arg.Any<CancellationToken>())
                .Returns(measures);
        }

        _context.DeviceRegistry.Register(_mockDevice);
    }

    #endregion
}
