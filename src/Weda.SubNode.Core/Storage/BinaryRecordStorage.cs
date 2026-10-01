using System.Globalization;
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
public class BinaryRecordStorage : IRecordStorage
{
    private readonly ILogger<BinaryRecordStorage> _logger;
    private readonly RecordingOptionsProvider _optionsProvider;
    private readonly string _resolvedStorageDirectory;
    private const int MillisecondsPerDay = 86400000;
    private const ushort CurrentVersion = 2;
    private const string FileNameDateFormat = "yyyy-MM-dd";
    private readonly ResiliencePipeline _storagePipeline;

    /// <summary>
    /// Creates storage that reads its limits through a shared provider.
    /// </summary>
    /// <remarks>
    /// Prefer this overload wherever the limits can change at runtime: the provider is the one
    /// cell a configuration update writes to, so storage constructed this way picks up a new
    /// retention or ring-buffer limit without being rebuilt.
    /// </remarks>
    /// <param name="logger">Logger for storage diagnostics.</param>
    /// <param name="optionsProvider">The provider holding the recording options in force.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public BinaryRecordStorage(ILogger<BinaryRecordStorage> logger, RecordingOptionsProvider optionsProvider)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(optionsProvider);

        _logger = logger;
        _optionsProvider = optionsProvider;
        // Resolved once: the data already written lives here, so relocating it is a restart-time
        // operation, not a runtime one.
        _resolvedStorageDirectory = PathHelper.ResolveStorageDirectory(optionsProvider.Current.StorageDirectory);
        _storagePipeline = ConnectionPolicies.CreateGeneralOperationPipeline(logger, ConnectionPolicyOptions.NFSDefault);
    }

    /// <summary>
    /// Creates storage from a fixed set of options.
    /// </summary>
    /// <param name="logger">Logger for storage diagnostics.</param>
    /// <param name="options">The recording options.</param>
    public BinaryRecordStorage(ILogger<BinaryRecordStorage> logger, IOptions<RecordingOptions> options)
        : this(logger, new RecordingOptionsProvider((options ?? throw new ArgumentNullException(nameof(options))).Value))
    {
    }

    /// <summary>
    /// The recording options in force right now.
    /// </summary>
    private RecordingOptions Options => _optionsProvider.Current;

    /// <summary>
    /// The absolute directory holding recording files.
    /// </summary>
    public string StorageDirectory => _resolvedStorageDirectory;

    public async Task WriteAsync(string sensorId, int interval, SchemaType schemaType, RecordingDataPoint dataPoint, CancellationToken cancellationToken = default)
    {
        if (!schemaType.IsSlotBasedSchema())
            throw new NotSupportedException($"SchemaType {schemaType} is not supported for slot-based storage");

        EnsureDiskSpace(cancellationToken);

        var filePath = GetFilePath(sensorId, interval, schemaType, dataPoint.Timestamp);
        var startOfDay = GetStartOfDay(dataPoint.Timestamp);

        // Ensure-then-write is retried as one unit: a concurrent cleanup can delete the directory
        // and file in the window between them, which would otherwise lose this sample silently.
        // Re-running EnsureFileExists on retry rebuilds what cleanup removed. Cleanup runs at most
        // once a day, so a retry never races the same cleanup pass twice.
        await _storagePipeline.ExecuteAsync(async token =>
        {
            EnsureFileExists(filePath, interval, schemaType, startOfDay);
            await WriteToSlotAsync(filePath, interval, schemaType, startOfDay, dataPoint, token);
        }, cancellationToken);
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
        yield return Path.Combine(sensorDir, FormattableString.Invariant($"{date:yyyy-MM-dd}_{interval}.bin"));

        // V2 format: {date}_{interval}_{schemaType}.bin
        foreach (var schemaType in new[] { SchemaType.Double, SchemaType.Long, SchemaType.Integer, SchemaType.Boolean })
        {
            yield return Path.Combine(sensorDir, FormattableString.Invariant($"{date:yyyy-MM-dd}_{interval}_{schemaType.ToString().ToLowerInvariant()}.bin"));
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

    /// <inheritdoc />
    public Task<RecordingCleanupResult> CleanupAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_resolvedStorageDirectory))
            return Task.FromResult(RecordingCleanupResult.Empty);

        var cutoffDate = before.UtcDateTime.Date;
        var scanned = 0;
        var deleted = 0;
        var failed = 0;
        long bytesFreed = 0;

        foreach (var sensorDir in Directory.GetDirectories(_resolvedStorageDirectory))
        {
            // Skip if directory was deleted between enumeration and access
            if (!Directory.Exists(sensorDir))
                continue;

            foreach (var file in Directory.GetFiles(sensorDir, "*.bin"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;

                var fileName = Path.GetFileNameWithoutExtension(file);
                var datePart = fileName?.Split('_')[0];

                if (!DateTime.TryParseExact(datePart, FileNameDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate)
                    || fileDate >= cutoffDate)
                {
                    continue;
                }

                // One undeletable file must not cost every sensor after it its sweep: a lock
                // or a permission error on a single recording would otherwise abort the pass
                // and let storage grow unbounded with nothing to show why.
                var size = TryGetFileSize(file);
                try
                {
                    File.Delete(file);
                    deleted++;
                    bytesFreed += size;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failed++;
                    _logger.LogWarning(ex, "Failed to delete expired recording {File}", file);
                }
            }

            // A sensor that has aged out entirely must not be left behind as an empty
            // directory: GetSensorIdsAsync enumerates directories, so a stale entry would
            // keep being reported as a sensor that exists but can never return data.
            RemoveSensorDirectoryIfEmpty(sensorDir);
        }

        return Task.FromResult(new RecordingCleanupResult(scanned, deleted, bytesFreed, failed));
    }

    /// <inheritdoc />
    public Task<RecordingCleanupResult> EnforceStorageLimitsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(EnsureDiskSpace(cancellationToken));

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
        var fileName = FormattableString.Invariant($"{date:yyyy-MM-dd}_{interval}_{schemaType.ToString().ToLowerInvariant()}");
        return Path.Combine(_resolvedStorageDirectory, sensorId, $"{fileName}.bin");
    }

    private static long GetStartOfDay(long timestamp)
    {
        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.Date;
        return new DateTimeOffset(date, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    // Not wrapped in the resilience pipeline itself: its only caller, WriteAsync, already runs
    // ensure-then-write as a single retried unit, so an inner pipeline would just nest retries.
    private static void EnsureFileExists(string filePath, int interval, SchemaType schemaType, long startOfDay)
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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What the pass deleted and freed.</returns>
    private RecordingCleanupResult EnsureDiskSpace(CancellationToken cancellationToken = default)
    {
        var options = Options;

        if (options.MinFreeDiskSpaceMb <= 0 && options.MaxStorageSizeMb <= 0)
            return RecordingCleanupResult.Empty;

        if (!Directory.Exists(_resolvedStorageDirectory))
            return RecordingCleanupResult.Empty;

        var minFreeBytes = (long)options.MinFreeDiskSpaceMb * 1024 * 1024;
        var maxStorageBytes = (long)options.MaxStorageSizeMb * 1024 * 1024;
        var drivePath = Path.GetPathRoot(Path.GetFullPath(_resolvedStorageDirectory))!;
        var currentStorageBytes = GetStorageSizeBytes();

        var deleted = 0;
        long bytesFreed = 0;

        bool ShouldCleanup()
        {
            var belowFreeSpace = options.MinFreeDiskSpaceMb > 0 &&
                new DriveInfo(drivePath).AvailableFreeSpace < minFreeBytes;
            var exceedStorageSize = options.MaxStorageSizeMb > 0 && currentStorageBytes > maxStorageBytes;
            return belowFreeSpace || exceedStorageSize;
        }

        while (ShouldCleanup())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deletedFileSize = _storagePipeline.Execute(DeleteOldestFileAndGetSize);

            if (deletedFileSize == 0)
                break;

            deleted++;
            bytesFreed += deletedFileSize;
            currentStorageBytes = Math.Max(0, currentStorageBytes - deletedFileSize);
        }

        return new RecordingCleanupResult(deleted, deleted, bytesFreed, FilesFailed: 0);
    }

    private long DeleteOldestFileAndGetSize()
    {
        var oldestFile = GetOldestFile();
        if (oldestFile == null)
            return 0;

        var deletedFileSize = TryGetFileSize(oldestFile);

        File.Delete(oldestFile);

        var sensorDir = Path.GetDirectoryName(oldestFile);
        if (sensorDir != null)
        {
            RemoveSensorDirectoryIfEmpty(sensorDir);
        }

        return deletedFileSize;
    }

    /// <summary>
    /// Reads a file's size, treating an unreadable file as zero bytes.
    /// </summary>
    /// <remarks>
    /// Only ever used to tally freed space, so a file that vanished underneath us is worth a
    /// warning and a zero - never a failed cleanup pass.
    /// </remarks>
    /// <param name="path">Absolute path of the file to measure.</param>
    /// <returns>The size in bytes, or 0 when it could not be read.</returns>
    private long TryGetFileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read file size for {File}", path);
            return 0;
        }
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
        return DateTime.TryParseExact(datePart, FileNameDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }
}
