using System.Collections.Concurrent;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Telemetry;

/// <summary>
/// Per-sensor cache for buffering telemetry data.
/// Acts like a Modbus register - stores the latest value pushed from external sources.
/// Device samples from this cache at configured intervals (SensorConfig.Interval).
/// Thread-safe for concurrent push (from external sources) and read (from device sampling).
/// </summary>
public class SensorCache
{
    private readonly ConcurrentDictionary<string, SensorCacheEntry> _cache = new();

    /// <summary>
    /// Push a telemetry measure into the cache.
    /// Overwrites any existing value for the same ResourceId.
    /// Called by external data sources (MQTT, WebSocket, etc.).
    /// </summary>
    /// <param name="measure">Telemetry measure to cache</param>
    public void Push(TelemetryMeasure measure)
    {
        var entry = new SensorCacheEntry
        {
            Measure = measure,
            ReceivedAt = DateTimeOffset.UtcNow
        };
        _cache.AddOrUpdate(measure.ResourceId, entry, (_, _) => entry);
    }

    /// <summary>
    /// Push multiple telemetry measures into the cache.
    /// </summary>
    /// <param name="measures">Telemetry measures to cache</param>
    public void Push(IEnumerable<TelemetryMeasure> measures)
    {
        foreach (var measure in measures)
        {
            Push(measure);
        }
    }

    /// <summary>
    /// Read the latest value for a specific sensor.
    /// Returns null if no data has been cached for this sensor.
    /// </summary>
    /// <param name="resourceId">Sensor resource ID</param>
    /// <returns>Latest cached measure or null</returns>
    public TelemetryMeasure? Read(string resourceId)
    {
        return _cache.TryGetValue(resourceId, out var entry) ? entry.Measure : null;
    }

    /// <summary>
    /// Read all cached sensor values.
    /// Returns the latest value for each sensor that has data.
    /// </summary>
    /// <returns>List of latest measures</returns>
    public List<TelemetryMeasure> ReadAll()
    {
        return _cache.Values.Select(e => e.Measure).ToList();
    }

    /// <summary>
    /// Read values for specific sensors.
    /// Only returns sensors that have cached data.
    /// </summary>
    /// <param name="resourceIds">Sensor resource IDs to read</param>
    /// <returns>List of measures for sensors that have data</returns>
    public List<TelemetryMeasure> Read(IEnumerable<string> resourceIds)
    {
        var result = new List<TelemetryMeasure>();
        foreach (var resourceId in resourceIds)
        {
            if (_cache.TryGetValue(resourceId, out var entry))
            {
                result.Add(entry.Measure);
            }
        }
        return result;
    }

    /// <summary>
    /// Check if a sensor has cached data.
    /// </summary>
    /// <param name="resourceId">Sensor resource ID</param>
    /// <returns>True if data exists</returns>
    public bool HasData(string resourceId)
    {
        return _cache.ContainsKey(resourceId);
    }

    /// <summary>
    /// Get the timestamp when a sensor's data was last received.
    /// </summary>
    /// <param name="resourceId">Sensor resource ID</param>
    /// <returns>Timestamp or null if no data</returns>
    public DateTimeOffset? GetLastReceivedTime(string resourceId)
    {
        return _cache.TryGetValue(resourceId, out var entry) ? entry.ReceivedAt : null;
    }

    /// <summary>
    /// Clear all cached data.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Clear cached data for a specific sensor.
    /// </summary>
    /// <param name="resourceId">Sensor resource ID</param>
    public void Clear(string resourceId)
    {
        _cache.TryRemove(resourceId, out _);
    }

    /// <summary>
    /// Number of sensors with cached data.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Internal cache entry with metadata.
    /// </summary>
    private class SensorCacheEntry
    {
        public required TelemetryMeasure Measure { get; init; }
        public required DateTimeOffset ReceivedAt { get; init; }
    }
}
