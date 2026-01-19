using System.Collections.Concurrent;
using ErrorOr;
using Microsoft.Extensions.Options;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Core.Storage;

public class RecordingService : IRecordingService, IDisposable
{
    private readonly IRecordStorage _storage;
    private readonly RecordingOptions _options;
    private readonly ConcurrentDictionary<string, int> _lastRecordedslot = new();
    private readonly ConcurrentDictionary<string, List<RecordingDataPoint>> _batchBuffers = new();
    private readonly Timer? _flushTimer;
    private readonly object _batchLock = new();
    private bool _enabled;
    private bool _batchEnabled;
    private int _batchMaxSamples;
    private bool _disposed;

    public RecordingService(IRecordStorage storage, IOptions<RecordingOptions> options)
    {
        _storage = storage;
        _options = options.Value;
        _enabled = options.Value.Enabled;
        _batchMaxSamples = Math.Max(1, options.Value.BatchMaxSamples);
        _batchEnabled = options.Value.BatchEnabled;

        if (_batchEnabled && _options.FlushIntervalSeconds > 0)
        {
            _flushTimer = new Timer(
                callback: async _ => await ExecutePeriodicFlush(),
                state: null,
                dueTime: TimeSpan.FromSeconds(_options.FlushIntervalSeconds),
                period: TimeSpan.FromSeconds(_options.FlushIntervalSeconds)
            );
        }
    }

    private async Task ExecutePeriodicFlush()
    {
        try
        {
            await FlushAsync();
        }
        catch
        {
            // Ignore flush errors in timer callback
        }
    }

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
        if (!_enabled || !ShouldRecord(sensorId, interval, timestamp))
            return;

        var dataPoint = new RecordingDataPoint(timestamp, value);

        if (!_batchEnabled)
        {
            await _storage.WriteAsync(sensorId, interval, dataPoint, cancellationToken);
            return;
        }

        var bufferKey = $"{sensorId}:{interval}";
        List<RecordingDataPoint>? batchToWrite = null;

        lock (_batchLock)
        {
            var buffer = _batchBuffers.GetOrAdd(bufferKey, _ => new List<RecordingDataPoint>());
            buffer.Add(dataPoint);

            if (buffer.Count >= _batchMaxSamples)
            {
                batchToWrite = [.. buffer];
                buffer.Clear();
            }
        }

        if (batchToWrite != null)
        {
            await _storage.WriteBatchAsync(sensorId, interval, batchToWrite, cancellationToken);
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        List<(string sensorId, int interval, List<RecordingDataPoint> dataPoints)> buffersToFlush;

        lock (_batchLock)
        {
            buffersToFlush = _batchBuffers
                .Where(kvp => kvp.Value.Count > 0)
                .Select(kvp =>
                {
                    var parts = kvp.Key.Split(':');
                    var sensorId = parts[0];
                    var interval = int.Parse(parts[1]);
                    var dataPoints = new List<RecordingDataPoint>(kvp.Value);
                    kvp.Value.Clear();
                    return (sensorId, interval, dataPoints);
                })
                .ToList();
        }

        foreach (var (sensorId, interval, dataPoints) in buffersToFlush)
        {
            await _storage.WriteBatchAsync(sensorId, interval, dataPoints, cancellationToken);
        }
    }

    public async Task FlushSensorAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        List<(int interval, List<RecordingDataPoint> dataPoints)> buffersToFlush;

        lock (_batchLock)
        {
            buffersToFlush = _batchBuffers
                .Where(kvp => kvp.Key.StartsWith($"{sensorId}:") && kvp.Value.Count > 0)
                .Select(kvp =>
                {
                    var parts = kvp.Key.Split(':');
                    var interval = int.Parse(parts[1]);
                    var dataPoints = new List<RecordingDataPoint>(kvp.Value);
                    kvp.Value.Clear();
                    return (interval, dataPoints);
                })
                .ToList();
        }

        foreach (var (interval, dataPoints) in buffersToFlush)
        {
            await _storage.WriteBatchAsync(sensorId, interval, dataPoints, cancellationToken);
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

    public void UpdateBatchSettings(bool batchEnabled, int batchMaxSamples)
    {
        _batchEnabled = batchEnabled;
        _batchMaxSamples = batchMaxSamples;
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
    }

    private static long GetStartOfDay(long timestamp)
    {
        var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.Date;
        return new DateTimeOffset(date, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _flushTimer?.Dispose();
        _disposed = true;
    }
}
