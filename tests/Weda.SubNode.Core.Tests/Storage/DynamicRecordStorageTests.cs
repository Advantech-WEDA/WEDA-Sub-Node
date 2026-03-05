using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Core.Storage;

using Xunit;

namespace Weda.SubNode.Core.Tests.Storage;

public class DynamicRecordStorageTests : IDisposable
{
    private readonly string _testDir;
    private readonly DynamicRecordStorage _storage;

    public DynamicRecordStorageTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"drs_test_{Guid.NewGuid():N}");
        _storage = new DynamicRecordStorage(NullLogger<DynamicRecordStorage>.Instance, _testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Fact]
    public async Task AppendAndRead_SingleRecord_ShouldMatch()
    {
        // Arrange
        var sensorId = "sensor01";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var payload = Encoding.UTF8.GetBytes("Hello, World!");

        // Act
        await _storage.AppendAsync(sensorId, timestamp, payload, SchemaType.String);
        var record = await _storage.ReadAsync(sensorId, timestamp);

        // Assert
        record.ShouldNotBeNull();
        record.Value.Timestamp.ShouldBe(timestamp);
        record.Value.Payload.ToArray().ShouldBe(payload);
        record.Value.SchemaType.ShouldBe(SchemaType.String);
    }

    [Fact]
    public async Task AppendAndRead_MultipleRecords_ShouldFindByTimestamp()
    {
        // Arrange
        var sensorId = "sensor02";
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var payloads = new[]
        {
            Encoding.UTF8.GetBytes("Record 1"),
            Encoding.UTF8.GetBytes("Record 2"),
            Encoding.UTF8.GetBytes("Record 3")
        };

        // Act
        for (int i = 0; i < payloads.Length; i++)
        {
            await _storage.AppendAsync(sensorId, baseTime + i * 1000, payloads[i], SchemaType.String);
        }

        // Assert - find middle record
        var record = await _storage.ReadAsync(sensorId, baseTime + 1000);
        record.ShouldNotBeNull();
        record.Value.Payload.ToArray().ShouldBe(payloads[1]);
    }

    [Fact]
    public async Task ReadAsync_NonExistentTimestamp_ShouldReturnNull()
    {
        // Arrange
        var sensorId = "sensor03";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _storage.AppendAsync(sensorId, timestamp, new byte[] { 1, 2, 3 }, SchemaType.Integer);

        // Act
        var record = await _storage.ReadAsync(sensorId, timestamp + 999);

        // Assert
        record.ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_NonExistentSensor_ShouldReturnNull()
    {
        // Act
        var record = await _storage.ReadAsync("nonexistent", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        // Assert
        record.ShouldBeNull();
    }

    [Fact]
    public async Task ReadRangeAsync_ShouldReturnRecordsInRange()
    {
        // Arrange
        var sensorId = "sensor04";
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 10; i++)
        {
            var payload = Encoding.UTF8.GetBytes($"Record {i}");
            await _storage.AppendAsync(sensorId, baseTime + i * 1000, payload, SchemaType.String);
        }

        // Act - query middle range [3, 7]
        var records = await _storage.ReadRangeAsync(sensorId, baseTime + 3000, baseTime + 7000);

        // Assert
        records.Count.ShouldBe(5);
        records[0].Timestamp.ShouldBe(baseTime + 3000);
        records[4].Timestamp.ShouldBe(baseTime + 7000);
    }

    [Fact]
    public async Task ReadRangeAsync_NoRecordsInRange_ShouldReturnEmpty()
    {
        // Arrange
        var sensorId = "sensor05";
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await _storage.AppendAsync(sensorId, baseTime, new byte[] { 1, 2, 3 }, SchemaType.Integer);

        // Act
        var records = await _storage.ReadRangeAsync(sensorId, baseTime + 10000, baseTime + 20000);

        // Assert
        records.ShouldBeEmpty();
    }

    [Fact]
    public async Task AppendAsync_DuplicateTimestamps_ShouldIncrementSeqNum()
    {
        // Arrange
        var sensorId = "sensor06";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Act - append 3 records with same timestamp
        await _storage.AppendAsync(sensorId, timestamp, new byte[] { 1 }, SchemaType.Integer);
        await _storage.AppendAsync(sensorId, timestamp, new byte[] { 2 }, SchemaType.Integer);
        await _storage.AppendAsync(sensorId, timestamp, new byte[] { 3 }, SchemaType.Integer);

        // Assert - ReadAsync returns first record (SeqNum=0)
        var record = await _storage.ReadAsync(sensorId, timestamp);
        record.ShouldNotBeNull();
        record.Value.Payload.ToArray().ShouldBe(new byte[] { 1 });

        // ReadRangeAsync returns all 3
        var records = await _storage.ReadRangeAsync(sensorId, timestamp, timestamp);
        records.Count.ShouldBe(3);
    }

    [Fact]
    public async Task AppendAsync_LargePayload_ShouldHandleCorrectly()
    {
        // Arrange
        var sensorId = "sensor07";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var payload = new byte[1024 * 1024]; // 1MB
        Random.Shared.NextBytes(payload);

        // Act
        await _storage.AppendAsync(sensorId, timestamp, payload, SchemaType.ImageJpeg);
        var record = await _storage.ReadAsync(sensorId, timestamp);

        // Assert
        record.ShouldNotBeNull();
        record.Value.Payload.ToArray().ShouldBe(payload);
        record.Value.SchemaType.ShouldBe(SchemaType.ImageJpeg);
        record.Value.Flags.ShouldBe(IndexFlags.Mime);
    }

    [Fact]
    public async Task BinarySearch_ManyRecords_ShouldFindCorrectly()
    {
        // Arrange
        var sensorId = "sensor08";
        var baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var recordCount = 1000;

        for (int i = 0; i < recordCount; i++)
        {
            await _storage.AppendAsync(sensorId, baseTime + i, new byte[] { (byte)(i % 256) }, SchemaType.Integer);
        }

        // Act & Assert - find various positions
        var first = await _storage.ReadAsync(sensorId, baseTime);
        first.ShouldNotBeNull();
        first.Value.Payload.ToArray().ShouldBe(new byte[] { 0 });

        var middle = await _storage.ReadAsync(sensorId, baseTime + 500);
        middle.ShouldNotBeNull();
        middle.Value.Payload.ToArray().ShouldBe(new byte[] { 500 % 256 });

        var last = await _storage.ReadAsync(sensorId, baseTime + 999);
        last.ShouldNotBeNull();
        last.Value.Payload.ToArray().ShouldBe(new byte[] { 999 % 256 });

        var notFound = await _storage.ReadAsync(sensorId, baseTime + 1000);
        notFound.ShouldBeNull();
    }

    [Fact]
    public async Task CleanupAsync_RemovesOldFiles()
    {
        // Arrange - Write data for "10 days ago"
        var sensorId = "sensor09";
        var oldDate = DateTimeOffset.UtcNow.AddDays(-10);
        var oldTimestamp = oldDate.ToUnixTimeMilliseconds();
        await _storage.AppendAsync(sensorId, oldTimestamp, new byte[] { 1, 2, 3 }, SchemaType.String);

        // Verify files exist
        var idxPattern = $"{sensorId}_{oldDate:yyyy-MM-dd}_v1.idx";
        var datPattern = $"{sensorId}_{oldDate:yyyy-MM-dd}_v1.dat";
        File.Exists(Path.Combine(_testDir, idxPattern)).ShouldBeTrue();
        File.Exists(Path.Combine(_testDir, datPattern)).ShouldBeTrue();

        // Act - Cleanup files older than 7 days
        var deleted = await _storage.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert
        deleted.ShouldBe(1);
        File.Exists(Path.Combine(_testDir, idxPattern)).ShouldBeFalse();
        File.Exists(Path.Combine(_testDir, datPattern)).ShouldBeFalse();
    }

    [Fact]
    public async Task CleanupAsync_KeepsRecentFiles()
    {
        // Arrange - Write data for "3 days ago"
        var sensorId = "sensor10";
        var recentDate = DateTimeOffset.UtcNow.AddDays(-3);
        var recentTimestamp = recentDate.ToUnixTimeMilliseconds();
        await _storage.AppendAsync(sensorId, recentTimestamp, new byte[] { 1, 2, 3 }, SchemaType.String);

        // Act - Cleanup files older than 7 days
        var deleted = await _storage.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert - Recent files should still exist
        deleted.ShouldBe(0);
        var idxPattern = $"{sensorId}_{recentDate:yyyy-MM-dd}_v1.idx";
        File.Exists(Path.Combine(_testDir, idxPattern)).ShouldBeTrue();
    }
}
