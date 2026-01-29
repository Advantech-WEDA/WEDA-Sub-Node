using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Abstractions.Utilities;
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
        // Use the constant storage directory resolved by PathHelper
        _testDirectory = PathHelper.ResolveStorageDirectory(RecordingOptions.StorageDirectory);
        // Use a unique prefix for sensor IDs to avoid conflicts between test runs
        _testSensorPrefix = $"test-{Guid.NewGuid():N}-";

        _options = new RecordingOptions();
        _storage = new BinaryRecordStorage(_loggerFactory.CreateLogger<BinaryRecordStorage>(), Options.Create(_options));
        _deviceRegistry = new DeviceRegistry();
        _service = new RecordingService(_loggerFactory.CreateLogger<RecordingService>(), _storage, _deviceRegistry, Options.Create(_options));
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
        await _service.RecordAsync(sensorId, 1000, timestamp, 42.5);

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
        await _service.RecordAsync(sensorId, 1000, timestamp, 42.5);

        var sensorDir = Path.Combine(_testDirectory, sensorId);
        var files = Directory.GetFiles(sensorDir, "*.bin");
        var initialSize = new FileInfo(files[0]).Length;

        // Act - Same slot
        await _service.RecordAsync(sensorId, 1000, timestamp + 500, 43.0);

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
        await _service.RecordAsync(sensorId, 1000, startOfDayTimestamp, 42.5);

        // Act - Write to slot 1 (1 second later)
        await _service.RecordAsync(sensorId, 1000, startOfDayTimestamp + 1000, 43.0);

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
        await _service.RecordAsync(sensorId1, 1000, timestamp, 42.5);
        await _service.RecordAsync(sensorId2, 1000, timestamp, 43.5);

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
        await _service.RecordAsync(sensorId, 1000, oldTimestamp, 42.5);

        // Verify file exists before cleanup
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        var oldFileName = $"{oldDate:yyyy-MM-dd}_1000.bin";
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
        await _service.RecordAsync(sensorId, 1000, recentTimestamp, 42.5);

        // Verify file exists before cleanup
        var sensorDir = Path.Combine(_testDirectory, sensorId);
        var recentFileName = $"{recentDate:yyyy-MM-dd}_1000.bin";
        var recentFilePath = Path.Combine(sensorDir, recentFileName);
        File.Exists(recentFilePath).ShouldBeTrue();

        // Act - Cleanup files older than 7 days
        await _service.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert - Recent file should still exist
        File.Exists(recentFilePath).ShouldBeTrue();
    }

    #endregion
}
