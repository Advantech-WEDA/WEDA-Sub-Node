using System.Collections.Concurrent;

namespace SystemAgentExample.Protocols;

/// <summary>
/// Remembers which enabled sensors currently produce no value, so the parser warns
/// once when a sensor stops producing values instead of on every poll — and warns
/// again if it recovers and later fails anew.
/// </summary>
internal sealed class MissingValueTracker
{
    private readonly ConcurrentDictionary<string, byte> _missing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Records a poll without a value.</summary>
    /// <returns>True for the first miss since the sensor last produced a value.</returns>
    public bool MarkMissing(string sensorName) => _missing.TryAdd(sensorName, 0);

    /// <summary>Records a poll that produced a value.</summary>
    public void MarkPresent(string sensorName) => _missing.TryRemove(sensorName, out _);
}
