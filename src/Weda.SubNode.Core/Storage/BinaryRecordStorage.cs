using Microsoft.Extensions.Options;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Abstractions.Utilities;

namespace Weda.SubNode.Core.Storage;

public class BinaryRecordStorage(IOptions<RecordingOptions> options) : IRecordStorage
{
    private readonly RecordingOptions _options = options.Value;
    private readonly string _resolvedStorageDirectory = PathHelper.ResolveStorageDirectory(options.Value.StorageDirectory);
    private const int MillisecondsPerDay = 86400000;
    private const ushort Version = 1;

    public async Task WriteAsync(string sensorId, int interval, RecordingDataPoint dataPoint, CancellationToken cancellationToken = default)
    {
        EnsureDiskSpace();

        var filePath = GetFilePath(sensorId, interval, dataPoint.Timestamp);
        var startOfDay = GetStartOfDay(dataPoint.Timestamp);
        EnsureFileExists(filePath, interval, startOfDay);
        await WriteToSlotAsync(filePath, interval, startOfDay, dataPoint, cancellationToken);
    }

    public async Task WriteBatchAsync(string sensorId, int interval, IEnumerable<RecordingDataPoint> dataPoints, CancellationToken cancellationToken = default)
    {
        foreach (var dataPoint in dataPoints)
        {
            await WriteAsync(sensorId, interval, dataPoint, cancellationToken);
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

            var filePath = GetFilePathForDate(sensorId, interval, currentDate);
            if (File.Exists(filePath))
            {
                var dataPoints = await ReadFromFileAsync(filePath, interval, startMs, endMs, cancellationToken);
                result.AddRange(dataPoints);
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

    private string GetFilePathForDate(string sensorId, int interval, DateTime date)
    {
        var fileName = $"{date:yyyy-MM-dd}_{interval}";
        return Path.Combine(_resolvedStorageDirectory, sensorId, $"{fileName}.bin");
    }

    private static async Task<List<RecordingDataPoint>> ReadFromFileAsync(string filePath, int interval, long startMs, long endMs, CancellationToken cancellationToken)
    {
        var result = new List<RecordingDataPoint>();

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Read header
        var headerBytes = new byte[RecordingFileHeader.HeaderSize];
        await fs.ReadExactlyAsync(headerBytes, cancellationToken);

        var startOfDay = BitConverter.ToInt64(headerBytes, 8);
        var slotCount = BitConverter.ToInt32(headerBytes, 16);

        // Calculate slot range to read
        var startSlot = Math.Max(0, (int)((startMs - startOfDay) / interval));
        var endSlot = Math.Min(slotCount - 1, (int)((endMs - startOfDay) / interval));

        if (startSlot > endSlot || startSlot >= slotCount)
            return result;

        // Seek to start slot
        var startPosition = RecordingFileHeader.HeaderSize + startSlot * sizeof(double);
        fs.Seek(startPosition, SeekOrigin.Begin);

        // Read slots (including NaN values to preserve gaps)
        var buffer = new byte[sizeof(double)];
        for (var slot = startSlot; slot <= endSlot; slot++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await fs.ReadExactlyAsync(buffer, cancellationToken);
            var value = BitConverter.ToDouble(buffer);
            var timestamp = startOfDay + (long)slot * interval;
            result.Add(new RecordingDataPoint(timestamp, value));
        }

        return result;
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

    public Task<IReadOnlyList<int>> GetIntervalsAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        var sensorDir = Path.Combine(_resolvedStorageDirectory, sensorId);
        if (!Directory.Exists(sensorDir))
            return Task.FromResult<IReadOnlyList<int>>([]);

        var intervals = Directory.GetFiles(sensorDir, "*.bin")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name?.Split('_').LastOrDefault())
            .Where(intervalStr => int.TryParse(intervalStr, out _))
            .Select(intervalStr => int.Parse(intervalStr!))
            .Distinct()
            .OrderBy(i => i)
            .ToList();

        return Task.FromResult<IReadOnlyList<int>>(intervals);
    }

    public Task CleanupAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_resolvedStorageDirectory))
            return Task.CompletedTask;

        var cutoffDate = before.UtcDateTime.Date;

        foreach (var sensorDir in Directory.GetDirectories(_resolvedStorageDirectory))
        {
            foreach (var file in Directory.GetFiles(sensorDir, "*.bin"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = Path.GetFileNameWithoutExtension(file);
                var datePart = fileName.Split('_')[0];

                if (DateTime.TryParse(datePart, out var fileDate) && fileDate < cutoffDate)
                {
                    File.Delete(file);
                }
            }
        }

        return Task.CompletedTask;
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

    private string GetFilePath(string sensorId, int interval, long timestamp)
    {
        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.Date;
        var fileName = $"{date:yyyy-MM-dd}_{interval}";
        return Path.Combine(_resolvedStorageDirectory, sensorId, $"{fileName}.bin");
    }

    private static long GetStartOfDay(long timestamp)
    {
        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.Date;
        return new DateTimeOffset(date, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    private static void EnsureFileExists(string filePath, int interval, long startOfDay)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(filePath))
        {
            CreateFileWithHeader(filePath, interval, startOfDay);
        }
    }

    private static void CreateFileWithHeader(string filePath, int interval, long startOfDay)
    {
        var slotCount = MillisecondsPerDay / interval;
        var fileSize = RecordingFileHeader.HeaderSize + slotCount * sizeof(double);

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        fs.SetLength(fileSize);

        fs.Write(BitConverter.GetBytes(RecordingFileHeader.Prefix));
        fs.Write(BitConverter.GetBytes(Version));
        fs.Write(BitConverter.GetBytes((ushort)interval));
        fs.Write(BitConverter.GetBytes(startOfDay));
        fs.Write(BitConverter.GetBytes(slotCount));
        fs.Write(BitConverter.GetBytes(0));

        var nanBytes = BitConverter.GetBytes(double.NaN);
        for (int i = 0; i < slotCount; i++)
        {
            fs.Write(nanBytes);
        }
    }

    private static async Task WriteToSlotAsync(string filePath, int interval, long startOfDay, RecordingDataPoint dataPoint, CancellationToken cancellationToken)
    {
        var slotIndex = (int)((dataPoint.Timestamp - startOfDay) / interval);
        var position = RecordingFileHeader.HeaderSize + slotIndex * sizeof(double);

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write);
        fs.Seek(position, SeekOrigin.Begin);
        await fs.WriteAsync(BitConverter.GetBytes(dataPoint.Value), cancellationToken);
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
            var oldestFile = GetOldestFile();
            if (oldestFile == null)
                break;

            long deletedFileSize = 0;
            try
            {
                deletedFileSize = new FileInfo(oldestFile).Length;
            }
            catch
            {
                // Ignore file size read errors; cleanup still proceeds.
            }

            File.Delete(oldestFile);

            // Clean up empty sensor directories
            var sensorDir = Path.GetDirectoryName(oldestFile);
            if (sensorDir != null && Directory.Exists(sensorDir) && !Directory.EnumerateFileSystemEntries(sensorDir).Any())
            {
                Directory.Delete(sensorDir);
            }

            if (deletedFileSize > 0)
            {
                currentStorageBytes = Math.Max(0, currentStorageBytes - deletedFileSize);
            }
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
            catch
            {
                // Skip files that can't be read.
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
    /// Parses date from filename format: yyyy-MM-dd_interval
    /// </summary>
    private static DateTime? ParseDateFromFileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;

        var datePart = fileName.Split('_')[0];
        return DateTime.TryParse(datePart, out var date) ? date : null;
    }
}