using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Commands.Handlers.ReportData;
using Weda.SubNode.Core.Commands.Handlers.ReportData.Models;
using Weda.SubNode.TestBase;
using Xunit;

namespace Weda.SubNode.Core.Tests.Commands.Handlers;

/// <summary>
/// Unit tests for ReportDataCommandHandler.
/// Tests the report.data command that retrieves latest cached telemetry data.
/// </summary>
public class ReportDataCommandHandlerTests : IDisposable
{
    private readonly MockApplicationContext _context;
    private readonly ReportDataCommandHandler _handler;
    private readonly IDevice _mockDevice;

    public ReportDataCommandHandlerTests()
    {
        _context = new MockApplicationContext();
        _handler = new ReportDataCommandHandler();
        _mockDevice = Substitute.For<IDevice>();

        // Setup SubNodeInfo with valid ID (Id is derived from DeviceId)
        _context.SubNodeInfo = new SubNodeInfo
        {
            Name = "TestSubNode",
            DeviceId = "test-subnode-001",  // Id property returns this value
            Manufacturer = "Test",
            Model = "MockSubNode",
            SwVersion = "1.0.0"
        };
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Success Scenarios

    [Fact]
    public async Task HandleAsync_WithPrimitiveData_ShouldSendTelemetryAndReturnSuccess()
    {
        // Arrange
        var resourceId = "00000000-0000-0000-0000-00000f782c";
        var sensorShortId = "f782c";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var sensor = new Sensor { ResourceId = resourceId, Name = "Temperature" };
        var deviceConfig = CreateDeviceConfiguration("TestDevice", sensor);

        SetupMockDevice(deviceConfig, resourceId, new List<TelemetryMeasure>
        {
            new() { ResourceId = resourceId, Value = 25.5, Timestamp = timestamp }
        });

        _context.MockCloudService
            .SendTelemetryAsync(Arg.Any<string>(), Arg.Any<TelemetryData>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var command = CreateCommand(sensorShortId, timestamp);

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(ReportDataStatusCode.Success);
        result.Value.ResultData!.DataTransferred.ShouldBeTrue();

        await _context.MockCloudService.Received(1)
            .SendTelemetryAsync(
                Arg.Is<string>(id => id == "test-subnode-001"),
                Arg.Is<TelemetryData>(data => data.Measures.Count == 1),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithChunkedData_ShouldSendAllChunks()
    {
        // Arrange
        var resourceId = "00000000-0000-0000-0000-00000f782c";
        var sensorShortId = "f782c";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var transferId = Guid.NewGuid().ToString();

        var sensor = new Sensor { ResourceId = resourceId, Name = "Image" };
        var deviceConfig = CreateDeviceConfiguration("TestDevice", sensor);

        // Simulate chunked data (3 chunks)
        var chunkedMeasures = new List<TelemetryMeasure>
        {
            CreateChunkedMeasure(resourceId, timestamp, transferId, 0, 3, "chunk0data"),
            CreateChunkedMeasure(resourceId, timestamp, transferId, 1, 3, "chunk1data"),
            CreateChunkedMeasure(resourceId, timestamp, transferId, 2, 3, "chunk2data")
        };

        SetupMockDevice(deviceConfig, resourceId, chunkedMeasures);

        _context.MockCloudService
            .SendTelemetryAsync(Arg.Any<string>(), Arg.Any<TelemetryData>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var command = CreateCommand(sensorShortId, timestamp);

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(ReportDataStatusCode.Success);

        await _context.MockCloudService.Received(1)
            .SendTelemetryAsync(
                Arg.Any<string>(),
                Arg.Is<TelemetryData>(data => data.Measures.Count == 3),
                Arg.Any<CancellationToken>());
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task HandleAsync_WhenSubNodeNotRegistered_ShouldReturnPermissionDenied()
    {
        // Arrange - DeviceId is null so Id will be null
        _context.SubNodeInfo = new SubNodeInfo { Name = "TestSubNode", DeviceId = null };
        var command = CreateCommand("f782c", 1234567890);

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.IsError.ShouldBeFalse(); // Handler returns result, not error
        result.Value.Status.ShouldBe(ReportDataStatusCode.PermissionDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenSensorNotFound_ShouldReturnNoDataAvailable()
    {
        // Arrange - No devices registered
        var command = CreateCommand("nonexistent", 1234567890);

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(ReportDataStatusCode.NoDataAvailable);
        result.Value.Message.ShouldContain("Sensor not found");
    }

    [Fact]
    public async Task HandleAsync_WhenNoCachedData_ShouldReturnNoDataAvailable()
    {
        // Arrange
        var resourceId = "00000000-0000-0000-0000-00000f782c";
        var sensorShortId = "f782c";

        var sensor = new Sensor { ResourceId = resourceId, Name = "Temperature" };
        var deviceConfig = CreateDeviceConfiguration("TestDevice", sensor);

        // Return empty list (no cached data)
        SetupMockDevice(deviceConfig, resourceId, new List<TelemetryMeasure>());

        var command = CreateCommand(sensorShortId, 1234567890);

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(ReportDataStatusCode.NoDataAvailable);
        result.Value.Message.ShouldContain("No data available");
    }

    [Fact]
    public async Task HandleAsync_WhenSendTelemetryFails_ShouldReturnGenericError()
    {
        // Arrange
        var resourceId = "00000000-0000-0000-0000-00000f782c";
        var sensorShortId = "f782c";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var sensor = new Sensor { ResourceId = resourceId, Name = "Temperature" };
        var deviceConfig = CreateDeviceConfiguration("TestDevice", sensor);

        SetupMockDevice(deviceConfig, resourceId, new List<TelemetryMeasure>
        {
            new() { ResourceId = resourceId, Value = 25.5, Timestamp = timestamp }
        });

        _context.MockCloudService
            .SendTelemetryAsync(Arg.Any<string>(), Arg.Any<TelemetryData>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var command = CreateCommand(sensorShortId, timestamp);

        // Act
        var result = await _handler.HandleAsync(command, _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(ReportDataStatusCode.GenericError);
        result.Value.Message.ShouldContain("Failed to send");
    }

    [Fact]
    public async Task HandleAsync_WhenCancelled_ShouldReturnTimeout()
    {
        // Arrange
        var resourceId = "00000000-0000-0000-0000-00000f782c";
        var sensorShortId = "f782c";

        var sensor = new Sensor { ResourceId = resourceId, Name = "Temperature" };
        var deviceConfig = CreateDeviceConfiguration("TestDevice", sensor);

        _mockDevice.Configuration.Returns(deviceConfig);
        _mockDevice
            .ReadSensorTelemetryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<List<TelemetryMeasure>>(x => throw new OperationCanceledException());

        _context.DeviceRegistry.Register(_mockDevice);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = CreateCommand(sensorShortId, 1234567890);

        // Act
        var result = await _handler.HandleAsync(command, _context, cts.Token);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(ReportDataStatusCode.Timeout);
    }

    #endregion

    #region Helper Methods

    private static DeviceConfiguration CreateDeviceConfiguration(string deviceName, params Sensor[] sensors)
    {
        return new DeviceConfiguration
        {
            DeviceName = deviceName,
            SubNodeInfo = new SubNodeInfo
            {
                Name = "TestSubNode",
                SubNodeType = SubNodeType.CustomDevice,
                Manufacturer = "Test",
                Model = "TestModel"
            },
            Sensors = sensors.ToList()
        };
    }

    private void SetupMockDevice(DeviceConfiguration config, string resourceId, List<TelemetryMeasure> measures)
    {
        _mockDevice.Configuration.Returns(config);
        _mockDevice
            .ReadSensorTelemetryAsync(resourceId, Arg.Any<CancellationToken>())
            .Returns(measures);

        _context.DeviceRegistry.Register(_mockDevice);
    }

    private static TelemetryMeasure CreateChunkedMeasure(
        string resourceId,
        long timestamp,
        string transferId,
        int chunkIndex,
        int totalChunks,
        string data)
    {
        return new TelemetryMeasure
        {
            ResourceId = resourceId,
            Value = data,
            Timestamp = timestamp,
            Metadata = new Dictionary<string, object>
            {
                ["transferId"] = transferId,
                ["chunkIndex"] = chunkIndex,
                ["totalChunks"] = totalChunks
            }
        };
    }

    private static ReportDataCommand CreateCommand(string sensorShortId, long timestamp, string? transferId = null)
    {
        return new ReportDataCommand
        {
            Parameters = new ReportDataParameters
            {
                SensorShortResourceId = sensorShortId,
                ResourceTimestamp = timestamp,
                TransferId = transferId
            }
        };
    }

    #endregion
}
