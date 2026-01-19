using System.Collections.Concurrent;
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
        var key = $"{sensorId}:{startOfDay}";

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

    private static long GetStartOfDay(long timestamp)
    {
        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.Date;
        return new DateTimeOffset(date, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }
}