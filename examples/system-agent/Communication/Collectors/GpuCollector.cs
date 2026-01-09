using ManagedCuda.Nvml;

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
            if (NvmlNativeMethods.nvmlInit() != nvmlReturn.Success) return 0;

            try
            {
                nvmlDevice device = default;

                if (NvmlNativeMethods.nvmlDeviceGetHandleByIndex(0, ref device) != nvmlReturn.Success) return 0;

                nvmlUtilization utilization = default;
                if (NvmlNativeMethods.nvmlDeviceGetUtilizationRates(device, ref utilization) != nvmlReturn.Success) return 0;

                return (int)utilization.gpu; // 0~100
            }
            finally
            {
                NvmlNativeMethods.nvmlShutdown();
            }
        }
        catch
        {
            return 0;
        }
    }

}
