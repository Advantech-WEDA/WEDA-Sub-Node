using System.Collections.Concurrent;
using ErrorOr;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Core.Storage;

public class RecordingService(IRecordStorage storage) : IRecordingService
{
    private readonly IRecordStorage _storage = storage;
    private readonly ConcurrentDictionary<string, int> _lastRecordedslot = new();

    public bool ShouldRecord(string sensorId, int interval, long timestamp)
    {
        var startOfDay = GetStartOfDay(timestamp);
        var slotIndex = (int)((timestamp - startOfDay) / interval);
        var key = $"{sensorId}:{interval}:{startOfDay}";

        var lastSlot = _lastRecordedslot.GetOrAdd(key, -1);
        if (slotIndex > lastSlot)
        {
            _lastRecordedslot[key] = slotIndex;
            return true;
        }
        return false;
    }

    public async Task RecordAsync(string sensorId, int interval, long timestamp, double value, CancellationToken cancellationToken = default)
    {
        if (ShouldRecord(sensorId, interval, timestamp))
        {
            await _storage.WriteAsync(sensorId, interval, new RecordingDataPoint(timestamp, value), cancellationToken);
        }
    }

    public async Task CleanupAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
    {
        await _storage.CleanupAsync(before, cancellationToken);
    }

    public async Task<ErrorOr<IReadOnlyList<string>>> GetSensorIdsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var sensorIds = await _storage.GetSensorIdsAsync(cancellationToken);
            return sensorIds.ToList();
        }
        catch (Exception ex)
        {
            return Errors.Recording.StorageError(ex);
        }
    }

    public async Task<ErrorOr<RecordingResult>> GetRecordingsAsync(
        string sensorId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sensorIds = await _storage.GetSensorIdsAsync(cancellationToken);
            if (!sensorIds.Contains(sensorId))
            {
                return Errors.Recording.SensorNotFound(sensorId);
            }

            var intervals = await _storage.GetIntervalsAsync(sensorId, cancellationToken);
            var measures = new List<RecordingMeasureResult>();

            foreach (var interval in intervals)
            {
                var data = await _storage.ReadAsync(sensorId, interval, start, end, cancellationToken);
                if (data.Count == 0)
                    continue;

                measures.Add(new RecordingMeasureResult(
                    Interval: interval,
                    StartTimeStamp: data[0].Timestamp,
                    Values: data.Select(d => d.Value).ToList()));
            }

            return new RecordingResult(sensorId, measures);
        }
        catch (Exception ex)
        {
            return Errors.Recording.StorageError(ex);
        }
    }

    public async Task<ErrorOr<Deleted>> DeleteSensorAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sensorIds = await _storage.GetSensorIdsAsync(cancellationToken);
            if (!sensorIds.Contains(sensorId))
            {
                return Errors.Recording.SensorNotFound(sensorId);
            }

            await _storage.DeleteSensorAsync(sensorId, cancellationToken);

            // Clear cached slot tracking for this sensor
            var keysToRemove = _lastRecordedslot.Keys.Where(k => k.StartsWith($"{sensorId}:")).ToList();
            foreach (var key in keysToRemove)
            {
                _lastRecordedslot.TryRemove(key, out _);
            }

            return Result.Deleted;
        }
        catch (Exception ex)
        {
            return Errors.Recording.StorageError(ex);
        }
    }

    public async Task<ErrorOr<Deleted>> DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _storage.DeleteAllAsync(cancellationToken);
            _lastRecordedslot.Clear();
            return Result.Deleted;
        }
        catch (Exception ex)
        {
            return Errors.Recording.StorageError(ex);
        }
    }

    private static long GetStartOfDay(long timestamp)
    {
        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.Date;
        return new DateTimeOffset(date, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }
}
