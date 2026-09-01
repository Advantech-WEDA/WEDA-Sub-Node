using System.IO.Hashing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Weda.SubNode.Abstractions.Common;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Abstractions.Utilities;
using Weda.SubNode.Core.Policies;

namespace Weda.SubNode.Core.Storage;

/// <summary>
/// Binary slot-based storage for fixed-size data types.
/// Supports V1 (legacy, double-only) for reading and V2 (multi-type) for writing.
/// </summary>
public class BinaryRecordStorage(ILogger<BinaryRecordStorage> logger, IOptions<RecordingOptions> options) : IRecordStorage
{
    private readonly ILogger<BinaryRecordStorage> _logger = logger;
    private readonly RecordingOptions _options = options.Value;
    private readonly string _resolvedStorageDirectory = PathHelper.ResolveStorageDirectory(RecordingOptions.StorageDirectory);
    private const int MillisecondsPerDay = 86400000;
    private const ushort CurrentVersion = 2;
    private readonly ResiliencePipeline _storagePipeline = ConnectionPolicies.CreateGeneralOperationPipeline(logger, ConnectionPolicyOptions.NFSDefault);

    public async Task WriteAsync(string sensorId, int interval, SchemaType schemaType, RecordingDataPoint dataPoint, CancellationToken cancellationToken = default)
    {
        if (!schemaType.IsSlotBasedSchema())
            throw new NotSupportedException($"SchemaType {schemaType} is not supported for slot-based storage");

        EnsureDiskSpace();

        var filePath = GetFilePath(sensorId, interval, schemaType, dataPoint.Timestamp);
        var startOfDay = GetStartOfDay(dataPoint.Timestamp);
        EnsureFileExists(filePath, interval, schemaType, startOfDay);
        await WriteToSlotAsync(filePath, interval, schemaType, startOfDay, dataPoint, cancellationToken);
    }

    public async Task WriteBatchAsync(string sensorId, int interval, SchemaType schemaType, IEnumerable<RecordingDataPoint> dataPoints, CancellationToken cancellationToken = default)
    {
        foreach (var dataPoint in dataPoints)
        {
            await WriteAsync(sensorId, interval, schemaType, dataPoint, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<RecordingDataPoint>> ReadAsync(string sensorId, int interval, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        var result = new List<RecordingDataPoint>();
        var startMs = start.ToUnixTimeMilliseconds();
        var endMs = end.ToUnixTimeMilliseconds();

        // Iterate through each day in the range
        var currentDate = start.UtcDateTime.Date;
        var endDate = end.UtcDateTime.Date;

        while (currentDate <= endDate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Try to find files for this date (V1 or V2 format)
            var filePaths = GetFilePathsForDate(sensorId, interval, currentDate);
            foreach (var filePath in filePaths)
            {
                if (File.Exists(filePath))
                {
                    var dataPoints = await ReadFromFileAsync(filePath, interval, startMs, endMs, cancellationToken);
                    result.AddRange(dataPoints);
                }
            }

            currentDate = currentDate.AddDays(1);
        }

        return result;
    }

    public async Task<IReadOnlyList<RecordingDataPoint>> ReadAllIntervalsAsync(string sensorId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        var intervals = await GetIntervalsAsync(sensorId, cancellationToken);
        if (intervals.Count == 0)
            return [];

        var allResults = new List<RecordingDataPoint>();
        foreach (var interval in intervals)
        {
            var data = await ReadAsync(sensorId, interval, start, end, cancellationToken);
            allResults.AddRange(data);
        }

        return allResults.OrderBy(p => p.Timestamp).ToList();
    }

    /// <summary>
    /// Gets all possible file paths for a sensor/interval/date combination.
    /// Returns V1 path first, then V2 paths for each SchemaType.
    /// </summary>
    private IEnumerable<string> GetFilePathsForDate(string sensorId, int interval, DateTime date)
    {
        var sensorDir = Path.Combine(_resolvedStorageDirectory, sensorId);

        // V1 format: {date}_{interval}.bin
        yield return Path.Combine(sensorDir, $"{date:yyyy-MM-dd}_{interval}.bin");

        // V2 format: {date}_{interval}_{schemaType}.bin
        foreach (var schemaType in new[] { SchemaType.Double, SchemaType.Long, SchemaType.Integer, SchemaType.Boolean })
        {
            yield return Path.Combine(sensorDir, $"{date:yyyy-MM-dd}_{interval}_{schemaType.ToString().ToLowerInvariant()}.bin");
        }
    }

    private static async Task<List<RecordingDataPoint>> ReadFromFileAsync(string filePath, int interval, long startMs, long endMs, CancellationToken cancellationToken)
    {
        var dataPoints = await RecordingBinFileReader.ReadDataPointsAsync(filePath, interval, startMs, endMs, cancellationToken);
        return dataPoints.ToList();
    }

    public Task<IReadOnlyList<string>> GetSensorIdsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_resolvedStorageDirectory))
            return Task.FromResult<IReadOnlyList<string>>([]);

        var sensorIds = Directory.GetDirectories(_resolvedStorageDirectory)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>()
            .OrderBy(name => name)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(sensorIds);
    }

