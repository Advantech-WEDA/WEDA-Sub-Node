namespace SystemMonitorExample.Models;

/// <summary>
/// Raw system metrics data collected from operating system APIs.
/// This is the "raw data" that the Parser will convert to TelemetryMeasure.
/// Based on node_exporter metrics format.
/// </summary>
public class SystemMetricsRawData
{
    public CpuMetrics Cpu { get; set; } = new();
    public GpuMetrics Gpu { get; set; } = new();
    public RamMetrics Ram { get; set; } = new();
    public List<DiskMetrics> Disks { get; set; } = [];
    public List<NetworkMetrics> Networks { get; set; } = [];
    public SystemMetrics System { get; set; } = new();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// CPU metrics per core.
/// </summary>
public class CpuCoreMetrics
{
    /// <summary>
    /// CPU core number (0-based).
    /// </summary>
    public int CoreNumber { get; set; }

    /// <summary>
    /// Time spent in user mode (seconds).
    /// </summary>
    public double SecondsUser { get; set; }

    /// <summary>
    /// Time spent in nice priority (seconds).
    /// </summary>
    public double SecondsNice { get; set; }

    /// <summary>
    /// Time spent in system/kernel mode (seconds).
    /// </summary>
    public double SecondsSystem { get; set; }

    /// <summary>
    /// Time spent idle (seconds).
    /// </summary>
    public double SecondsIdle { get; set; }

    /// <summary>
    /// Time spent waiting for I/O (seconds).
    /// </summary>
    public double SecondsIowait { get; set; }

    /// <summary>
    /// Time spent handling hardware IRQs (seconds).
    /// </summary>
    public double SecondsIrq { get; set; }

    /// <summary>
    /// Time spent handling software IRQs (seconds).
    /// </summary>
    public double SecondsSoftirq { get; set; }

    /// <summary>
    /// Time stolen by other VMs (seconds).
    /// </summary>
    public double SecondsSteal { get; set; }

    /// <summary>
    /// Total CPU time (sum of all modes).
    /// </summary>
    public double SecondsTotal => SecondsUser + SecondsNice + SecondsSystem + SecondsIdle +
                                   SecondsIowait + SecondsIrq + SecondsSoftirq + SecondsSteal;
}

/// <summary>
/// CPU metrics from the system.
/// </summary>
public class CpuMetrics
{
    /// <summary>
    /// Per-core CPU metrics.
    /// </summary>
    public List<CpuCoreMetrics> Cores { get; set; } = [];

    /// <summary>
    /// 1-minute load average.
    /// </summary>
    public double Load1 { get; set; }

    /// <summary>
    /// 5-minute load average.
    /// </summary>
    public double Load5 { get; set; }

    /// <summary>
    /// 15-minute load average.
    /// </summary>
    public double Load15 { get; set; }

    /// <summary>
    /// Total context switches.
    /// </summary>
    public long ContextSwitchesTotal { get; set; }
}

/// <summary>
/// GPU metrics.
/// </summary>
public class GpuMetrics
{
    /// <summary>
    /// GPU utilization percentage (0-100).
    /// </summary>
    public int Utilization { get; set; }
}

/// <summary>
/// RAM/Memory metrics.
/// </summary>
public class RamMetrics
{
    /// <summary>
    /// Total memory in bytes.
    /// </summary>
    public long MemTotalBytes { get; set; }

    /// <summary>
    /// Available memory in bytes (including reclaimable cache).
    /// </summary>
    public long MemAvailableBytes { get; set; }

    /// <summary>
    /// Free memory in bytes.
    /// </summary>
    public long MemFreeBytes { get; set; }

    /// <summary>
    /// Memory used for buffers in bytes.
    /// </summary>
    public long BuffersBytes { get; set; }

    /// <summary>
    /// Memory used for cache in bytes.
    /// </summary>
    public long CachedBytes { get; set; }

    /// <summary>
    /// Total swap space in bytes.
    /// </summary>
    public long SwapTotalBytes { get; set; }

    /// <summary>
    /// Free swap space in bytes.
    /// </summary>
    public long SwapFreeBytes { get; set; }
}

/// <summary>
/// Disk metrics for a single device/filesystem.
/// </summary>
public class DiskMetrics
{
    /// <summary>
    /// Device name (e.g., "sda", "nvme0n1").
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Mount point (e.g., "/", "/home").
    /// </summary>
    public string MountPoint { get; set; } = string.Empty;

    /// <summary>
    /// Available space for non-privileged users in bytes.
    /// </summary>
    public long FilesystemAvailBytes { get; set; }

    /// <summary>
    /// Free space in bytes.
    /// </summary>
    public long FilesystemFreeBytes { get; set; }

    /// <summary>
    /// Available inodes.
    /// </summary>
    public long FilesystemFilesFree { get; set; }

    /// <summary>
    /// Total inodes.
    /// </summary>
    public long FilesystemFiles { get; set; }

    /// <summary>
    /// Total disk reads completed.
    /// </summary>
    public long ReadsCompletedTotal { get; set; }

    /// <summary>
    /// Total disk writes completed.
    /// </summary>
    public long WritesCompletedTotal { get; set; }

    /// <summary>
    /// Total bytes read from disk.
    /// </summary>
    public long ReadBytesTotal { get; set; }

    /// <summary>
    /// Total bytes written to disk.
    /// </summary>
    public long WrittenBytesTotal { get; set; }

    /// <summary>
    /// Total I/O time in seconds.
    /// </summary>
    public long IOTimeSecondsTotal { get; set; }
}

/// <summary>
/// Network interface metrics.
/// </summary>
public class NetworkMetrics
{
    /// <summary>
    /// Interface name (e.g., "eth0", "en0").
    /// </summary>
    public string InterfaceName { get; set; } = string.Empty;

    /// <summary>
    /// Total bytes received.
    /// </summary>
    public long ReceiveBytesTotal { get; set; }

    /// <summary>
    /// Total bytes transmitted.
    /// </summary>
    public long TransmitBytesTotal { get; set; }

    /// <summary>
    /// Total packets received.
    /// </summary>
    public long ReceivePacketsTotal { get; set; }

    /// <summary>
    /// Total packets transmitted.
    /// </summary>
    public long TransmitPacketsTotal { get; set; }

    /// <summary>
    /// Total receive errors.
    /// </summary>
    public long ReceiveErrsTotal { get; set; }

    /// <summary>
    /// Total transmit errors.
    /// </summary>
    public long TransmitErrsTotal { get; set; }
}

/// <summary>
/// System-level metrics.
/// </summary>
public class SystemMetrics
{
    /// <summary>
    /// Current system time (Unix timestamp in seconds).
    /// </summary>
    public long TimeSeconds { get; set; }

    /// <summary>
    /// Time offset from NTP server in seconds.
    /// </summary>
    public double TimexOffsetSeconds { get; set; }

    /// <summary>
    /// System boot time (Unix timestamp in seconds).
    /// </summary>
    public long BootTimeSeconds { get; set; }

    /// <summary>
    /// Number of allocated file descriptors.
    /// </summary>
    public long FilefdAllocated { get; set; }

    /// <summary>
    /// Maximum number of file descriptors.
    /// </summary>
    public long FilefdMaximum { get; set; }

    /// <summary>
    /// Number of running processes.
    /// </summary>
    public int ProcsRunning { get; set; }

    /// <summary>
    /// Number of blocked processes.
    /// </summary>
    public int ProcsBlocked { get; set; }

    /// <summary>
    /// Total interrupts handled.
    /// </summary>
    public long IntrTotal { get; set; }
}
