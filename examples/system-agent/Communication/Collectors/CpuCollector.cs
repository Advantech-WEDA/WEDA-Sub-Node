using System.Diagnostics;

using Microsoft.Extensions.Logging;

using SystemAgentExample.Models;

namespace SystemAgentExample.Communication.Collectors;

public class CpuCollector
{
    private readonly ILogger _logger;

    public CpuCollector(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<CpuMetrics> CollectCpuMetricsAsync(CancellationToken ct)
    {
        var metrics = new CpuMetrics();

        try
        {
            if (OperatingSystem.IsLinux())
            {
                var statContent = await File.ReadAllTextAsync("/proc/stat", ct);
                metrics.Cores = ParseLinuxCpuStats(statContent);
                metrics.ContextSwitchesTotal = ParseContextSwitches(statContent);

                var loadContent = await File.ReadAllTextAsync("/proc/loadavg", ct);
                var loadParts = loadContent.Split(' ');
                if (loadParts.Length >= 3)
                {
                    metrics.Load1 = double.Parse(loadParts[0]);
                    metrics.Load5 = double.Parse(loadParts[1]);
                    metrics.Load15 = double.Parse(loadParts[2]);
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                metrics.Cores = GetMacOSCpuCores();
                var loadAvg = GetMacOSLoadAverage();
                metrics.Load1 = loadAvg.load1;
                metrics.Load5 = loadAvg.load5;
                metrics.Load15 = loadAvg.load15;
            }
            else if (OperatingSystem.IsWindows())
            {
                metrics.Cores = GetWindowsCpuCores();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect CPU metrics");
        }

        return metrics;
    }

    private static List<CpuCoreMetrics> ParseLinuxCpuStats(string statContent)
    {
        var cores = new List<CpuCoreMetrics>();
        var lines = statContent.Split('\n');

        foreach (var line in lines)
        {
            if (line.StartsWith("cpu") && line.Length > 3 && char.IsDigit(line[3]))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 8)
                {
                    var coreNum = int.Parse(parts[0][3..]);
                    const double userHz = 100.0;

                    cores.Add(new CpuCoreMetrics
                    {
                        CoreNumber = coreNum,
                        SecondsUser = long.Parse(parts[1]) / userHz,
                        SecondsNice = long.Parse(parts[2]) / userHz,
                        SecondsSystem = long.Parse(parts[3]) / userHz,
                        SecondsIdle = long.Parse(parts[4]) / userHz,
                        SecondsIowait = parts.Length > 5 ? long.Parse(parts[5]) / userHz : 0,
                        SecondsIrq = parts.Length > 6 ? long.Parse(parts[6]) / userHz : 0,
                        SecondsSoftirq = parts.Length > 7 ? long.Parse(parts[7]) / userHz : 0,
                        SecondsSteal = parts.Length > 8 ? long.Parse(parts[8]) / userHz : 0
                    });
                }
            }
        }

        return cores;
    }

    private static long ParseContextSwitches(string statContent)
    {
        var lines = statContent.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("ctxt "))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[1], out var ctxt))
                {
                    return ctxt;
                }
            }
        }
        return 0;
    }

    private static List<CpuCoreMetrics> GetMacOSCpuCores()
    {
        var cores = new List<CpuCoreMetrics>();
        var coreCount = Environment.ProcessorCount;

        for (int i = 0; i < coreCount; i++)
        {
            cores.Add(new CpuCoreMetrics { CoreNumber = i });
        }

        return cores;
    }

    private (double load1, double load5, double load15) GetMacOSLoadAverage()
    {
        try
        {
            var psi = new ProcessStartInfo("sysctl", "-n vm.loadavg")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var cleaned = output.Replace("{", "").Replace("}", "").Trim();
                var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    return (
                        double.Parse(parts[0]),
                        double.Parse(parts[1]),
                        double.Parse(parts[2])
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get macOS load average");
        }

        return (0, 0, 0);
    }

    private static List<CpuCoreMetrics> GetWindowsCpuCores()
    {
        var cores = new List<CpuCoreMetrics>();
        var coreCount = Environment.ProcessorCount;

        for (int i = 0; i < coreCount; i++)
        {
            cores.Add(new CpuCoreMetrics { CoreNumber = i });
        }

        return cores;
    }
}
