using System.Diagnostics;

using Microsoft.Extensions.Logging;

using SystemAgentExample.Models;

namespace SystemAgentExample.Communication.Collectors;

public class GpuCollector
{
    private readonly ILogger _logger;

    public GpuCollector(ILogger logger)
    {
        _logger = logger;
    }

    public GpuMetrics CollectGpuMetrics()
    {
        var metrics = new GpuMetrics();

        try
        {
            if (OperatingSystem.IsLinux())
            {
                var utilization = TryGetNvidiaGpuUtilization();
                metrics.Utilization = utilization;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect GPU metrics");
        }

        return metrics;
    }

    private static int TryGetNvidiaGpuUtilization()
    {
        try
        {
            var psi = new ProcessStartInfo("nvidia-smi", "--query-gpu=utilization.gpu --format=csv,noheader,nounits")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (int.TryParse(output, out var utilization))
                {
                    return utilization;
                }
            }
        }
        catch
        {
            // nvidia-smi not available or failed
        }

        return 0;
    }
}
