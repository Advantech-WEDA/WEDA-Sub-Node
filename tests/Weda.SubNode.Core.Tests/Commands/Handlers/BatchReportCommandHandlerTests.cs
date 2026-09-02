using ErrorOr;

using NSubstitute;
using Shouldly;

using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Storage;

using RecordingErrors = Weda.SubNode.Abstractions.Storage.Errors.Recording;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Commands.Handlers.BatchReport;
using Weda.SubNode.Core.Commands.Handlers.BatchReport.Models;
using Weda.SubNode.TestBase;

using Xunit;

namespace Weda.SubNode.Core.Tests.Commands.Handlers;

/// <summary>
/// Unit tests for BatchReportCommandHandler.
/// Covers how per-sensor read outcomes map to the report status and to data gaps.
/// </summary>
public class BatchReportCommandHandlerTests : IDisposable
{
    private const string SubNodeId = "test-subnode-001";
    private const string RespTopic = "test/resp";

    private readonly MockApplicationContext _context;
    private readonly BatchReportCommandHandler _handler;
    private readonly IRecordingService _recordingService;

    public BatchReportCommandHandlerTests()
    {
        _context = new MockApplicationContext();
        _handler = new BatchReportCommandHandler();
        _recordingService = Substitute.For<IRecordingService>();

        _context.RecordingService = _recordingService;
        _context.RecordingOptions = new RecordingOptions();
        _context.SubNodeInfo = new SubNodeInfo
        {
            Name = "TestSubNode",
            DeviceId = SubNodeId,
            Manufacturer = "Test",
            Model = "MockSubNode",
            SwVersion = "1.0.0"
        };

        _context.MockCloudService
            .SendBatchTelemetryAsync(Arg.Any<string>(), Arg.Any<BatchTelemetrySendMessage>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _context.MockCloudService
            .SendCommandResponseAsync(Arg.Any<string>(), Arg.Any<CommandResponse>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    public void Dispose() => _context.Dispose();

    #region Data Gap Reporting

    [Fact]
    public async Task HandleAsync_SensorWithNoStoredRecordings_ReportsSuccessWithoutDataGaps()
    {
        // Arrange - "sensor-empty" was enumerated but its recordings aged out of retention, so
        // the read reports NotFound. That is an absence of data, not a storage failure.
        SetupSensors("sensor-with-data", "sensor-empty");
        SetupRecordings("sensor-with-data", Measure(interval: 1000, values: [1.0, 2.0, 3.0]));
        SetupRecordingError("sensor-empty", RecordingErrors.SensorNotFound("sensor-empty"));

        // Act
        var result = await _handler.HandleAsync(CreateCommand(), _context);

        // Assert - No phantom gap, and the report is not downgraded to PartialSuccess.
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(CommandStatusCode.Success);
        result.Value.ResultData!.DataGaps.ShouldBeNull();
        result.Value.ResultData.TotalSamples.ShouldBe(3);
        result.Value.ResultData.Sensors.ShouldBe(["sensor-with-data", "sensor-empty"], ignoreOrder: true);
    }

    [Fact]
    public async Task HandleAsync_AllSensorsEmpty_ReportsNotFoundWithoutDataGaps()
    {
        // Arrange - Every sensor directory has been emptied by retention
        SetupSensors("sensor-a", "sensor-b");
        SetupRecordingError("sensor-a", RecordingErrors.SensorNotFound("sensor-a"));
        SetupRecordingError("sensor-b", RecordingErrors.SensorNotFound("sensor-b"));

        // Act
        var result = await _handler.HandleAsync(CreateCommand(), _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(CommandStatusCode.NotFound);
        result.Value.ResultData!.DataGaps.ShouldBeNull();
        result.Value.ResultData.TotalSamples.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_SensorReadFails_ReportsPartialSuccessWithPopulatedDataGap()
    {
        // Arrange - A genuine storage failure on one sensor
        var start = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeMilliseconds();
        var end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Storage addresses sensors by ShortId - the last 5 characters of the ResourceId.
        const string brokenSensorId = "b0a4d";

        SetupSensors("f782c", brokenSensorId);
        SetupRecordings("f782c", Measure(interval: 1000, values: [1.0, 2.0]));
        SetupRecordingError(brokenSensorId, RecordingErrors.StorageError(new IOException("disk read failure")));
        SetupSensorInterval(brokenSensorId, resourceId: $"00000000-0000-0000-0000-00000{brokenSensorId}", intervalMs: 60_000);

        // Act
        var result = await _handler.HandleAsync(CreateCommand(start, end), _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(CommandStatusCode.PartialSuccess);

        var gap = result.Value.ResultData!.DataGaps.ShouldHaveSingleItem();
        gap.SensorId.ShouldBe(brokenSensorId);
        gap.Reason.ShouldBe("sensorError");

        // The gap must describe the window it covers rather than being an empty marker.
        gap.StartTime.ShouldBe(result.Value.ResultData.TimeRange!.StartTime);
        gap.EndTime.ShouldBe(result.Value.ResultData.TimeRange.EndTime);
        gap.StartTime.ShouldNotBeNullOrEmpty();
        gap.EndTime.ShouldNotBeNullOrEmpty();

        // 2 hours at a 60s report interval is ~120 samples.
        gap.MissingSamples.ShouldBe(121);
    }

    [Fact]
    public async Task HandleAsync_SensorReadFailsWithUnknownInterval_EstimatesMissingSamplesAtOneSecond()
    {
        // Arrange - A failing sensor that is not present in any device configuration
        var start = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds();
        var end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        SetupSensors("c93f1");
        SetupRecordingError("c93f1", RecordingErrors.StorageError(new IOException("disk read failure")));

        // Act
        var result = await _handler.HandleAsync(CreateCommand(start, end), _context);

        // Assert - Falls back to the 1s default rather than reporting a meaningless 0.
        var gap = result.Value.ResultData!.DataGaps.ShouldHaveSingleItem();
        gap.MissingSamples.ShouldBe(601);
    }

    #endregion

    #region Success Scenarios

    [Fact]
    public async Task HandleAsync_AllSensorsReadable_ReportsSuccessAndSendsBatches()
    {
        // Arrange
        SetupSensors("sensor-a", "sensor-b");
        SetupRecordings("sensor-a", Measure(interval: 1000, values: [1.0, 2.0]));
        SetupRecordings("sensor-b", Measure(interval: 1000, values: [3.0]));

        // Act
        var result = await _handler.HandleAsync(CreateCommand(), _context);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Status.ShouldBe(CommandStatusCode.Success);
        result.Value.ResultData!.TotalSamples.ShouldBe(3);
        result.Value.ResultData.DataGaps.ShouldBeNull();

        await _context.MockCloudService.Received()
            .SendBatchTelemetryAsync(SubNodeId, Arg.Any<BatchTelemetrySendMessage>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Helper Methods

    private void SetupSensors(params string[] sensorIds) =>
        _recordingService.GetSensorIdsAsync(Arg.Any<CancellationToken>())
            .Returns(sensorIds.ToList());

    private void SetupRecordings(string sensorId, params RecordingMeasureResult[] measures) =>
        _recordingService
            .GetRecordingsAsync(sensorId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new RecordingResult(sensorId, measures));

    private void SetupRecordingError(string sensorId, Error error) =>
        _recordingService
            .GetRecordingsAsync(sensorId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(error);

    /// <summary>
    /// Registers a device configuration so the handler can resolve the sensor's report interval.
    /// </summary>
    /// <remarks>
    /// The handler keys its interval lookup on ShortId, which is the last 5 characters of the
    /// ResourceId, so <paramref name="resourceId"/> must end with <paramref name="sensorId"/>'s
    /// storage identifier for the lookup to hit.
    /// </remarks>
    private void SetupSensorInterval(string sensorId, string resourceId, double intervalMs)
    {
        var sensor = new Sensor
        {
            ResourceId = resourceId,
            Name = sensorId,
            Report = new SensorReport { Interval = (int)intervalMs }
        };

        _context.DeviceConfigsInternal["TestDevice"] = new DeviceConfiguration
        {
            DeviceName = "TestDevice",
            SubNodeInfo = new SubNodeInfo
            {
                Name = "TestSubNode",
                SubNodeType = SubNodeType.CustomDevice,
                Manufacturer = "Test",
                Model = "TestModel"
            },
            Sensors = [sensor]
        };
    }

    private static RecordingMeasureResult Measure(int interval, IReadOnlyList<object> values) =>
        new(interval, DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds(), SchemaType.Double, values);

    private static BatchReportCommand CreateCommand(long? startTime = null, long? endTime = null)
    {
        var end = endTime ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var start = startTime ?? end - (10 * 60 * 1000);

        return new BatchReportCommand
        {
            DeviceCmd = "report.historical",
            RespTopic = RespTopic,
            SeqId = 1,
            Timeout = 30,
            Parameters = new BatchReportParameters
            {
                TimeRange = new TimeRange(start, end)
            }
        };
    }

    #endregion
}
