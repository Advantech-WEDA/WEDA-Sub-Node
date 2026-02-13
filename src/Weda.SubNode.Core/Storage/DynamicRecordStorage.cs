using System.Buffers.Binary;
using System.IO.Hashing;

using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Storage.Recordings;
using DrsHelper = Weda.SubNode.Core.Storage.DynamicStoragePathHelper;

namespace Weda.SubNode.Core.Storage;

public class DynamicRecordStorage : IDynamicRecordStorage
{
    private readonly string _dataDir;
    private readonly ILogger<DynamicRecordStorage> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DynamicRecordStorage(ILogger<DynamicRecordStorage> logger, string dataDir)
    {
        _dataDir = dataDir;
        _logger = logger;
        Directory.CreateDirectory(dataDir);
    }


    public async Task AppendAsync(string sensorId, long timestamp, ReadOnlyMemory<byte> payload, SchemaType schemaType, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var date = DrsHelper.GetDateFromTimestamp(timestamp);
            var idxPath = DrsHelper.GetIndexPath(_dataDir, sensorId, date);
            var datPath = DrsHelper.GetDataPath(_dataDir, sensorId, date);

            // 1. Compute payload CRC
            var payloadCrc = Crc32.HashToUInt32(payload.Span);
            
            // 2. Get current data file size (= write offset)
            var datOffset = GetFileSize(datPath);

            // 3. Write data frame: [Magic:2][Size:4][Payload:N]
            await WriteDataFrameAsync(datPath, payload, cancellationToken);

            // 4. Fsync data file
            await FsyncAsync(datPath);

            // 5. Build index record
            var flags = GetIndexFlags(schemaType);
            var seqNum = GetNextSeqNum(idxPath, timestamp);
            var indexRecord = CreateIndexRecord(timestamp, datOffset, payload.Length, payloadCrc, flags, schemaType, seqNum);

            // 6. Write index record
            await WriteIndexRecordAsync(idxPath, indexRecord, cancellationToken);
            
            // 7. Fsync index file
            await FsyncAsync(idxPath);

            _logger.LogDebug(
                "Appended record: Sensor={SensorId}, Timestamp={Teimstamp}, Size={Size}, SchemaType={SchemaType}",
                sensorId, timestamp, payload.Length, schemaType);
        }
        finally
        {
            _lock.Release();   
        }
    }

    public async Task<StorageRecord?> ReadAsync(string sensorId, long timestamp, CancellationToken cancellationToken = default)
    {
        var date = DrsHelper.GetDateFromTimestamp(timestamp);
        var idxPath = DrsHelper.GetIndexPath(_dataDir, sensorId, date);
        var datPath = DrsHelper.GetDataPath(_dataDir, sensorId, date);

        var recordCount = GetRecordCount(idxPath);
        if (recordCount == 0)
            return null;

        await using var idxFs = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var recordIndex = BinarySearchIndex(idxFs, recordCount, timestamp);

        if (recordIndex < 0)
            return null;

        var indexRecord = ReadIndexRecordAt(idxFs, recordIndex);
        var payload = await ReadPayloadAtAsync(datPath, indexRecord.Offset, indexRecord.PayloadSize, cancellationToken);

        return new StorageRecord(
            indexRecord.Timestamp,
            payload,
            indexRecord.SchemaType,
            indexRecord.Flags
        );
    }

    public async Task<IReadOnlyList<StorageRecord>> ReadRangeAsync(string sensorId, long startTime, long endTime, CancellationToken cancellationToken = default)
    {
        var date = DrsHelper.GetDateFromTimestamp(startTime);
        var idxPath = DrsHelper.GetIndexPath(_dataDir, sensorId, date);
        var datPath = DrsHelper.GetDataPath(_dataDir, sensorId, date);

        var recordCount = GetRecordCount(idxPath);
        if (recordCount == 0)
            return [];

        var results = new List<StorageRecord>();

        await using var idxFs = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var startIndex = BinarySearchLowerBound(idxFs, recordCount, startTime);

        if (startIndex < 0)
            return [];

        for (var i = startIndex; i < recordCount; i++)
        {
            var indexRecord = ReadIndexRecordAt(idxFs, i);

            if (indexRecord.Timestamp > endTime)
                break;

            var payload = await ReadPayloadAtAsync(datPath, indexRecord.Offset, indexRecord.PayloadSize, cancellationToken);
            results.Add(new StorageRecord(
                indexRecord.Timestamp,
                payload,
                indexRecord.SchemaType,
                indexRecord.Flags
            ));
        }

        return results;
    }

    public Task<int> CleanupAsync(DateTimeOffset cutoffDate, CancellationToken cancellationToken = default)
    {
        var cutoff = DateOnly.FromDateTime(cutoffDate.UtcDateTime);
        var deletedCount = 0;

        if (!Directory.Exists(_dataDir))
            return Task.FromResult(0);

        // Pattern: {sensorId}_{yyyy-MM-dd}_v{version}.idx
        var idxFiles = Directory.GetFiles(_dataDir, $"*{DrsHelper.IndexExtension}");

        foreach (var idxPath in idxFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileNameWithoutExtension(idxPath);
            var fileDate = ExtractDateFromFileName(fileName);

            if (fileDate is null || fileDate >= cutoff)
                continue;

            // Delete .idx and corresponding .dat
            var datPath = Path.ChangeExtension(idxPath, DrsHelper.DataExtension);

            try
            {
                File.Delete(idxPath);
                if (File.Exists(datPath))
                    File.Delete(datPath);

                deletedCount++;
                _logger.LogDebug("Deleted expired files: {IdxPath}", idxPath);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Failed to delete expired file: {IdxPath}", idxPath);
            }
        }

        _logger.LogInformation("Cleanup completed: Deleted {Count} file pairs older than {CutoffDate:yyyy-MM-dd}", deletedCount, cutoff);
        return Task.FromResult(deletedCount);
    }

    public Task<IReadOnlyList<string>> GetSensorIdsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_dataDir))
            return Task.FromResult<IReadOnlyList<string>>([]);

        // Pattern: {sensorId}_{yyyy-MM-dd}_v{version}.idx
        var idxFiles = Directory.GetFiles(_dataDir, $"*{DrsHelper.IndexExtension}");

        var sensorIds = idxFiles
            .Select(path => ExtractSensorIdFromFileName(Path.GetFileNameWithoutExtension(path)))
            .Where(id => id != null)
            .Distinct()
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(sensorIds!);
    }

    private static string? ExtractSensorIdFromFileName(string fileName)
    {
        // Format: {sensorId}_{yyyy-MM-dd}_v{version}
        var firstUnderscore = fileName.IndexOf('_');
        if (firstUnderscore <= 0)
            return null;

        return fileName[..firstUnderscore];
    }

    private static DateOnly? ExtractDateFromFileName(string fileName)
    {
        // Format: {sensorId}_{yyyy-MM-dd}_v{version}
        var parts = fileName.Split('_');
        if (parts.Length < 2)
            return null;

        // Date is second-to-last part (before _v{version})
        var datePart = parts[^2];
        if (DateOnly.TryParseExact(datePart, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
            return date;

        return null;
    }

    private static long GetFileSize(string path)
        => File.Exists(path) ? new FileInfo(path).Length : 0;

    private async static Task WriteDataFrameAsync(string path, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        await using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);

        var header = new byte[DataFrame.HeaderSize];
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0), DataFrame.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(2), (uint)payload.Length);
        await fs.WriteAsync(header, cancellationToken);
        await fs.WriteAsync(payload, cancellationToken);
    }

    private async static Task FsyncAsync(string path)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Flush(flushToDisk: true);
    }

    private static IndexFlags GetIndexFlags(SchemaType schemaType) 
        => schemaType >= SchemaType.ImageJpeg ? IndexFlags.Mime : IndexFlags.None;

    private static ushort GetNextSeqNum(string idxPath, long timestamp)
    {
        if (!File.Exists(idxPath))
            return 0;

        var fileSize = new FileInfo(idxPath).Length;
        if (fileSize < IndexRecord.Size)
            return 0;

        // Read last index record to check timestamp
        using var fs = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(-IndexRecord.Size, SeekOrigin.End);

        var buffer = new byte[IndexRecord.Size];
        fs.ReadExactly(buffer);

        var lastTimestamp = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan()[0..]);
        var lastSeqNum = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan()[26..]);

        // If same timestamp, increment SeqNum; otherwise reset to 0
        return lastTimestamp == timestamp ? (ushort)(lastSeqNum + 1) : (ushort)0;
    }

    private static IndexRecord CreateIndexRecord(long timestamp, long offset, int size, uint payloadCrc, IndexFlags flags, SchemaType schemaType, ushort seqNum)
    {
        // compute indexCrc over first 28 bytes
        var buffer = new byte[28];
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan()[0..], timestamp);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan()[8..], offset);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan()[16..], size);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan()[20..], payloadCrc);
        buffer[24] = (byte)flags;
        buffer[25] = (byte)schemaType;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan()[26..], seqNum);

        var indexCrc = Crc32.HashToUInt32(buffer);

        return new IndexRecord(timestamp, offset, size, payloadCrc, flags, schemaType, seqNum, indexCrc);
    }
     
    private static async Task WriteIndexRecordAsync(string path, IndexRecord record, CancellationToken cancellationToken)
    {
        await using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);

        var buffer = new byte[IndexRecord.Size];
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan()[0..], record.Timestamp);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan()[8..], record.Offset);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan()[16..], record.PayloadSize);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan()[20..], record.PayloadCrc);
        buffer[24] = (byte)record.Flags;
        buffer[25] = (byte)record.SchemaType;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan()[26..], record.SeqNum);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan()[28..], record.IndexCrc);

        await fs.WriteAsync(buffer, cancellationToken);
    }

    private static IndexRecord ReadIndexRecordAt(FileStream fs, long recordIndex)
    {
        var offset = recordIndex * IndexRecord.Size;
        fs.Seek(offset, SeekOrigin.Begin);

        Span<byte> buffer = stackalloc byte[IndexRecord.Size];
        fs.ReadExactly(buffer);

        return new IndexRecord(
            timestamp: BinaryPrimitives.ReadInt64LittleEndian(buffer[0..]),
            offset: BinaryPrimitives.ReadInt64LittleEndian(buffer[8..]),
            payloadSize: BinaryPrimitives.ReadInt32LittleEndian(buffer[16..]),
            payloadCrc: BinaryPrimitives.ReadUInt32LittleEndian(buffer[20..]),
            flags: (IndexFlags)buffer[24],
            schemaType: (SchemaType)buffer[25],
            seqNum: BinaryPrimitives.ReadUInt16LittleEndian(buffer[26..]),
            indexCrc: BinaryPrimitives.ReadUInt32LittleEndian(buffer[28..])
        );
    }

    private static long GetRecordCount(string idxPath)
    {
        if (!File.Exists(idxPath))
            return 0;
        return new FileInfo(idxPath).Length / IndexRecord.Size;
    }

    private static long BinarySearchIndex(FileStream fs, long recordCount, long timestamp)
    {
        if (recordCount == 0)
            return -1;

        long left = 0;
        long right = recordCount - 1;
        long result = -1;

        while (left <= right)
        {
            var mid = left + (right - left) / 2;
            var record = ReadIndexRecordAt(fs, mid);

            if (record.Timestamp == timestamp)
            {
                result = mid;
                right = mid - 1;
            }
            else if (record.Timestamp < timestamp)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return result;
    }

    private static long BinarySearchLowerBound(FileStream fs, long recordCount, long timestamp)
    {
        if (recordCount == 0)
            return -1;

        long left = 0;
        long right = recordCount;

        while (left < right)
        {
            var mid = left + (right - left) / 2;
            var record = ReadIndexRecordAt(fs, mid);

            if (record.Timestamp < timestamp)
            {
                left = mid + 1;
            }
            else
            {
                right = mid;
            }
        }

        return left < recordCount ? left : -1;
    }

    private static async Task<byte[]> ReadPayloadAtAsync(string datPath, long offset, int size, CancellationToken cancellationToken)
    {
        await using var fs = new FileStream(datPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(offset + DataFrame.HeaderSize, SeekOrigin.Begin);

        var buffer = new byte[size];
        await fs.ReadExactlyAsync(buffer, cancellationToken);
        return buffer;
    }
}