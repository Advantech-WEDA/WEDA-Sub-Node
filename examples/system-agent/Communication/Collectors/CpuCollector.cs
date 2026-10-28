using System.Diagnostics;
using System.Globalization;

using Microsoft.Extensions.Logging;

using SystemAgentExample.Models;

namespace SystemAgentExample.Communication.Collectors;

public class CpuCollector
{
    private const string ProcStatPath = "/proc/stat";

    /// <summary>
    /// Shortest window a usage figure is computed over. Reads closer together than
    /// this repeat the last figure (see <see cref="CpuUsageTracker"/>).
    /// </summary>
    private static readonly TimeSpan MinimumUsageWindow = TimeSpan.FromSeconds(1);

    private readonly ILogger _logger;
    private readonly CpuUsageTracker _usageTracker = new(MinimumUsageWindow);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly object _usageLock = new();

    public CpuCollector(ILogger logger)
    {
        _logger = logger;
        PrimeUsageBaseline();
    }

    /// <summary>
    /// Collects CPU usage, load averages and context switches.
    /// </summary>
    /// <remarks>
    /// On Linux a failure to read or parse /proc/stat or /proc/loadavg is thrown, so
    /// the caller's retry policy runs and the cpu metrics are reported as failed
    /// rather than as zeros. Platforms without a usage source leave
    /// <see cref="CpuMetrics.UsagePercent"/> null.
    /// </remarks>
    public async Task<CpuMetrics> CollectCpuMetricsAsync(CancellationToken ct)
    {
        var metrics = new CpuMetrics();

        if (OperatingSystem.IsLinux())
        {
            var statContent = await File.ReadAllTextAsync(ProcStatPath, ct);
            var sample = CpuTimes.ParseAggregate(statContent)
                ?? throw new InvalidDataException($"{ProcStatPath} has no parsable aggregate 'cpu' line");
            metrics.UsagePercent = UpdateUsage(sample);
            metrics.ContextSwitchesTotal = ParseContextSwitches(statContent);

            var loadContent = await File.ReadAllTextAsync("/proc/loadavg", ct);
            var loadParts = loadContent.Split(' ');
            if (loadParts.Length >= 3)
            {
                metrics.Load1 = double.Parse(loadParts[0], CultureInfo.InvariantCulture);
                metrics.Load5 = double.Parse(loadParts[1], CultureInfo.InvariantCulture);
                metrics.Load15 = double.Parse(loadParts[2], CultureInfo.InvariantCulture);
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            var loadAvg = GetMacOSLoadAverage();
            metrics.Load1 = loadAvg.load1;
            metrics.Load5 = loadAvg.load5;
            metrics.Load15 = loadAvg.load15;
        }

        return metrics;
    }

    /// <summary>
    /// Takes the first usage sample at start-up, so the first collection already
    /// covers a full window instead of reporting nothing.
    /// </summary>
    private void PrimeUsageBaseline()
    {
        if (!OperatingSystem.IsLinux())
            return;

        try
        {
            if (CpuTimes.ParseAggregate(File.ReadAllText(ProcStatPath)) is { } sample)
                UpdateUsage(sample);
        }
        catch (Exception ex)
        {
            // Not fatal: the first collection becomes the baseline instead
            _logger.LogWarning(ex, "Failed to take the initial CPU usage sample from {Path}", ProcStatPath);
        }
    }

    private double? UpdateUsage(CpuTimes sample)
    {
        lock (_usageLock)
        {
            return _usageTracker.Update(sample, _clock.Elapsed);
        }
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
                        double.Parse(parts[0], CultureInfo.InvariantCulture),
                        double.Parse(parts[1], CultureInfo.InvariantCulture),
                        double.Parse(parts[2], CultureInfo.InvariantCulture)
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
}
