namespace SystemAgentExample.Models;

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
    
    // Hardware platform-specific metrics (implementation may vary by platform)
    public HardwareInfoMetrics HardwareInfo { get; set; } = new();
    // Onboard sensors are split into separate metrics since they can be configured independently
    public TemperatureMetrics Temperature { get; set; } = new();
    public VoltageMetrics Voltage { get; set; } = new();
    public FanSpeedMetrics FanSpeed { get; set; } = new();
    public GpioMetrics Gpio { get; set; } = new();
    public WatchdogMetrics Watchdog { get; set; } = new();
    public ThermalProtectionMetrics ThermalProtection { get; set; } = new();
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



/// <summary>
/// Hardware platform information including motherboard, manufacturer, BIOS, and firmware details.
/// Implementation-agnostic abstraction - actual implementation may use different hardware APIs.
/// </summary>
public class HardwareInfoMetrics
{
    /// <summary>
    /// The name of the motherboard.
    /// </summary>
    public string MotherboardName { get; set; } = string.Empty;

    /// <summary>
    /// The vendor of the motherboard.
    /// </summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>
    /// The BIOS version string.
    /// </summary>
    public string BiosRevision { get; set; } = string.Empty;

    /// <summary>
    /// The version of the device driver.
    /// </summary>
    public string DriverVersion { get; set; } = string.Empty;

    /// <summary>
    /// The version of the SDK library.
    /// </summary>
    public string LibraryVersion { get; set; } = string.Empty;

    /// <summary>
    /// The embedded controller (EC) firmware version.
    /// </summary>
    public string EcRevision { get; set; } = string.Empty;
}

/// <summary>
/// Temperature readings from onboard sensors.
/// Separated from voltage and fan speed metrics to allow independent configuration.
/// </summary>
public class TemperatureMetrics
{
    /// <summary>
    /// A dictionary containing temperature readings, keyed by the source name (e.g., CPU, System).
    /// Value is the temperature in Celsius (°C).
    /// </summary>
    public Dictionary<string, double?> Temperatures { get; set; } = new Dictionary<string, double?>();
}

/// <summary>
/// Voltage readings from onboard sensors.
/// </summary>
public class VoltageMetrics
{
    /// <summary>
    /// A dictionary containing voltage readings, keyed by the source name.
    /// Value is the voltage in volts.
    /// </summary>
    public Dictionary<string, double?> Voltages { get; set; } = new Dictionary<string, double?>();
}

/// <summary>
/// Fan speed readings from onboard sensors.
/// </summary>
public class FanSpeedMetrics
{
    /// <summary>
    /// A dictionary containing fan speed readings, keyed by the source name.
    /// Value is the fan speed in RPM.
    /// </summary>
    public Dictionary<string, double?> FanSpeeds { get; set; } = new Dictionary<string, double?>();
}

/// <summary>
/// GPIO (General Purpose Input/Output) metrics and capabilities.
/// </summary>
public class GpioMetrics
{
    /// <summary>
    /// Indicates whether the GPIO feature is supported by the device.
    /// </summary>
    public bool IsSupported { get; set; }

    /// <summary>
    /// The list of available GPIO pin names/IDs on the device.
    /// </summary>
    public string[] PinNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// A dictionary to hold the current level for a specific pin, using the pin name as the key.
    /// Key: Pin Name/ID (string). Value: Pin level as integer (0 = Low, 1 = High).
    /// </summary>
    public Dictionary<string, int> PinStateDetails { get; set; } = new Dictionary<string, int>();
}

/// <summary>
/// Watchdog timer metrics and capabilities.
/// </summary>
public class WatchdogMetrics
{
    /// <summary>
    /// Indicates whether the Watchdog feature is supported by the device.
    /// </summary>
    public bool IsSupported { get; set; }

    /// <summary>
    /// A list of available Watchdog timer IDs.
    /// </summary>
    public string[] TimerIds { get; set; } = Array.Empty<string>();

    /// <summary>
    /// A dictionary mapping Timer ID to its capabilities and current configuration.
    /// Since the exact types (WatchdogTimerCap, WatchdogTimerConfig) are unknown, the values are stored as object.
    /// Key: Timer ID (string). Value: An object containing timer capabilities and configuration retrieved via TryGetCap/TryGetConfig.
    /// </summary>
    public Dictionary<string, object> TimerDetails { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Thermal protection metrics and zone configurations.
/// </summary>
public class ThermalProtectionMetrics
{
    /// <summary>
    /// Indicates whether the Thermal Protection feature is supported by the device.
    /// </summary>
    public bool IsSupported { get; set; }

    /// <summary>
    /// A list of available thermal protection zone IDs.
    /// </summary>
    public string[] ZoneIds { get; set; } = Array.Empty<string>();

    /// <summary>
    /// A dictionary mapping Zone ID to its capabilities and current configuration.
    /// Since the exact types (ThermalProtectionZoneCap, ThermalProtectionZoneConfig) are unknown, the values are stored as object.
    /// Key: Zone ID (string). Value: An object containing zone capabilities and configuration retrieved via TryGetZoneCap/TryGetZoneConfig.
    /// </summary>
    public Dictionary<string, object> ZoneDetails { get; set; } = new Dictionary<string, object>();
}
