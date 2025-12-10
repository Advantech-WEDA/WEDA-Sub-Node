using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using SystemMonitorExample.Models;

namespace SystemMonitorExample.Communication;

/// <summary>
/// Collects raw system resource metrics from operating system APIs.
/// This is the "communication layer" that interfaces with the local system.
/// Returns raw data that will be converted to TelemetryMeasure by SystemMetricsParser.
/// Part of the Communication layer.
/// </summary>
public class SystemResourceCollector
{
    private readonly ILogger _logger;

    // Track boot time (calculated once)
    private static readonly long BootTimeSeconds = CalculateBootTimeSeconds();

    public SystemResourceCollector(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Collects all system metrics and returns raw data.
    /// </summary>
    public async Task<SystemMetricsRawData> CollectAllMetricsAsync(CancellationToken ct)
    {
        return await CollectMetricsAsync(["cpu", "memory", "disk", "network", "system", "gpu"], ct);
    }

    /// <summary>
    /// Collects only the requested metric categories and returns raw data.
    /// This is more efficient when only specific metrics are needed.
    /// </summary>
    /// <param name="metricTypes">Set of metric types to collect: "cpu", "memory", "disk", "network", "system", "gpu"</param>
    /// <param name="ct">Cancellation token</param>
    public async Task<SystemMetricsRawData> CollectMetricsAsync(HashSet<string> metricTypes, CancellationToken ct)
    {
        var rawData = new SystemMetricsRawData
        {
            Timestamp = DateTimeOffset.UtcNow
        };

        var tasks = new List<Task>();

        // Only collect the requested metric categories
        Task<CpuMetrics>? cpuTask = null;
        Task<RamMetrics>? ramTask = null;
        Task<List<DiskMetrics>>? diskTask = null;
        Task<List<NetworkMetrics>>? networkTask = null;
        Task<SystemMetrics>? systemTask = null;
        Task<GpuMetrics>? gpuTask = null;

        if (metricTypes.Contains("cpu"))
        {
            cpuTask = CollectCpuMetricsAsync(ct);
            tasks.Add(cpuTask);
        }

        if (metricTypes.Contains("memory"))
        {
            ramTask = CollectRamMetricsAsync(ct);
            tasks.Add(ramTask);
        }

        if (metricTypes.Contains("disk"))
        {
            diskTask = CollectDiskMetricsAsync(ct);
            tasks.Add(diskTask);
        }

        if (metricTypes.Contains("network"))
        {
            networkTask = Task.Run(() => CollectNetworkMetrics(), ct);
            tasks.Add(networkTask);
        }

        if (metricTypes.Contains("system"))
        {
            systemTask = CollectSystemMetricsAsync(ct);
            tasks.Add(systemTask);
        }

        if (metricTypes.Contains("gpu"))
        {
            gpuTask = Task.Run(() => CollectGpuMetrics(), ct);
            tasks.Add(gpuTask);
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }

        // Assign results from completed tasks
        if (cpuTask != null) rawData.Cpu = await cpuTask;
        if (ramTask != null) rawData.Ram = await ramTask;
        if (diskTask != null) rawData.Disks = await diskTask;
        if (networkTask != null) rawData.Networks = await networkTask;
        if (systemTask != null) rawData.System = await systemTask;
        if (gpuTask != null) rawData.Gpu = await gpuTask;

        return rawData;
    }

    private async Task<CpuMetrics> CollectCpuMetricsAsync(CancellationToken ct)
    {
        var metrics = new CpuMetrics();

        try
        {
            if (OperatingSystem.IsLinux())
            {
                // Read /proc/stat for CPU times
                var statContent = await File.ReadAllTextAsync("/proc/stat", ct);
                metrics.Cores = ParseLinuxCpuStats(statContent);

                // Read context switches from /proc/stat
                metrics.ContextSwitchesTotal = ParseContextSwitches(statContent);

                // Read load averages from /proc/loadavg
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
                // macOS: Use sysctl for load averages
                metrics.Cores = GetMacOSCpuCores();
                var loadAvg = GetMacOSLoadAverage();
                metrics.Load1 = loadAvg.load1;
                metrics.Load5 = loadAvg.load5;
                metrics.Load15 = loadAvg.load15;
            }
            else if (OperatingSystem.IsWindows())
            {
                // Windows: Basic CPU info
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
            // Match cpu0, cpu1, etc. (not the aggregate "cpu" line)
            if (line.StartsWith("cpu") && line.Length > 3 && char.IsDigit(line[3]))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 8)
                {
                    var coreNum = int.Parse(parts[0][3..]);
                    // Values are in USER_HZ (typically 100), convert to seconds
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

        // macOS doesn't expose per-core stats easily without native code
        // Return placeholder cores with zero values
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
            // Use getloadavg via process
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

                // Output format: "{ 1.23 4.56 7.89 }"
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

    private async Task<RamMetrics> CollectRamMetricsAsync(CancellationToken ct)
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

    private async Task<List<DiskMetrics>> CollectDiskMetricsAsync(CancellationToken ct)
    {
        var diskList = new List<DiskMetrics>();

        try
        {
            // Collect filesystem metrics from DriveInfo (cross-platform)
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives.Where(d => d.IsReady))
            {
                var deviceName = NormalizeDriveName(drive.Name);
                var disk = new DiskMetrics
                {
                    DeviceName = deviceName,
                    MountPoint = drive.Name,
                    FilesystemAvailBytes = drive.AvailableFreeSpace,
                    FilesystemFreeBytes = drive.TotalFreeSpace
                };

                diskList.Add(disk);
            }

            // Linux-specific: Read I/O stats from /proc/diskstats
            if (OperatingSystem.IsLinux())
            {
                var diskStats = await ReadLinuxDiskStatsAsync(ct);
                foreach (var disk in diskList)
                {
                    if (diskStats.TryGetValue(disk.DeviceName, out var stats))
                    {
                        disk.ReadsCompletedTotal = stats.ReadsCompleted;
                        disk.WritesCompletedTotal = stats.WritesCompleted;
                        disk.ReadBytesTotal = stats.SectorsRead * 512;
                        disk.WrittenBytesTotal = stats.SectorsWritten * 512;
                        disk.IOTimeSecondsTotal = stats.IoTimeMs / 1000;
                    }
                }

                // Read inode info from statfs
                foreach (var disk in diskList)
                {
                    var inodeInfo = GetLinuxInodeInfo(disk.MountPoint);
                    disk.FilesystemFiles = inodeInfo.total;
                    disk.FilesystemFilesFree = inodeInfo.free;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect disk metrics");
        }

        return diskList;
    }

    private static async Task<Dictionary<string, DiskIoStats>> ReadLinuxDiskStatsAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, DiskIoStats>();

        try
        {
            var lines = await File.ReadAllLinesAsync("/proc/diskstats", ct);
            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 14)
                {
                    var deviceName = parts[2];
                    result[deviceName] = new DiskIoStats
                    {
                        ReadsCompleted = long.Parse(parts[3]),
                        SectorsRead = long.Parse(parts[5]),
                        WritesCompleted = long.Parse(parts[7]),
                        SectorsWritten = long.Parse(parts[9]),
                        IoTimeMs = long.Parse(parts[12])
                    };
                }
            }
        }
        catch
        {
            // Ignore errors reading disk stats
        }

        return result;
    }

    private static (long total, long free) GetLinuxInodeInfo(string _)
    {
        // This would require P/Invoke to statvfs, return 0 for now
        return (0, 0);
    }

    private List<NetworkMetrics> CollectNetworkMetrics()
    {
        var networks = new List<NetworkMetrics>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var iface in interfaces.Where(i => i.OperationalStatus == OperationalStatus.Up))
            {
                var stats = iface.GetIPv4Statistics();
                networks.Add(new NetworkMetrics
                {
                    InterfaceName = SanitizeInterfaceName(iface.Name),
                    ReceiveBytesTotal = stats.BytesReceived,
                    TransmitBytesTotal = stats.BytesSent,
                    ReceivePacketsTotal = stats.UnicastPacketsReceived + stats.NonUnicastPacketsReceived,
                    TransmitPacketsTotal = stats.UnicastPacketsSent + stats.NonUnicastPacketsSent,
                    ReceiveErrsTotal = stats.IncomingPacketsWithErrors,
                    TransmitErrsTotal = stats.OutgoingPacketsWithErrors
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect network metrics");
        }

        return networks;
    }

    private async Task<SystemMetrics> CollectSystemMetricsAsync(CancellationToken ct)
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
                // Read /proc/stat for procs_running, procs_blocked, intr
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

                // Read file descriptor info from /proc/sys/fs/file-nr
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

    private GpuMetrics CollectGpuMetrics()
    {
        var metrics = new GpuMetrics();

        try
        {
            // GPU metrics typically require NVIDIA SMI or similar tools
            // For now, return 0 - can be extended with nvidia-smi parsing
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

    private static long CalculateBootTimeSeconds()
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                // Read /proc/stat for btime
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

            // Fallback: Calculate from uptime
            var uptime = Environment.TickCount64 / 1000;
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() - uptime;
        }
        catch
        {
            return 0;
        }
    }

    private static string NormalizeDriveName(string driveName)
    {
        var normalized = driveName.TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(normalized) || normalized == "/")
            return "root";
        if (normalized.EndsWith(':'))
            return normalized.TrimEnd(':').ToLowerInvariant();
        return normalized.Replace("/", "_").TrimStart('_');
    }

    private static string SanitizeInterfaceName(string name)
    {
        return name.Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
    }

    private record struct DiskIoStats
    {
        public long ReadsCompleted { get; init; }
        public long SectorsRead { get; init; }
        public long WritesCompleted { get; init; }
        public long SectorsWritten { get; init; }
        public long IoTimeMs { get; init; }
    }
}
