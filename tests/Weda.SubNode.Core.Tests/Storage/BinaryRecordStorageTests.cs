using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Abstractions.Utilities;
using Weda.SubNode.Core.Storage;
using Xunit;

namespace Weda.SubNode.Core.Tests.Storage;

[Collection("StorageTests")]
public class BinaryRecordStorageTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly string _testDirectory;
    private readonly RecordingOptions _options;
    private readonly BinaryRecordStorage _storage;
    private readonly string _testSensorPrefix;

    public BinaryRecordStorageTests()
    {
        _loggerFactory = new NullLoggerFactory();
        // Use the constant storage directory resolved by PathHelper
        _testDirectory = PathHelper.ResolveStorageDirectory(RecordingOptions.StorageDirectory);
        // Use a unique prefix for sensor IDs to avoid conflicts between test runs
        _testSensorPrefix = $"test-{Guid.NewGuid():N}-";

        _options = new RecordingOptions();
        _storage = new BinaryRecordStorage(_loggerFactory.CreateLogger<BinaryRecordStorage>(), Options.Create(_options));
    }

    public void Dispose()
    {
        // Clean up only test sensor directories (those with our prefix)
        if (Directory.Exists(_testDirectory))
        {
            foreach (var dir in Directory.GetDirectories(_testDirectory))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.StartsWith(_testSensorPrefix))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }
    }

    private string TestSensorId(string name) => $"{_testSensorPrefix}{name}";

    #region WriteAsync Tests

    [Fact]
    public async Task WriteAsync_CreatesDirectoryAndFile()
    {
        // Arrange
        var sensorId = TestSensorId("sensor-1");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(timestamp, 42.5, SchemaType.Double);

        // Act
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, dataPoint);

        // Assert
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        Directory.Exists(sensorDir).ShouldBeTrue();

        var files = Directory.GetFiles(sensorDir, "*.bin");
        files.Length.ShouldBe(1);
    }

    [Fact]
    public async Task WriteAsync_WritesToDifferentSlots()
    {
        // Arrange - Use start of day for predictable slot calculation
        var sensorId = TestSensorId("sensor-1");
        var today = DateTimeOffset.UtcNow.Date;
        var startOfDayTimestamp = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();

        var dataPoint1 = new RecordingDataPoint(startOfDayTimestamp, 42.5, SchemaType.Double);        // slot 0
        var dataPoint2 = new RecordingDataPoint(startOfDayTimestamp + 1000, 43.5, SchemaType.Double); // slot 1

        // Act - Write to two different slots in the same file
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, dataPoint1);
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, dataPoint2);

        // Assert - File should exist with pre-allocated size (slot-based storage)
        var files = Directory.GetFiles(Path.Combine(_testDirectory, sensorId), "*.bin");
        files.Length.ShouldBe(1);

        // File size is pre-allocated, so both writes go to same file
        var fileInfo = new FileInfo(files[0]);
        fileInfo.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task WriteAsync_DifferentIntervals_CreatesDifferentFiles()
    {
        // Arrange
        var sensorId = TestSensorId("sensor-1");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(timestamp, 42.5, SchemaType.Double);

        // Act
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, dataPoint);
        await _storage.WriteAsync(sensorId, 5000, SchemaType.Double, dataPoint);

        // Assert
        var files = Directory.GetFiles(Path.Combine(_testDirectory, sensorId), "*.bin");
        files.Length.ShouldBe(2);
    }

    [Fact]
    public async Task WriteAsync_DifferentDays_CreatesDifferentFiles()
    {
        // Arrange
        var sensorId = TestSensorId("sensor-1");
        var today = DateTimeOffset.UtcNow.Date;
        var todayTimestamp = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var yesterdayTimestamp = todayTimestamp - 86400000;

        var todayPoint = new RecordingDataPoint(todayTimestamp, 42.5, SchemaType.Double);
        var yesterdayPoint = new RecordingDataPoint(yesterdayTimestamp, 43.5, SchemaType.Double);

        // Act
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, todayPoint);
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, yesterdayPoint);

        // Assert
        var files = Directory.GetFiles(Path.Combine(_testDirectory, sensorId), "*.bin");
        files.Length.ShouldBe(2);
    }

    [Fact]
    public async Task WriteAsync_FileNameFormat_IsCorrect()
    {
        // Arrange
        var sensorId = TestSensorId("sensor-1");
        var today = DateTimeOffset.UtcNow.Date;
        var timestamp = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(timestamp, 42.5, SchemaType.Double);

        // Act
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, dataPoint);

        // Assert - V2 file name format: {date}_{interval}_{schemaType}.bin
        var expectedFileName = $"{today:yyyy-MM-dd}_1000_double.bin";
        var files = Directory.GetFiles(Path.Combine(_testDirectory, sensorId), "*.bin");
        Path.GetFileName(files[0]).ShouldBe(expectedFileName);
    }

    #endregion

    #region CleanupAsync Tests

    [Fact]
    public async Task CleanupAsync_RemovesFilesOlderThanCutoff()
    {
        // Arrange - Write data using storage to ensure proper directory structure
        var sensorId = TestSensorId("sensor-1");
        var oldDate = DateTime.UtcNow.AddDays(-10);
        var oldTimestamp = new DateTimeOffset(oldDate, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(oldTimestamp, 42.5, SchemaType.Double);

        // Write old data through storage (creates proper directory structure)
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, dataPoint);

        // Verify file exists before cleanup
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        var oldFileName = $"{oldDate:yyyy-MM-dd}_1000_double.bin";
        var oldFilePath = Path.Combine(sensorDir, oldFileName);
        File.Exists(oldFilePath).ShouldBeTrue();

        // Act - Cleanup files older than 7 days
        await _storage.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert
        File.Exists(oldFilePath).ShouldBeFalse();
    }

    [Fact]
    public async Task CleanupAsync_KeepsFilesNewerThanCutoff()
    {
        // Arrange - Write data using storage to ensure proper directory structure
        var sensorId = TestSensorId("sensor-1");
        var recentDate = DateTime.UtcNow.AddDays(-3);
        var recentTimestamp = new DateTimeOffset(recentDate, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(recentTimestamp, 42.5, SchemaType.Double);

        // Write recent data through storage
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, dataPoint);

        // Verify file exists before cleanup
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        var recentFileName = $"{recentDate:yyyy-MM-dd}_1000_double.bin";
        var recentFilePath = Path.Combine(sensorDir, recentFileName);
        File.Exists(recentFilePath).ShouldBeTrue();

        // Act - Cleanup files older than 7 days
        await _storage.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert - Recent file should still exist
        File.Exists(recentFilePath).ShouldBeTrue();
    }

    [Fact]
    public async Task CleanupAsync_HandlesMultipleSensorDirectories()
    {
        // Arrange - Write data using storage to ensure proper directory structure
        var sensorId1 = TestSensorId("sensor-1");
        var sensorId2 = TestSensorId("sensor-2");
        var oldDate = DateTime.UtcNow.AddDays(-10);
        var oldTimestamp = new DateTimeOffset(oldDate, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(oldTimestamp, 42.5, SchemaType.Double);

        // Write old data for both sensors through storage
        await _storage.WriteAsync(sensorId1, 1000, SchemaType.Double, dataPoint);
        await _storage.WriteAsync(sensorId2, 1000, SchemaType.Double, dataPoint);

        // Verify files exist before cleanup
        var sensor1Dir = Path.Combine(_testDirectory, sensorId1);
        var sensor2Dir = Path.Combine(_testDirectory, sensorId2);
        var oldFileName = $"{oldDate:yyyy-MM-dd}_1000_double.bin";
        var oldFile1 = Path.Combine(sensor1Dir, oldFileName);
        var oldFile2 = Path.Combine(sensor2Dir, oldFileName);
        File.Exists(oldFile1).ShouldBeTrue();
        File.Exists(oldFile2).ShouldBeTrue();

        // Act - Cleanup files older than 7 days
        await _storage.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert
        File.Exists(oldFile1).ShouldBeFalse();
        File.Exists(oldFile2).ShouldBeFalse();
    }

    [Fact]
    public async Task CleanupAsync_EmptyDirectory_DoesNotThrow()
    {
        // Arrange - Storage directory exists but has no sensor subdirectories
        // The constant storage directory is used, but we ensure it's set up correctly

        // Act & Assert - Should not throw even if there are no files to clean
        await Should.NotThrowAsync(() => _storage.CleanupAsync(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task CleanupAsync_MixedOldAndNewFiles_OnlyRemovesOld()
    {
        // Arrange - Write data using storage to ensure proper directory structure
        var sensorId = TestSensorId("sensor-1");
        var oldDate = DateTime.UtcNow.AddDays(-10);
        var recentDate = DateTime.UtcNow.AddDays(-3);
        var oldTimestamp = new DateTimeOffset(oldDate, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var recentTimestamp = new DateTimeOffset(recentDate, TimeSpan.Zero).ToUnixTimeMilliseconds();

        // Write both old and recent data through storage
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, new RecordingDataPoint(oldTimestamp, 42.5, SchemaType.Double));
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, new RecordingDataPoint(recentTimestamp, 43.5, SchemaType.Double));

        // Verify files exist before cleanup
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        var oldFilePath = Path.Combine(sensorDir, $"{oldDate:yyyy-MM-dd}_1000_double.bin");
        var recentFilePath = Path.Combine(sensorDir, $"{recentDate:yyyy-MM-dd}_1000_double.bin");
        File.Exists(oldFilePath).ShouldBeTrue();
        File.Exists(recentFilePath).ShouldBeTrue();

        // Act - Cleanup files older than 7 days
        await _storage.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert
        File.Exists(oldFilePath).ShouldBeFalse();
        File.Exists(recentFilePath).ShouldBeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task WriteAsync_SpecialCharactersInSensorId_HandlesCorrectly()
    {
        // Arrange - Use a sensor ID that might be URL-encoded or have special chars
        var sensorId = TestSensorId("sensor_abc-123");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(timestamp, 42.5, SchemaType.Double);

        // Act
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, dataPoint);

        // Assert
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        Directory.Exists(sensorDir).ShouldBeTrue();
    }

    [Fact]
    public async Task WriteAsync_LargeValue_WritesCorrectly()
    {
        // Arrange
        var sensorId = TestSensorId("sensor-1");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(timestamp, double.MaxValue, SchemaType.Double);

        // Act
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, dataPoint);

        // Assert
        var files = Directory.GetFiles(Path.Combine(_testDirectory, sensorId), "*.bin");
        files.Length.ShouldBe(1);
        new FileInfo(files[0]).Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task WriteAsync_NegativeValue_WritesCorrectly()
    {
        // Arrange
        var sensorId = TestSensorId("sensor-1");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(timestamp, -273.15, SchemaType.Double);

        // Act
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, dataPoint);

        // Assert
        var files = Directory.GetFiles(Path.Combine(_testDirectory, sensorId), "*.bin");
        files.Length.ShouldBe(1);
    }

    #endregion
}
