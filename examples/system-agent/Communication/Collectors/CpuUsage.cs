using System.Globalization;

namespace SystemAgentExample.Communication.Collectors;

/// <summary>
/// One sample of the aggregate <c>cpu</c> line of /proc/stat, in clock ticks.
/// </summary>
/// <remarks>
/// The counters are cumulative since boot, so a usage figure needs two samples
/// (see <see cref="CpuUsageCalculator"/>). guest / guest_nice are not read: the
/// kernel already includes them in user / nice.
/// </remarks>
internal readonly record struct CpuTimes(
    long User, long Nice, long System, long Idle, long Iowait, long Irq, long Softirq, long Steal)
{
    /// <summary>Time not spent running work. iowait is idle time spent waiting for I/O.</summary>
    public long IdleTotal => Idle + Iowait;

    public long Total => User + Nice + System + Idle + Iowait + Irq + Softirq + Steal;

    /// <summary>
    /// Parses the aggregate <c>cpu</c> line. The aggregate line counts every CPU,
    /// including ones currently offline, so it keeps growing when cores are hot-plugged
    /// — unlike a sum of the per-core <c>cpuN</c> lines.
    /// </summary>
    /// <returns>The sample, or <c>null</c> when the line is missing or malformed.</returns>
    public static CpuTimes? ParseAggregate(string statContent)
    {
        foreach (var line in statContent.Split('\n'))
        {
            if (!line.StartsWith("cpu ", StringComparison.Ordinal))
                continue;

            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // user nice system idle are present on every kernel; the rest arrived later
            if (fields.Length < 5)
                return null;

            var values = new long[8];
            for (var i = 0; i < values.Length && i + 1 < fields.Length; i++)
            {
                if (!long.TryParse(fields[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out values[i]))
                    return null;
            }

            return new CpuTimes(values[0], values[1], values[2], values[3],
                values[4], values[5], values[6], values[7]);
        }

        return null;
    }
}

/// <summary>
/// CPU usage over the window between two <see cref="CpuTimes"/> samples.
/// </summary>
internal static class CpuUsageCalculator
{
    /// <summary>
    /// Returns the share of non-idle time between the samples, in percent (0–100,
    /// two decimals). Steal counts as busy, iowait as idle.
    /// </summary>
    /// <returns><c>null</c> when no time elapsed or the counters went backwards.</returns>
    public static double? Compute(CpuTimes previous, CpuTimes current)
    {
        var totalDelta = current.Total - previous.Total;
        if (totalDelta <= 0)
            return null;

        var idleDelta = current.IdleTotal - previous.IdleTotal;
        var usage = (1.0 - (double)idleDelta / totalDelta) * 100.0;

        return Math.Round(Math.Clamp(usage, 0.0, 100.0), 2);
    }
}

/// <summary>
/// Keeps the baseline sample between reads and turns successive samples into
/// usage figures.
/// </summary>
/// <remarks>
/// Every enabled cpu sensor triggers a read, so reads can arrive milliseconds apart.
/// A read inside <c>minimumWindow</c> repeats the last figure and leaves the
/// baseline where it is; the next full window is measured from that baseline. Before
/// the first figure exists, a short window is measured rather than reporting nothing.
/// Not thread-safe: callers serialise access.
/// </remarks>
internal sealed class CpuUsageTracker
{
    private readonly TimeSpan _minimumWindow;
    private CpuTimes? _baseline;
    private TimeSpan _baselineAt;
    private double? _lastUsage;

    public CpuUsageTracker(TimeSpan minimumWindow)
    {
        _minimumWindow = minimumWindow;
    }

    /// <summary>
    /// Records a sample taken at <paramref name="timestamp"/> (any monotonic clock).
    /// </summary>
    /// <returns>
    /// Usage since the baseline, the last figure when the window is too short, or
    /// <c>null</c> when there is no valid window yet.
    /// </returns>
    public double? Update(CpuTimes sample, TimeSpan timestamp)
    {
        if (_baseline is not { } baseline)
        {
            Reset(sample, timestamp, usage: null);
            return null;
        }

        // Too short to measure well: repeat the last figure. With no figure yet (the read
        // right after start-up), the short window is still better than reporting nothing.
        if (timestamp - _baselineAt < _minimumWindow && _lastUsage is not null)
            return _lastUsage;

        Reset(sample, timestamp, CpuUsageCalculator.Compute(baseline, sample));
        return _lastUsage;
    }

    private void Reset(CpuTimes sample, TimeSpan timestamp, double? usage)
    {
        _baseline = sample;
        _baselineAt = timestamp;
        _lastUsage = usage;
    }
}