    public Task<PagedResult<string>> GetSensorsAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_resolvedStorageDirectory))
            return Task.FromResult(PagedResult<string>.Empty(pageIndex, pageSize));

        var allSensorIds = Directory.GetDirectories(_resolvedStorageDirectory)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>()
            .OrderBy(name => name)
            .ToList();

        var totalCount = allSensorIds.Count;
        var items = allSensorIds
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResult<string>(items, totalCount, pageIndex, pageSize));
    }

    /// <inheritdoc />
    /// <remarks>
    /// A sensor exists as soon as its directory does, even when retention has removed every
    /// recording file inside it.
    /// </remarks>
    public Task<bool> SensorExistsAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        var sensorDir = Path.Combine(_resolvedStorageDirectory, sensorId);
        return Task.FromResult(Directory.Exists(sensorDir));
    }

    public Task<IReadOnlyList<int>> GetIntervalsAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        var sensorDir = Path.Combine(_resolvedStorageDirectory, sensorId);
        if (!Directory.Exists(sensorDir))
            return Task.FromResult<IReadOnlyList<int>>([]);

        var intervals = Directory.GetFiles(sensorDir, "*.bin")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => ExtractIntervalFromFileName(name))
            .Where(interval => interval.HasValue)
            .Select(interval => interval!.Value)
            .Distinct()
            .OrderBy(i => i)
            .ToList();

        return Task.FromResult<IReadOnlyList<int>>(intervals);
    }

    /// <summary>
    /// Extracts interval from filename.
    /// V1: yyyy-MM-dd_interval
    /// V2: yyyy-MM-dd_interval_schemaType
    /// </summary>
    private static int? ExtractIntervalFromFileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;

        var parts = fileName.Split('_');
        if (parts.Length < 2)
            return null;

        // Interval is always the second part
        return int.TryParse(parts[1], out var interval) ? interval : null;
    }

    public Task CleanupAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_resolvedStorageDirectory))
            return Task.CompletedTask;

        var cutoffDate = before.UtcDateTime.Date;

        foreach (var sensorDir in Directory.GetDirectories(_resolvedStorageDirectory))
        {
            // Skip if directory was deleted between enumeration and access
            if (!Directory.Exists(sensorDir))
                continue;

            foreach (var file in Directory.GetFiles(sensorDir, "*.bin"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = Path.GetFileNameWithoutExtension(file);
                var datePart = fileName?.Split('_')[0];

                if (DateTime.TryParse(datePart, out var fileDate) && fileDate < cutoffDate)
                {
                    File.Delete(file);
                }
            }

            // A sensor that has aged out entirely must not be left behind as an empty
            // directory: GetSensorIdsAsync enumerates directories, so a stale entry would
            // keep being reported as a sensor that exists but can never return data.
            RemoveSensorDirectoryIfEmpty(sensorDir);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a sensor directory once its last recording file is gone.
    /// </summary>
    /// <remarks>
    /// Failures are logged and swallowed: the directory may legitimately gain a new file
    /// from a concurrent write between the emptiness check and the delete, and losing that
    /// race must never fail the cleanup or the write that triggered it.
    /// </remarks>
    /// <param name="sensorDir">Absolute path of the sensor directory to remove when empty.</param>
    private void RemoveSensorDirectoryIfEmpty(string sensorDir)
    {
        try
        {
            if (Directory.Exists(sensorDir) && !Directory.EnumerateFileSystemEntries(sensorDir).Any())
            {
                Directory.Delete(sensorDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove empty sensor directory {SensorDir}", sensorDir);
        }
    }

    public Task DeleteSensorAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        var sensorDir = Path.Combine(_resolvedStorageDirectory, sensorId);
        if (Directory.Exists(sensorDir))
        {
            Directory.Delete(sensorDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(_resolvedStorageDirectory))
        {
            foreach (var sensorDir in Directory.GetDirectories(_resolvedStorageDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Delete(sensorDir, recursive: true);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets file path for V2 format: {date}_{interval}_{schemaType}.bin
    /// </summary>
    private string GetFilePath(string sensorId, int interval, SchemaType schemaType, long timestamp)
    {
        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.Date;
        var fileName = $"{date:yyyy-MM-dd}_{interval}_{schemaType.ToString().ToLowerInvariant()}";
        return Path.Combine(_resolvedStorageDirectory, sensorId, $"{fileName}.bin");
    }

    private static long GetStartOfDay(long timestamp)
    {
        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.Date;
        return new DateTimeOffset(date, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    private void EnsureFileExists(string filePath, int interval, SchemaType schemaType, long startOfDay)
    {
        _storagePipeline.Execute(() =>
        {
            var directory = Path.GetDirectoryName(filePath)!;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (!File.Exists(filePath))
            {
                CreateFileWithHeaderV2(filePath, interval, schemaType, startOfDay);
            }
        });
    }

    /// <summary>
    /// Creates a new V2 format file with header and empty slots.
    /// </summary>
    private static void CreateFileWithHeaderV2(string filePath, int interval, SchemaType schemaType, long startOfDay)
    {
        var slotSize = schemaType.GetSlotSize();
        var slotCount = MillisecondsPerDay / interval;
        var fileSize = RecordingFileHeader.HeaderSizeV2 + slotCount * slotSize;

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        fs.SetLength(fileSize);

        // V2 Header layout (32 bytes):
        // [0-3]   uint32  Prefix
        // [4-5]   uint16  Version (= 2)
        // [6]     byte    Flags (0 = Little Endian)
        // [7]     byte    SchemaType
        // [8-11]  uint32  Interval
        // [12-15] uint32  SlotSize
        // [16-23] ulong   StartTimestamp
        // [24-27] uint32  SlotCount
        // [28-31] uint32  HeaderChecksum (CRC32 of bytes 0-27)

        // Build header bytes (first 28 bytes, checksum excluded)
        var headerBytes = new byte[28];
        var offset = 0;
        BitConverter.GetBytes(RecordingFileHeader.Prefix).CopyTo(headerBytes, offset); offset += 4;
        BitConverter.GetBytes(CurrentVersion).CopyTo(headerBytes, offset); offset += 2;
        headerBytes[offset++] = 0; // Flags: Little Endian
        headerBytes[offset++] = (byte)schemaType;
        BitConverter.GetBytes((uint)interval).CopyTo(headerBytes, offset); offset += 4;
        BitConverter.GetBytes((uint)slotSize).CopyTo(headerBytes, offset); offset += 4;
        BitConverter.GetBytes((ulong)startOfDay).CopyTo(headerBytes, offset); offset += 8;
        BitConverter.GetBytes((uint)slotCount).CopyTo(headerBytes, offset);

        // Calculate CRC32 checksum
        var checksum = Crc32.HashToUInt32(headerBytes);

        // Write header with checksum
        fs.Write(headerBytes);
        fs.Write(BitConverter.GetBytes(checksum));

        // Initialize all slots with empty values
        var emptySlotBytes = schemaType.GetEmptySlotBytes();
        for (int i = 0; i < slotCount; i++)
        {
            fs.Write(emptySlotBytes);
        }
    }

    private static async Task WriteToSlotAsync(string filePath, int interval, SchemaType schemaType, long startOfDay, RecordingDataPoint dataPoint, CancellationToken cancellationToken)
    {
        var slotIndex = (int)((dataPoint.Timestamp - startOfDay) / interval);
        var slotCount = MillisecondsPerDay / interval;
        var slotSize = schemaType.GetSlotSize();

        // Prevent index out of range error
        if (slotIndex < 0 || slotIndex >= slotCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dataPoint),
                $"Timestamp {dataPoint.Timestamp} produces invalid slot {slotIndex} (valid range 0-{slotCount - 1})");
        }

        var position = RecordingFileHeader.HeaderSizeV2 + slotIndex * slotSize;
        var valueBytes = schemaType.ToBytes(dataPoint.Value);

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write);
        fs.Seek(position, SeekOrigin.Begin);
        await fs.WriteAsync(valueBytes, cancellationToken);
    }

    /// <summary>
    /// Ensures sufficient disk space is available by deleting oldest files (Ring Buffer FIFO).
    /// Supports both free-disk threshold and max storage size policies.
    /// </summary>
    private void EnsureDiskSpace()
    {
        if (_options.MinFreeDiskSpaceMb <= 0 && _options.MaxStorageSizeMb <= 0)
            return;

        if (!Directory.Exists(_resolvedStorageDirectory))
            return;

        var minFreeBytes = (long)_options.MinFreeDiskSpaceMb * 1024 * 1024;
        var maxStorageBytes = (long)_options.MaxStorageSizeMb * 1024 * 1024;
        var drivePath = Path.GetPathRoot(Path.GetFullPath(_resolvedStorageDirectory))!;
        var currentStorageBytes = GetStorageSizeBytes();

        bool ShouldCleanup()
        {
            var belowFreeSpace = _options.MinFreeDiskSpaceMb > 0 &&
                new DriveInfo(drivePath).AvailableFreeSpace < minFreeBytes;
            var exceedStorageSize = _options.MaxStorageSizeMb > 0 && currentStorageBytes > maxStorageBytes;
            return belowFreeSpace || exceedStorageSize;
        }

        while (ShouldCleanup())
        {
            var deletedFileSize = _storagePipeline.Execute(DeleteOldestFileAndGetSize);

            if (deletedFileSize == 0)
                break;

            currentStorageBytes = Math.Max(0, currentStorageBytes - deletedFileSize);
        };
    }

    private long DeleteOldestFileAndGetSize()
    {
        var oldestFile = GetOldestFile();
        if (oldestFile == null)
            return 0;

        long deletedFileSize = 0;
        try
        {
            deletedFileSize = new FileInfo(oldestFile).Length;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read file size for {File}", oldestFile);
        }

        File.Delete(oldestFile);

        var sensorDir = Path.GetDirectoryName(oldestFile);
        if (sensorDir != null)
        {
            RemoveSensorDirectoryIfEmpty(sensorDir);
        }

        return deletedFileSize;
    }

    private long GetStorageSizeBytes()
    {
        if (!Directory.Exists(_resolvedStorageDirectory))
            return 0;

        long totalBytes = 0;
        foreach (var file in Directory.GetDirectories(_resolvedStorageDirectory)
                     .SelectMany(sensorDir => Directory.GetFiles(sensorDir, "*.bin")))
        {
            try
            {
                totalBytes += new FileInfo(file).Length;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read file size for {File} during storage calculation", file);
            }
        }

        return totalBytes;
    }

    /// <summary>
    /// Gets the oldest recording file based on date in filename.
    /// </summary>
    private string? GetOldestFile()
    {
        if (!Directory.Exists(_resolvedStorageDirectory))
            return null;

        return Directory.GetDirectories(_resolvedStorageDirectory)
            .SelectMany(sensorDir => Directory.GetFiles(sensorDir, "*.bin"))
            .Select(file => new
            {
                Path = file,
                Date = ParseDateFromFileName(Path.GetFileNameWithoutExtension(file))
            })
            .Where(f => f.Date.HasValue)
            .OrderBy(f => f.Date)
            .Select(f => f.Path)
            .FirstOrDefault();
    }

    /// <summary>
    /// Parses date from filename format.
    /// V1: yyyy-MM-dd_interval
    /// V2: yyyy-MM-dd_interval_schemaType
    /// </summary>
    private static DateTime? ParseDateFromFileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;

        var datePart = fileName.Split('_')[0];
        return DateTime.TryParse(datePart, out var date) ? date : null;
    }
}
