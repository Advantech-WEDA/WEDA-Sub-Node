using System.Globalization;
using System.Runtime.InteropServices;
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

    [Fact]
    public async Task WriteAsync_AfterDirectoryRemoved_RebuildsAndWrites()
    {
        // A concurrent cleanup can delete a sensor's directory between the ensure and the write.
        // WriteAsync runs ensure-then-write as one retried unit, so re-running it must rebuild the
        // removed directory and persist the sample rather than lose it. This exercises that rebuild
        // path deterministically by deleting the directory out from under a second write.
        var sensorId = TestSensorId("sensor-rebuild");
        var today = DateTimeOffset.UtcNow.Date;
        var firstTs = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var secondTs = firstTs + 1000;

        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, new RecordingDataPoint(firstTs, 1.0, SchemaType.Double));

        var sensorDir = Path.Combine(_testDirectory, sensorId);
        Directory.Delete(sensorDir, recursive: true);
        Directory.Exists(sensorDir).ShouldBeFalse();

        // Act - the directory is gone; the write must recreate it and succeed.
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, new RecordingDataPoint(secondTs, 2.0, SchemaType.Double));

        // Assert - the second sample is persisted and readable.
        Directory.Exists(sensorDir).ShouldBeTrue();
        var data = await _storage.ReadAsync(sensorId, 1000,
            DateTimeOffset.FromUnixTimeMilliseconds(secondTs).AddSeconds(-1),
            DateTimeOffset.FromUnixTimeMilliseconds(secondTs).AddSeconds(1));
        data.ShouldContain(p => p.Timestamp == secondTs);
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

    [Fact]
    public async Task CleanupAsync_UnderNonInvariantCulture_WritesGregorianNameAndExpiresOldFiles()
    {
        // Filenames must be Gregorian yyyy-MM-dd on both the write and read side, independent of
        // the host culture. ar-SA defaults to the Umm al-Qura calendar: a culture-sensitive write
        // would emit a name like "1448-03-09_..." and a culture-sensitive parse would read those
        // digits back as a different calendar, silently corrupting retention. This test pins both
        // ends: the written name is Gregorian, and cleanup still expires it.
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            var sensorId = TestSensorId("sensor-culture");
            var oldDate = DateTime.UtcNow.AddDays(-10);
            var oldTimestamp = new DateTimeOffset(oldDate, TimeSpan.Zero).ToUnixTimeMilliseconds();

            await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, new RecordingDataPoint(oldTimestamp, 42.5, SchemaType.Double));

            // The written filename must carry the Gregorian year, not the Umm al-Qura year.
            var sensorDir = Path.Combine(_testDirectory, sensorId);
            var writtenName = Path.GetFileName(Directory.GetFiles(sensorDir, "*.bin").ShouldHaveSingleItem());
            var expectedName = FormattableString.Invariant($"{oldDate:yyyy-MM-dd}_1000_double.bin");
            writtenName.ShouldBe(expectedName);

            // Act
            await _storage.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

            // Assert - The expired file is removed even though the current culture is non-Gregorian.
            File.Exists(Path.Combine(sensorDir, expectedName)).ShouldBeFalse();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task CleanupAsync_LastFileExpires_RemovesSensorDirectory()
    {
        // Arrange - A sensor whose only recording is past retention
        var sensorId = TestSensorId("sensor-aged-out");
        var oldDate = DateTime.UtcNow.AddDays(-10);
        var oldTimestamp = new DateTimeOffset(oldDate, TimeSpan.Zero).ToUnixTimeMilliseconds();

        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, new RecordingDataPoint(oldTimestamp, 42.5, SchemaType.Double));

        var sensorDir = Path.Combine(_testDirectory, sensorId);
        Directory.Exists(sensorDir).ShouldBeTrue();

        // Act
        await _storage.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert - The directory must not survive as an empty shell, otherwise the sensor keeps
        // being enumerated while it can never return data.
        Directory.Exists(sensorDir).ShouldBeFalse();

        var sensorIds = await _storage.GetSensorIdsAsync();
        sensorIds.ShouldNotContain(sensorId);
    }

    [Fact]
    public async Task CleanupAsync_SomeFilesRemain_KeepsSensorDirectory()
    {
        // Arrange - One expired file and one still within retention
        var sensorId = TestSensorId("sensor-partly-aged");
        var oldDate = DateTime.UtcNow.AddDays(-10);
        var recentDate = DateTime.UtcNow.AddDays(-3);

        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double,
            new RecordingDataPoint(new DateTimeOffset(oldDate, TimeSpan.Zero).ToUnixTimeMilliseconds(), 1.0, SchemaType.Double));
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double,
            new RecordingDataPoint(new DateTimeOffset(recentDate, TimeSpan.Zero).ToUnixTimeMilliseconds(), 2.0, SchemaType.Double));

        // Act
        await _storage.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        Directory.Exists(sensorDir).ShouldBeTrue();
        Directory.GetFiles(sensorDir, "*.bin").Length.ShouldBe(1);
    }

    [Fact]
    public async Task CleanupAsync_EmptyDirectoryDeleteFails_SwallowsErrorAndDoesNotThrow()
    {
        // The empty-directory removal races concurrent writes: the directory can be judged empty
        // and then have its deletion fail (a concurrent WriteAsync re-populates it, or the delete
        // simply loses). That failure must never propagate out of cleanup. We reproduce the
        // failure deterministically by making the parent unwritable so deleting the (empty) sensor
        // directory throws, then assert cleanup still completes.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || IsRunningAsRoot())
        {
            // The permission barrier below does not hold on Windows or for root; skip rather than
            // assert something the platform cannot enforce.
            return;
        }

        var sensorId = TestSensorId("sensor-delete-blocked");
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        Directory.CreateDirectory(sensorDir);
        Directory.EnumerateFileSystemEntries(sensorDir).ShouldBeEmpty();

        var originalMode = File.GetUnixFileMode(_testDirectory);
        try
        {
            // Drop write permission on the parent so deleting the empty sensor directory throws.
            File.SetUnixFileMode(_testDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            await Should.NotThrowAsync(() => _storage.CleanupAsync(DateTimeOffset.UtcNow));
        }
        finally
        {
            File.SetUnixFileMode(_testDirectory, originalMode);
            if (Directory.Exists(sensorDir))
                Directory.Delete(sensorDir, recursive: true);
        }
    }

    private static bool IsRunningAsRoot() =>
        !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.UserName == "root";

    #endregion

    #region SensorExistsAsync Tests

    [Fact]
    public async Task SensorExistsAsync_SensorWithData_ReturnsTrue()
    {
        // Arrange
        var sensorId = TestSensorId("sensor-1");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, new RecordingDataPoint(timestamp, 42.5, SchemaType.Double));

        // Act
        var exists = await _storage.SensorExistsAsync(sensorId);

        // Assert
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task SensorExistsAsync_DirectoryWithoutFiles_ReturnsTrue()
    {
        // Arrange - A sensor directory left behind with no recordings in it
        var sensorId = TestSensorId("sensor-empty");
        Directory.CreateDirectory(Path.Combine(_testDirectory, sensorId));

        // Act
        var exists = await _storage.SensorExistsAsync(sensorId);

        // Assert - Known sensor, no data. Existence must not depend on having files.
        exists.ShouldBeTrue();
        (await _storage.GetIntervalsAsync(sensorId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SensorExistsAsync_UnknownSensor_ReturnsFalse()
    {
        // Act
        var exists = await _storage.SensorExistsAsync(TestSensorId("never-recorded"));

        // Assert
        exists.ShouldBeFalse();
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
