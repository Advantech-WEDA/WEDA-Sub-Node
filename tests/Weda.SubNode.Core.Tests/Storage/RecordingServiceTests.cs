using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Core.Context;
using Weda.SubNode.Core.Storage;
using Xunit;

namespace Weda.SubNode.Core.Tests.Storage;

[Collection("StorageTests")]
public class RecordingServiceTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _testDirectory;
    private readonly RecordingOptions _options;
    private readonly BinaryRecordStorage _storage;
    private readonly DeviceRegistry _deviceRegistry;
    private readonly RecordingService _service;
    private readonly string _testSensorPrefix;

    public RecordingServiceTests()
    {
        _loggerFactory = new NullLoggerFactory();
        // Each fixture owns its own storage directory. Sharing one global directory let the
        // ring-buffer FIFO in one test delete files another test was asserting on.
        _testDirectory = Path.Combine(Path.GetTempPath(), $"recording-service-test-{Guid.NewGuid():N}");
        // Use a unique prefix for sensor IDs to avoid conflicts between test runs
        _testSensorPrefix = $"test-{Guid.NewGuid():N}-";

        _options = new RecordingOptions { StorageDirectory = _testDirectory };
        _storage = new BinaryRecordStorage(_loggerFactory.CreateLogger<BinaryRecordStorage>(), Options.Create(_options));
        _deviceRegistry = new DeviceRegistry();
        _service = new RecordingService(_loggerFactory.CreateLogger<RecordingService>(), _storage, _deviceRegistry, Options.Create(_options));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private string TestSensorId(string name) => $"{_testSensorPrefix}{name}";

    #region ShouldRecord Tests

    [Fact]
    public void ShouldRecord_FirstCallInSlot_ReturnsTrue()
    {
        // Arrange
        var sensorId = TestSensorId("sensor-1");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Act
        var result = _service.ShouldRecord(sensorId, 1000, timestamp);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ShouldRecord_SameSlot_ReturnsFalse()
    {
        // Arrange - Use start of day for predictable slot calculation
        var sensorId = TestSensorId("sensor-1");
        var today = DateTimeOffset.UtcNow.Date;
        var startOfDayTimestamp = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();

        // First call at slot 0 (startOfDay + 0ms)
        _service.ShouldRecord(sensorId, 1000, startOfDayTimestamp);

        // Act - Same slot 0 (startOfDay + 500ms, still in slot 0 range 0-999ms)
        var result = _service.ShouldRecord(sensorId, 1000, startOfDayTimestamp + 500);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ShouldRecord_NextSlot_ReturnsTrue()
    {
        // Arrange
        var sensorId = TestSensorId("sensor-1");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _service.ShouldRecord(sensorId, 1000, timestamp);

        // Act - Next slot (after 1000ms)
        var result = _service.ShouldRecord(sensorId, 1000, timestamp + 1500);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ShouldRecord_DifferentSensors_Independent()
    {
        // Arrange
        var sensorId1 = TestSensorId("sensor-1");
        var sensorId2 = TestSensorId("sensor-2");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _service.ShouldRecord(sensorId1, 1000, timestamp);

        // Act - Different sensor, same timestamp
        var result = _service.ShouldRecord(sensorId2, 1000, timestamp);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ShouldRecord_DifferentDays_ResetsSlot()
    {
        // Arrange
        var sensorId = TestSensorId("sensor-1");
        var today = DateTimeOffset.UtcNow.Date;
        var todayTimestamp = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var tomorrowTimestamp = todayTimestamp + 86400000; // +1 day in ms

        _service.ShouldRecord(sensorId, 1000, todayTimestamp);

        // Act - Same slot index but different day
        var result = _service.ShouldRecord(sensorId, 1000, tomorrowTimestamp);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ShouldRecord_LargerSlotIndex_ReturnsTrue()
    {
        // Arrange - Use start of day for predictable slot calculation
        var sensorId = TestSensorId("sensor-1");
        var today = DateTimeOffset.UtcNow.Date;
        var startOfDayTimestamp = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();

        // First call at slot 0
        _service.ShouldRecord(sensorId, 1000, startOfDayTimestamp);

        // Act - Move to slot 10 (10 seconds later)
        var result = _service.ShouldRecord(sensorId, 1000, startOfDayTimestamp + 10000);

        // Assert - Larger slot index should be allowed
        result.ShouldBeTrue();
    }

    [Fact]
    public void ShouldRecord_DifferentIntervals_Independent()
    {
        // Arrange - Use start of day for predictable slot calculation
        var sensorId = TestSensorId("sensor-1");
        var today = DateTimeOffset.UtcNow.Date;
        var startOfDayTimestamp = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();

        // Record with interval 1000ms at slot 0
        _service.ShouldRecord(sensorId, 1000, startOfDayTimestamp);

        // Act - Same sensor, same timestamp, but different interval (simulates dynamic change)
        var result = _service.ShouldRecord(sensorId, 5000, startOfDayTimestamp);

        // Assert - Different interval should have independent slot tracking
        result.ShouldBeTrue();
    }

    #endregion

    #region RecordAsync Tests

    [Fact]
    public async Task RecordAsync_FirstCall_WritesToStorage()
    {
        // Arrange
        var sensorId = TestSensorId("sensor-1");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Act
        await _service.RecordAsync(sensorId, 1000, SchemaType.Double, timestamp, 42.5);

        // Assert - File should be created in sensor directory
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        var files = Directory.GetFiles(sensorDir, "*.bin");
        files.Length.ShouldBe(1);
    }

    [Fact]
    public async Task RecordAsync_SameSlot_DoesNotWriteAgain()
    {
        // Arrange
        var sensorId = TestSensorId("sensor-1");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _service.RecordAsync(sensorId, 1000, SchemaType.Double, timestamp, 42.5);

        var sensorDir = Path.Combine(_testDirectory, sensorId);
        var files = Directory.GetFiles(sensorDir, "*.bin");
        var initialSize = new FileInfo(files[0]).Length;

        // Act - Same slot
        await _service.RecordAsync(sensorId, 1000, SchemaType.Double, timestamp + 500, 43.0);

        // Assert - File size should not change
        var finalSize = new FileInfo(files[0]).Length;
        finalSize.ShouldBe(initialSize);
    }

    [Fact]
    public async Task RecordAsync_NextSlot_WritesToDifferentPosition()
    {
        // Arrange - Use start of day for predictable slot calculation
        var sensorId = TestSensorId("sensor-1");
        var today = DateTimeOffset.UtcNow.Date;
        var startOfDayTimestamp = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();

        // Write to slot 0
        await _service.RecordAsync(sensorId, 1000, SchemaType.Double, startOfDayTimestamp, 42.5);

        // Act - Write to slot 1 (1 second later)
        await _service.RecordAsync(sensorId, 1000, SchemaType.Double, startOfDayTimestamp + 1000, 43.0);

        // Assert - Both values should be recorded (slot-based storage writes to fixed positions)
        // File exists and has data
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        var files = Directory.GetFiles(sensorDir, "*.bin");
        files.Length.ShouldBe(1);
        new FileInfo(files[0]).Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RecordAsync_MultipleSensors_CreatesSeparateDirectories()
    {
        // Arrange
        var sensorId1 = TestSensorId("sensor-1");
        var sensorId2 = TestSensorId("sensor-2");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Act
        await _service.RecordAsync(sensorId1, 1000, SchemaType.Double, timestamp, 42.5);
        await _service.RecordAsync(sensorId2, 1000, SchemaType.Double, timestamp, 43.5);

        // Assert - Both sensor directories should exist
        var sensor1Dir = Path.Combine(_testDirectory, sensorId1);
        var sensor2Dir = Path.Combine(_testDirectory, sensorId2);
        Directory.Exists(sensor1Dir).ShouldBeTrue();
        Directory.Exists(sensor2Dir).ShouldBeTrue();
    }

    #endregion

    #region CleanupAsync Tests

    [Fact]
    public async Task CleanupAsync_RemovesOldFiles()
    {
        // Arrange - Write old data using service to ensure proper directory structure
        var sensorId = TestSensorId("sensor-1");
        var oldDate = DateTime.UtcNow.AddDays(-10);
        var oldTimestamp = new DateTimeOffset(oldDate, TimeSpan.Zero).ToUnixTimeMilliseconds();

        // Write old data through service (uses storage internally)
        await _service.RecordAsync(sensorId, 1000, SchemaType.Double, oldTimestamp, 42.5);

        // Verify file exists before cleanup
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        var oldFileName = $"{oldDate:yyyy-MM-dd}_1000_double.bin";
        var oldFilePath = Path.Combine(sensorDir, oldFileName);
        File.Exists(oldFilePath).ShouldBeTrue();

        // Act - Cleanup files older than 7 days
        await _service.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert
        File.Exists(oldFilePath).ShouldBeFalse();
    }

    [Fact]
    public async Task CleanupAsync_KeepsRecentFiles()
    {
        // Arrange - Write recent data using service to ensure proper directory structure
        var sensorId = TestSensorId("sensor-1");
        var recentDate = DateTime.UtcNow.AddDays(-3);
        var recentTimestamp = new DateTimeOffset(recentDate, TimeSpan.Zero).ToUnixTimeMilliseconds();

        // Write recent data through service
        await _service.RecordAsync(sensorId, 1000, SchemaType.Double, recentTimestamp, 42.5);

        // Verify file exists before cleanup
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        var recentFileName = $"{recentDate:yyyy-MM-dd}_1000_double.bin";
        var recentFilePath = Path.Combine(sensorDir, recentFileName);
        File.Exists(recentFilePath).ShouldBeTrue();

        // Act - Cleanup files older than 7 days
        await _service.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert - Recent file should still exist
        File.Exists(recentFilePath).ShouldBeTrue();
    }

    #endregion

    #region GetRecordingsAsync Retention Tests

    [Fact]
    public async Task GetRecordingsAsync_SensorDirectoryWithoutFiles_ReturnsEmptyResultNotError()
    {
        // Arrange - A sensor known to storage whose recordings have all aged out
        var sensorId = TestSensorId("sensor-aged-out");
        Directory.CreateDirectory(Path.Combine(_testDirectory, sensorId));

        // Act
        var result = await _service.GetRecordingsAsync(sensorId, DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow);

        // Assert - Having no data is an empty result, never a storage failure. Reporting this
        // as an error is what surfaced phantom "sensorError" data gaps in batch reports.
        result.IsError.ShouldBeFalse();
        result.Value.SensorId.ShouldBe(sensorId);
        result.Value.Measures.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRecordingsAsync_UnknownSensor_ReturnsNotFound()
    {
        // Arrange - No directory was ever created for this sensor
        var sensorId = TestSensorId("never-recorded");

        // Act
        var result = await _service.GetRecordingsAsync(sensorId, DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
        result.FirstError.Code.ShouldBe("Recording.SensorNotFound");
    }

    [Fact]
    public async Task GetRecordingsAsync_AfterCleanupRemovesLastFile_ReturnsNotFound()
    {
        // Arrange - Cleanup removes both the expired file and the now-empty directory
        var sensorId = TestSensorId("sensor-cleaned");
        var oldTimestamp = new DateTimeOffset(DateTime.UtcNow.AddDays(-10), TimeSpan.Zero).ToUnixTimeMilliseconds();
        await _service.RecordAsync(sensorId, 1000, SchemaType.Double, oldTimestamp, 42.5);

        await _service.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Act
        var result = await _service.GetRecordingsAsync(sensorId, DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow);

        // Assert - The sensor is gone from storage entirely, so it is genuinely not found
        // and is no longer enumerated at all.
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);

        var sensorIds = await _service.GetSensorIdsAsync();
        sensorIds.Value.ShouldNotContain(sensorId);
    }

    [Fact]
    public async Task DeleteSensorAsync_SensorDirectoryWithoutFiles_DeletesDirectory()
    {
        // Arrange - An empty sensor directory must still be removable
        var sensorId = TestSensorId("sensor-empty");
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        Directory.CreateDirectory(sensorDir);

        // Act
        var result = await _service.DeleteSensorAsync(sensorId);

        // Assert
        result.IsError.ShouldBeFalse();
        Directory.Exists(sensorDir).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteSensorAsync_UnknownSensor_ReturnsNotFound()
    {
        // Act
        var result = await _service.DeleteSensorAsync(TestSensorId("never-recorded"));

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.NotFound);
    }

    #endregion

    #region GetRecordingsAsync Gap Tests

    private static long StartOfUtcDay(int daysAgo)
        => new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-daysAgo), TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static object SampleValue(SchemaType schemaType, int n) => schemaType switch
    {
        SchemaType.Integer => n,
        SchemaType.Long => (long)n,
        SchemaType.Boolean => n % 2 == 0,
        _ => (double)n
    };

    private Task WriteSlotAsync(string sensorId, int interval, SchemaType schemaType, long timestamp, object value)
        => _storage.WriteAsync(sensorId, interval, schemaType, new RecordingDataPoint(timestamp, value, schemaType));

    [Theory]
    [InlineData(SchemaType.Double)]
    [InlineData(SchemaType.Integer)]
    [InlineData(SchemaType.Long)]
    [InlineData(SchemaType.Boolean)]
    public async Task GetRecordingsAsync_MissedRead_KeepsLaterValuesOnTheirSlots(SchemaType schemaType)
    {
        // Arrange - Reads land in slots 2, 3 and 5; the read for slot 4 failed and left it empty.
        // Slots 0-1 and everything after 5 are empty too.
        const int interval = 5000;
        var sensorId = TestSensorId($"sensor-gap-{schemaType}");
        var day = StartOfUtcDay(daysAgo: 1);
        foreach (var slot in new[] { 2, 3, 5 })
        {
            await WriteSlotAsync(sensorId, interval, schemaType, day + slot * interval, SampleValue(schemaType, slot));
        }

        // Act
        var result = await _service.GetRecordingsAsync(
            sensorId,
            DateTimeOffset.FromUnixTimeMilliseconds(day),
            DateTimeOffset.FromUnixTimeMilliseconds(day).AddHours(1));

        // Assert - Consumers rebuild timestamps as StartTimeStamp + index * Interval, so the missed
        // read must keep its place as null. Dropping it moved slot 5's value onto slot 4.
        // Leading and trailing empty slots are not padded.
        result.IsError.ShouldBeFalse();
        var measure = result.Value.Measures.ShouldHaveSingleItem();
        measure.Interval.ShouldBe(interval);
        measure.SchemaType.ShouldBe(schemaType);
        measure.StartTimeStamp.ShouldBe(day + 2 * interval);
        measure.Values.ShouldBe([
            SampleValue(schemaType, 2),
            SampleValue(schemaType, 3),
            null,
            SampleValue(schemaType, 5)]);
    }

    [Fact]
    public async Task GetRecordingsAsync_GapOfADayOrMore_StartsNewMeasure()
    {
        // Arrange - One reading three days ago, the next one yesterday: nothing was recorded in
        // between, and the day in the middle has no recording file at all.
        const int interval = 60000;
        var sensorId = TestSensorId("sensor-outage");
        var firstDay = StartOfUtcDay(daysAgo: 3);
        var lastDay = StartOfUtcDay(daysAgo: 1);
        await WriteSlotAsync(sensorId, interval, SchemaType.Double, firstDay, 1.0);
        await WriteSlotAsync(sensorId, interval, SchemaType.Double, lastDay, 2.0);

        // Act
        var result = await _service.GetRecordingsAsync(
            sensorId,
            DateTimeOffset.FromUnixTimeMilliseconds(firstDay),
            DateTimeOffset.FromUnixTimeMilliseconds(lastDay).AddHours(1));

        // Assert - Each side of the outage is its own measure, starting on its own timestamp,
        // rather than two days of nulls between them.
        result.IsError.ShouldBeFalse();
        result.Value.Measures.Count.ShouldBe(2);
        result.Value.Measures[0].StartTimeStamp.ShouldBe(firstDay);
        result.Value.Measures[0].Values.ShouldBe([1.0]);
        result.Value.Measures[1].StartTimeStamp.ShouldBe(lastDay);
        result.Value.Measures[1].Values.ShouldBe([2.0]);
    }

    [Fact]
    public async Task GetRecordingsAsync_IntervalNotDividingADay_StartsNewMeasureAtMidnight()
    {
        // Arrange - 7 s does not divide a day, so each day's slot grid restarts at midnight:
        // the last slot of one day and the first slot of the next are 13 s apart, not 7 s.
        const int interval = 7000;
        var sensorId = TestSensorId("sensor-off-grid");
        var firstDay = StartOfUtcDay(daysAgo: 2);
        var nextDay = StartOfUtcDay(daysAgo: 1);
        var lastSlotOfFirstDay = firstDay + (86_400_000 / interval - 1) * interval;
        await WriteSlotAsync(sensorId, interval, SchemaType.Double, lastSlotOfFirstDay, 1.0);
        await WriteSlotAsync(sensorId, interval, SchemaType.Double, nextDay, 2.0);

        // Act
        var result = await _service.GetRecordingsAsync(
            sensorId,
            DateTimeOffset.FromUnixTimeMilliseconds(firstDay),
            DateTimeOffset.FromUnixTimeMilliseconds(nextDay).AddHours(1));

        // Assert - No single StartTimeStamp + index * Interval can reach both, so they are split.
        result.IsError.ShouldBeFalse();
        result.Value.Measures.Count.ShouldBe(2);
        result.Value.Measures[0].StartTimeStamp.ShouldBe(lastSlotOfFirstDay);
        result.Value.Measures[1].StartTimeStamp.ShouldBe(nextDay);
    }

    #endregion
}
