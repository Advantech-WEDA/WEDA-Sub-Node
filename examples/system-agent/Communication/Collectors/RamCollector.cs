using System.Diagnostics;

using Microsoft.Extensions.Logging;

using SystemAgentExample.Models;

namespace SystemAgentExample.Communication.Collectors;

public class RamCollector
{
    private readonly ILogger _logger;

    public RamCollector(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<RamMetrics> CollectRamMetricsAsync(CancellationToken ct)
    {
        var metrics = new RamMetrics();

        try
        {
            if (OperatingSystem.IsLinux())
            {
                var memInfo = await ReadLinuxMemInfoAsync(ct);

                metrics.MemTotalBytes = memInfo.GetValueOrDefault("MemTotal", 0) * 1024;
                metrics.MemAvailableBytes = memInfo.GetValueOrDefault("MemAvailable", 0) * 1024;
                metrics.MemFreeBytes = memInfo.GetValueOrDefault("MemFree", 0) * 1024;
                metrics.BuffersBytes = memInfo.GetValueOrDefault("Buffers", 0) * 1024;
                metrics.CachedBytes = memInfo.GetValueOrDefault("Cached", 0) * 1024;
                metrics.SwapTotalBytes = memInfo.GetValueOrDefault("SwapTotal", 0) * 1024;
                metrics.SwapFreeBytes = memInfo.GetValueOrDefault("SwapFree", 0) * 1024;
            }
            else
            {
                // Cross-platform fallback using GC info
                var gcInfo = GC.GetGCMemoryInfo();
                metrics.MemTotalBytes = gcInfo.TotalAvailableMemoryBytes;
                metrics.MemAvailableBytes = gcInfo.TotalAvailableMemoryBytes;
                metrics.MemFreeBytes = gcInfo.TotalAvailableMemoryBytes - Process.GetCurrentProcess().WorkingSet64;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect RAM metrics");
        }

        return metrics;
    }

    private static async Task<Dictionary<string, long>> ReadLinuxMemInfoAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, long>();
        var lines = await File.ReadAllLinesAsync("/proc/meminfo", ct);

        foreach (var line in lines)
        {
            var parts = line.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                var key = parts[0];
                var value = parts[1].Replace(" kB", "").Trim();
                if (long.TryParse(value, out var numValue))
                {
                    result[key] = numValue;
                }
            }
        }

        return result;
    }
}
