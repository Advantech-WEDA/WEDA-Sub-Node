using System.Collections.Concurrent;
using ErrorOr;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Weda.SubNode.Abstractions.Common;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Storage;

public class RecordingService : IRecordingService, IAsyncDisposable, IDisposable
{
    private readonly ILogger<RecordingService> _logger;
    private readonly IRecordStorage _storage;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly RecordingOptions _options;
    private readonly ConcurrentDictionary<string, int> _lastRecordedslot = new();
    private readonly Dictionary<string, List<RecordingDataPoint>> _batchBuffers = new();
    private readonly Timer? _flushTimer;
    private readonly object _batchLock = new();
    private volatile bool _enabled;
    private volatile bool _batchEnabled;
    private volatile int _batchMaxSamples;
    private volatile bool _disposed;

    public RecordingService(ILogger<RecordingService> logger, IRecordStorage storage, IDeviceRegistry deviceRegistry, IOptions<RecordingOptions> options)
    {
        _logger = logger;
        _storage = storage;
        _deviceRegistry = deviceRegistry;
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
        catch (Exception ex)
        {
            _logger.LogWarning("[RecordingService] Periodic flush failed - data may be lost: {Message}", ex.Message);
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

    public async Task RecordAsync(string sensorId, int interval, SchemaType schemaType, long timestamp, object value, CancellationToken cancellationToken = default)
    {
        if (!_enabled || !ShouldRecord(sensorId, interval, timestamp))
            return;

        try
        {
            var dataPoint = new RecordingDataPoint(timestamp, value, schemaType);

            if (!_batchEnabled)
            {
                await _storage.WriteAsync(sensorId, interval, schemaType, dataPoint, cancellationToken);
                return;
            }

            var bufferKey = $"{sensorId}:{interval}:{schemaType}";
            List<RecordingDataPoint>? batchToWrite = null;

            lock (_batchLock)
            {
                if (!_batchBuffers.TryGetValue(bufferKey, out var buffer))
                {
                    buffer = new List<RecordingDataPoint>();
                    _batchBuffers[bufferKey] = buffer;
                }
                buffer.Add(dataPoint);

                if (buffer.Count >= _batchMaxSamples)
                {
                    batchToWrite = [.. buffer];
                    buffer.Clear();
                }
            }

            if (batchToWrite != null)
            {
                await _storage.WriteBatchAsync(sensorId, interval, schemaType, batchToWrite, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record data for sensor {SensorId} at timestamp {Timestamp}", sensorId, timestamp);
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        List<(string sensorId, int interval, SchemaType schemaType, List<RecordingDataPoint> dataPoints)> buffersToFlush;

        lock (_batchLock)
        {
            buffersToFlush = _batchBuffers
                .Where(kvp => kvp.Value.Count > 0)
                .Select(kvp =>
                {
                    var parts = kvp.Key.Split(':');
                    var sensorId = parts[0];
                    var interval = int.Parse(parts[1]);
                    var schemaType = Enum.Parse<SchemaType>(parts[2]);
                    var dataPoints = new List<RecordingDataPoint>(kvp.Value);
                    kvp.Value.Clear();
                    return (sensorId, interval, schemaType, dataPoints);
                })
                .ToList();
        }

        foreach (var (sensorId, interval, schemaType, dataPoints) in buffersToFlush)
        {
            await _storage.WriteBatchAsync(sensorId, interval, schemaType, dataPoints, cancellationToken);
        }
    }

    public async Task FlushSensorAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        List<(int interval, SchemaType schemaType, List<RecordingDataPoint> dataPoints)> buffersToFlush;

        lock (_batchLock)
        {
            buffersToFlush = _batchBuffers
                .Where(kvp => kvp.Key.StartsWith($"{sensorId}:") && kvp.Value.Count > 0)
                .Select(kvp =>
                {
                    var parts = kvp.Key.Split(':');
                    var interval = int.Parse(parts[1]);
                    var schemaType = Enum.Parse<SchemaType>(parts[2]);
                    var dataPoints = new List<RecordingDataPoint>(kvp.Value);
                    kvp.Value.Clear();
                    return (interval, schemaType, dataPoints);
                })
                .ToList();
        }

        foreach (var (interval, schemaType, dataPoints) in buffersToFlush)
        {
            await _storage.WriteBatchAsync(sensorId, interval, schemaType, dataPoints, cancellationToken);
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

    public async Task<ErrorOr<PagedResult<RecordingSensorDto>>> GetSensorsAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        try
        {
            var pagedSensorIds = await _storage.GetSensorsAsync(pageIndex, pageSize, cancellationToken);

            // Build a lookup of all sensors from registered devices (by ShortId)
            var sensorLookup = _deviceRegistry.GetAllDevices()
                .SelectMany(d => d.Configuration.Sensors)
                .ToDictionary(s => s.ShortId, s => s);

            // Map sensor IDs to RecordingSensorDto
            var sensorDtos = pagedSensorIds.Items
                .Select(shortId => sensorLookup.TryGetValue(shortId, out var sensor)
                    ? RecordingSensorDto.FromSensor(sensor)
                    : null)
                .Where(dto => dto != null)
                .Cast<RecordingSensorDto>()
                .ToList();

            return new PagedResult<RecordingSensorDto>(
                sensorDtos,
                pagedSensorIds.TotalCount,
                pagedSensorIds.PageIndex,
                pagedSensorIds.PageSize);
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
            var intervals = await _storage.GetIntervalsAsync(sensorId, cancellationToken);
            if (intervals.Count == 0)
            {
                return Errors.Recording.SensorNotFound(sensorId);
            }

            var measures = new List<RecordingMeasureResult>();

            foreach (var interval in intervals)
            {
                var data = await _storage.ReadAsync(sensorId, interval, start, end, cancellationToken);
                if (data.Count == 0)
                    continue;

                measures.Add(new RecordingMeasureResult(
                    Interval: interval,
                    StartTimeStamp: data[0].Timestamp,
                    SchemaType: data[0].SchemaType,
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
            var intervals = await _storage.GetIntervalsAsync(sensorId, cancellationToken);
            if (intervals.Count == 0)
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

        // Flush remaining data before disposal
        try

        {
            FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Final flush failed during disposal - some data might be lost");
        }
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _flushTimer?.Dispose();

        try
        {
            await FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Final flush failed during disposal - some data might be lost");
        }
        _disposed = true;
        GC.SuppressFinalize(this);
    }

}
