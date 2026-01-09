using System.Diagnostics;

using Microsoft.Extensions.Logging;

using SystemAgentExample.Models;

namespace SystemAgentExample.Communication.Collectors;

public class SystemCollector
{
    private readonly ILogger _logger;
    private static readonly long BootTimeSeconds = CalculateBootTimeSeconds();

    public SystemCollector(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<SystemMetrics> CollectSystemMetricsAsync(CancellationToken ct)
    {
        var metrics = new SystemMetrics
        {
            TimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            BootTimeSeconds = BootTimeSeconds
        };

        try
        {
            if (OperatingSystem.IsLinux())
            {
                var statContent = await File.ReadAllTextAsync("/proc/stat", ct);
                var lines = statContent.Split('\n');

                foreach (var line in lines)
                {
                    if (line.StartsWith("procs_running "))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2) metrics.ProcsRunning = int.Parse(parts[1]);
                    }
                    else if (line.StartsWith("procs_blocked "))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2) metrics.ProcsBlocked = int.Parse(parts[1]);
                    }
                    else if (line.StartsWith("intr "))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2) metrics.IntrTotal = long.Parse(parts[1]);
                    }
                }

                try
                {
                    var fileNr = await File.ReadAllTextAsync("/proc/sys/fs/file-nr", ct);
                    var parts = fileNr.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        metrics.FilefdAllocated = long.Parse(parts[0]);
                        metrics.FilefdMaximum = long.Parse(parts[2].Trim());
                    }
                }
                catch
                {
                    // Ignore errors reading file-nr
                }
            }
            else
            {
                // Cross-platform: Count processes
                metrics.ProcsRunning = Process.GetProcesses().Length;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect system metrics");
        }

        return metrics;
    }

    private static long CalculateBootTimeSeconds()
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                var content = File.ReadAllText("/proc/stat");
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("btime "))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out var btime))
                        {
                            return btime;
                        }
                    }
                }
            }

            var uptime = Environment.TickCount64 / 1000;
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() - uptime;
        }
        catch
        {
            return 0;
        }
    }
}
