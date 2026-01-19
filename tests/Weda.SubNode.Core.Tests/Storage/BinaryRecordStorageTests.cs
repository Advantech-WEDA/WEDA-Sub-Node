using Microsoft.Extensions.Options;
using Shouldly;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Core.Storage;
using Xunit;

namespace Weda.SubNode.Core.Tests.Storage;

public class BinaryRecordStorageTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly RecordingOptions _options;
    private readonly BinaryRecordStorage _storage;

    public BinaryRecordStorageTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"binary-storage-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        _options = new RecordingOptions { StorageDirectory = _testDirectory };
        _storage = new BinaryRecordStorage(Options.Create(_options));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    #region WriteAsync Tests

    [Fact]
    public async Task WriteAsync_CreatesDirectoryAndFile()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(timestamp, 42.5);

        // Act
        await _storage.WriteAsync("sensor-1", 1000, dataPoint);

        // Assert
        var sensorDir = Path.Combine(_testDirectory, "sensor-1");
        Directory.Exists(sensorDir).ShouldBeTrue();

        var files = Directory.GetFiles(sensorDir, "*.bin");
        files.Length.ShouldBe(1);
    }

    [Fact]
    public async Task WriteAsync_WritesToDifferentSlots()
    {
        // Arrange - Use start of day for predictable slot calculation
        var today = DateTimeOffset.UtcNow.Date;
        var startOfDayTimestamp = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();

        var dataPoint1 = new RecordingDataPoint(startOfDayTimestamp, 42.5);        // slot 0
        var dataPoint2 = new RecordingDataPoint(startOfDayTimestamp + 1000, 43.5); // slot 1

        // Act - Write to two different slots in the same file
        await _storage.WriteAsync("sensor-1", 1000, dataPoint1);
        await _storage.WriteAsync("sensor-1", 1000, dataPoint2);

        // Assert - File should exist with pre-allocated size (slot-based storage)
        var files = Directory.GetFiles(Path.Combine(_testDirectory, "sensor-1"), "*.bin");
        files.Length.ShouldBe(1);

        // File size is pre-allocated, so both writes go to same file
        var fileInfo = new FileInfo(files[0]);
        fileInfo.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task WriteAsync_DifferentIntervals_CreatesDifferentFiles()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(timestamp, 42.5);

        // Act
        await _storage.WriteAsync("sensor-1", 1000, dataPoint);
        await _storage.WriteAsync("sensor-1", 5000, dataPoint);

        // Assert
        var files = Directory.GetFiles(Path.Combine(_testDirectory, "sensor-1"), "*.bin");
        files.Length.ShouldBe(2);
    }

    [Fact]
    public async Task WriteAsync_DifferentDays_CreatesDifferentFiles()
    {
        // Arrange
        var today = DateTimeOffset.UtcNow.Date;
        var todayTimestamp = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var yesterdayTimestamp = todayTimestamp - 86400000;

        var todayPoint = new RecordingDataPoint(todayTimestamp, 42.5);
        var yesterdayPoint = new RecordingDataPoint(yesterdayTimestamp, 43.5);

        // Act
        await _storage.WriteAsync("sensor-1", 1000, todayPoint);
        await _storage.WriteAsync("sensor-1", 1000, yesterdayPoint);

        // Assert
        var files = Directory.GetFiles(Path.Combine(_testDirectory, "sensor-1"), "*.bin");
        files.Length.ShouldBe(2);
    }

    [Fact]
    public async Task WriteAsync_FileNameFormat_IsCorrect()
    {
        // Arrange
        var today = DateTimeOffset.UtcNow.Date;
        var timestamp = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(timestamp, 42.5);

        // Act
        await _storage.WriteAsync("sensor-1", 1000, dataPoint);

        // Assert
        var expectedFileName = $"{today:yyyy-MM-dd}_1000.bin";
        var files = Directory.GetFiles(Path.Combine(_testDirectory, "sensor-1"), "*.bin");
        Path.GetFileName(files[0]).ShouldBe(expectedFileName);
    }

    #endregion

    #region CleanupAsync Tests

    [Fact]
    public async Task CleanupAsync_RemovesFilesOlderThanCutoff()
    {
        // Arrange
        var sensorDir = Path.Combine(_testDirectory, "sensor-1");
        Directory.CreateDirectory(sensorDir);

        var oldDate = DateTime.UtcNow.AddDays(-10);
        var oldFileName = $"{oldDate:yyyy-MM-dd}_1000.bin";
        var oldFilePath = Path.Combine(sensorDir, oldFileName);
        await File.WriteAllBytesAsync(oldFilePath, new byte[] { 1, 2, 3 });

        // Act
        await _storage.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert
        File.Exists(oldFilePath).ShouldBeFalse();
    }

    [Fact]
    public async Task CleanupAsync_KeepsFilesNewerThanCutoff()
    {
        // Arrange
        var sensorDir = Path.Combine(_testDirectory, "sensor-1");
        Directory.CreateDirectory(sensorDir);

        var recentDate = DateTime.UtcNow.AddDays(-3);
        var recentFileName = $"{recentDate:yyyy-MM-dd}_1000.bin";
        var recentFilePath = Path.Combine(sensorDir, recentFileName);
        await File.WriteAllBytesAsync(recentFilePath, new byte[] { 1, 2, 3 });

        // Act
        await _storage.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert
        File.Exists(recentFilePath).ShouldBeTrue();
    }

    [Fact]
    public async Task CleanupAsync_HandlesMultipleSensorDirectories()
    {
        // Arrange
        var oldDate = DateTime.UtcNow.AddDays(-10);
        var oldFileName = $"{oldDate:yyyy-MM-dd}_1000.bin";

        var sensor1Dir = Path.Combine(_testDirectory, "sensor-1");
        var sensor2Dir = Path.Combine(_testDirectory, "sensor-2");
        Directory.CreateDirectory(sensor1Dir);
        Directory.CreateDirectory(sensor2Dir);

        var oldFile1 = Path.Combine(sensor1Dir, oldFileName);
        var oldFile2 = Path.Combine(sensor2Dir, oldFileName);
        await File.WriteAllBytesAsync(oldFile1, new byte[] { 1 });
        await File.WriteAllBytesAsync(oldFile2, new byte[] { 2 });

        // Act
        await _storage.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert
        File.Exists(oldFile1).ShouldBeFalse();
        File.Exists(oldFile2).ShouldBeFalse();
    }

    [Fact]
    public async Task CleanupAsync_DirectoryNotExists_DoesNotThrow()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), $"non-existent-{Guid.NewGuid():N}");
        var options = new RecordingOptions { StorageDirectory = nonExistentDir };
        var storage = new BinaryRecordStorage(Options.Create(options));

        // Act & Assert - Should not throw
        await Should.NotThrowAsync(() => storage.CleanupAsync(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task CleanupAsync_MixedOldAndNewFiles_OnlyRemovesOld()
    {
        // Arrange
        var sensorDir = Path.Combine(_testDirectory, "sensor-1");
        Directory.CreateDirectory(sensorDir);

        var oldDate = DateTime.UtcNow.AddDays(-10);
        var recentDate = DateTime.UtcNow.AddDays(-3);

        var oldFilePath = Path.Combine(sensorDir, $"{oldDate:yyyy-MM-dd}_1000.bin");
        var recentFilePath = Path.Combine(sensorDir, $"{recentDate:yyyy-MM-dd}_1000.bin");

        await File.WriteAllBytesAsync(oldFilePath, new byte[] { 1 });
        await File.WriteAllBytesAsync(recentFilePath, new byte[] { 2 });

        // Act
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
        var sensorId = "sensor_abc-123";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(timestamp, 42.5);

        // Act
        await _storage.WriteAsync(sensorId, 1000, dataPoint);

        // Assert
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        Directory.Exists(sensorDir).ShouldBeTrue();
    }

    [Fact]
    public async Task WriteAsync_LargeValue_WritesCorrectly()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(timestamp, double.MaxValue);

        // Act
        await _storage.WriteAsync("sensor-1", 1000, dataPoint);

        // Assert
        var files = Directory.GetFiles(Path.Combine(_testDirectory, "sensor-1"), "*.bin");
        files.Length.ShouldBe(1);
        new FileInfo(files[0]).Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task WriteAsync_NegativeValue_WritesCorrectly()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dataPoint = new RecordingDataPoint(timestamp, -273.15);

        // Act
        await _storage.WriteAsync("sensor-1", 1000, dataPoint);

        // Assert
        var files = Directory.GetFiles(Path.Combine(_testDirectory, "sensor-1"), "*.bin");
        files.Length.ShouldBe(1);
    }

    #endregion
}
