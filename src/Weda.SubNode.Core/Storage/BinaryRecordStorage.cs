using Microsoft.Extensions.Options;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Core.Storage;

public class BinaryRecordStorage(IOptions<RecordingOptions> options) : IRecordStorage
{
    private readonly RecordingOptions _options = options.Value;
    private const int MillisecondsPerDay = 86400000;
    private const ushort Version = 1;

    public async Task WriteAsync(string sensorId, int interval, RecordingDataPoint dataPoint, CancellationToken cancellationToken = default)
    {
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

    public Task<IReadOnlyList<RecordingDataPoint>> ReadAsync(string sensorId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task CleanupAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_options.StorageDirectory))
            return Task.CompletedTask;

        var cutoffDate = before.UtcDateTime.Date;

        foreach (var sensorDir in Directory.GetDirectories(_options.StorageDirectory))
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

    private string GetFilePath(string sensorId, int interval, long timestamp)
    {
        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.Date;
        var fileName = $"{date:yyyy-MM-dd}_{interval}";
        return Path.Combine(_options.StorageDirectory, sensorId, $"{fileName}.bin");
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
}