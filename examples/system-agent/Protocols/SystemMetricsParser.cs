using ErrorOr;

using Microsoft.Extensions.Logging;

using SystemAgentExample.Communication;
using SystemAgentExample.Models;

using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Protocols;


public static class SupportedDataType
{
    public const string Cpu = "cpu";
    public const string Memory = "memory";
    public const string Disk = "disk";
    public const string Network = "network";
    public const string Gpu = "gpu";
    public const string System = "system";
    public const string Temperature = "temperature";
    public const string Voltage = "voltage";
    public const string Fanspeed = "fanspeed";
    public const string Hwinfo = "hwinfo";
    public const string Gpio = "gpio";
    public const string Watchdog = "watchdog";
    public const string Thermalprotection = "thermalprotection";
}


/// <summary>
/// Protocol parser for system metrics.
/// Converts raw system metrics data to TelemetryMeasure.
/// </summary>
public class SystemMetricsParser : IRequestResponseProtocolParser
{
    private readonly DeviceConfiguration _configuration;
    private readonly LocalSystemCommunication _communication;
    private readonly ILogger<SystemMetricsParser> _logger;

    public SystemMetricsParser(
        DeviceConfiguration configuration,
        LocalSystemCommunication communication,
        ILogger<SystemMetricsParser> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ICommunication Communication => _communication;

    public string ProtocolName => "LocalSystem";

    public IReadOnlyList<string> SupportedDataTypes => [
        SupportedDataType.Cpu,
        SupportedDataType.Memory,
        SupportedDataType.Disk,
        SupportedDataType.Network,
        SupportedDataType.Gpu,
        SupportedDataType.System,
        SupportedDataType.Temperature,
        SupportedDataType.Voltage,
        SupportedDataType.Fanspeed,
        SupportedDataType.Hwinfo,
        SupportedDataType.Gpio,
        SupportedDataType.Watchdog,
        SupportedDataType.Thermalprotection
    ];

    public bool SupportsBidirectional => false;

    /// <summary>
    /// Read telemetry data for all enabled sensors.
    /// </summary>
    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var enabledSensors = _configuration.Sensors.Where(s => s.Report.Enabled).ToList();
        return await ReadTelemetryForSensorsAsync(enabledSensors, cancellationToken);
    }

    /// <summary>
    /// Read telemetry data for specific sensors only (optimized per-sensor collection).
    /// Groups sensors by MetricType and only collects the required categories.
    /// </summary>
    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(
        IEnumerable<string> sensorResourceIds,
        CancellationToken cancellationToken = default)
    {
        // Find sensors by ResourceId
        var requestedIds = sensorResourceIds.ToHashSet();

        var sensors = _configuration.Sensors
            .Where(s => s.Report.Enabled && requestedIds.Contains(s.ResourceId))
            .ToList();

        return await ReadTelemetryForSensorsAsync(sensors, cancellationToken);
    }

    /// <summary>
    /// Internal method to read telemetry for a specific set of sensors.
    /// Optimizes by only collecting the metric categories that are needed.
    /// Uses the Request-Response pattern via Communication.RequestAsync().
    /// </summary>
    private async Task<List<TelemetryMeasure>> ReadTelemetryForSensorsAsync(
        List<Sensor> sensors,
        CancellationToken cancellationToken)
    {
        if (!_communication.IsConnected)
        {
            _logger.LogWarning("Cannot read sensor data: communication not connected");
            return [];
        }

        if (sensors.Count == 0)
        {
            return [];
        }

        try
        {
            // Group sensors by MetricType to determine what to collect
            var metricTypes = sensors
                .Select(s => GetParameterValue(s, "MetricType"))
                .Where(t => t != null)
                .Distinct()
                .ToHashSet();

            _logger.LogDebug("Collecting metrics for types: {Types}", string.Join(", ", metricTypes));

            // Use Request-Response pattern via LocalSystemCommunication.RequestAsync
            var request = new SystemMetricsRequest(metricTypes!);
            var rawData = await _communication.RequestAsync(request, cancellationToken);

            // Convert raw data to telemetry measures
            return ConvertToTelemetryMeasures(sensors, rawData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read sensor data from local system");
            return [];
        }
    }

    /// <summary>
    /// Convert raw system metrics to TelemetryMeasures using sensor Parameters.
    /// </summary>
    private List<TelemetryMeasure> ConvertToTelemetryMeasures(List<Sensor> sensors, SystemMetricsRawData rawData)
    {
        var measures = new List<TelemetryMeasure>();

        foreach (var sensor in sensors)
        {
            var metricType = GetParameterValue(sensor, "MetricType");
            var metricName = GetParameterValue(sensor, "MetricName");

            if (metricType == null)
            {
                _logger.LogWarning("Sensor {Name} missing MetricType parameter", sensor.Name);
                continue;
            }

            var value = GetMetricValue(rawData, metricType, metricName, sensor);
            if (value != null)
            {
                measures.Add(new TelemetryMeasure
                {
                    ResourceId = sensor.ResourceId,
                    Value = value
                });
            }
        }

        return measures;
    }

    /// <summary>
    /// Get metric value from raw data based on MetricType and MetricName.
    /// </summary>
    private object? GetMetricValue(SystemMetricsRawData rawData, string metricType, string? metricName, Sensor sensor)
    {
        return metricType.ToLowerInvariant() switch
        {
            SupportedDataType.Cpu => metricName != null ? GetCpuMetric(rawData.Cpu, metricName) : null,
            SupportedDataType.Memory => metricName != null ? GetMemoryMetric(rawData.Ram, metricName) : null,
            SupportedDataType.Disk => metricName != null ? GetDiskMetric(rawData.Disks, metricName, sensor) : null,
            SupportedDataType.Network => metricName != null ? GetNetworkMetric(rawData.Networks, metricName, sensor) : null,
            SupportedDataType.Gpu => metricName != null ? GetGpuMetric(rawData.Gpu, metricName) : null,
            SupportedDataType.System => metricName != null ? GetSystemMetric(rawData.System, metricName) : null,
            SupportedDataType.Hwinfo => metricName != null ? GetHardwareInfoMetric(rawData.HardwareInfo, metricName) : null,
            SupportedDataType.Temperature => metricName != null ? GetTemperatureMetric(rawData.Temperature, metricName, sensor) : null,
            SupportedDataType.Voltage => metricName != null ? GetVoltageMetric(rawData.Voltage, metricName, sensor) : null,
            SupportedDataType.Fanspeed => metricName != null ? GetFanSpeedMetric(rawData.FanSpeed, metricName, sensor) : null,
            SupportedDataType.Gpio => metricName != null ? GetGpioMetric(rawData.Gpio, metricName) : null,
            SupportedDataType.Watchdog => metricName != null ? GetWatchdogMetric(rawData.Watchdog, metricName) : null,
            SupportedDataType.Thermalprotection => metricName != null ? GetThermalProtectionMetric(rawData.ThermalProtection, metricName) : null,
            _ => null
        };
    }

    private object? GetCpuMetric(CpuMetrics cpu, string metricName)
    {
        return metricName.ToLowerInvariant() switch
        {
            "usage" => CalculateCpuUsage(cpu),
            "load1" => cpu.Load1,
            "load5" => cpu.Load5,
            "load15" => cpu.Load15,
            "context_switches" => cpu.ContextSwitchesTotal,
            _ => null
        };
    }

    private double CalculateCpuUsage(CpuMetrics cpu)
    {
        if (cpu.Cores.Count == 0) return 0;

        // Calculate overall CPU usage from all cores
        double totalIdle = 0;
        double totalBusy = 0;

        foreach (var core in cpu.Cores)
        {
            totalIdle += core.SecondsIdle;
            totalBusy += core.SecondsUser + core.SecondsSystem + core.SecondsNice +
                        core.SecondsIowait + core.SecondsIrq + core.SecondsSoftirq + core.SecondsSteal;
        }

        var total = totalIdle + totalBusy;
        if (total == 0) return 0;

        return Math.Round((totalBusy / total) * 100, 2);
    }

    private object? GetMemoryMetric(RamMetrics ram, string metricName)
    {
        return metricName.ToLowerInvariant() switch
        {
            "total" => ram.MemTotalBytes,
            "available" => ram.MemAvailableBytes,
            "used" => ram.MemTotalBytes - ram.MemAvailableBytes,
            "free" => ram.MemFreeBytes,
            "cached" => ram.CachedBytes,
            "buffers" => ram.BuffersBytes,
            "swap_total" => ram.SwapTotalBytes,
            "swap_free" => ram.SwapFreeBytes,
            _ => null
        };
    }

    private object? GetDiskMetric(List<DiskMetrics> disks, string metricName, Sensor sensor)
    {
        var mountPoint = GetParameterValue(sensor, "MountPoint") ?? "/";

        // Find disk by mount point (normalize for comparison)
        var disk = disks.FirstOrDefault(d =>
            NormalizeMountPoint(d.MountPoint) == NormalizeMountPoint(mountPoint));

        if (disk == null)
        {
            _logger.LogDebug("Disk not found for mount point: {MountPoint}", mountPoint);
            return null;
        }

        var total = disk.FilesystemAvailBytes + (disk.FilesystemFreeBytes > 0
            ? disk.FilesystemFreeBytes - disk.FilesystemAvailBytes + disk.FilesystemAvailBytes
            : disk.FilesystemAvailBytes);

        // Estimate total from available (this is a simplification)
        // In real implementation, we'd need to get total from statfs
        return metricName.ToLowerInvariant() switch
        {
            "total" => total > 0 ? total : disk.FilesystemAvailBytes * 2, // Rough estimate
            "available" => disk.FilesystemAvailBytes,
            "free" => disk.FilesystemFreeBytes,
            "used" => total > disk.FilesystemAvailBytes ? total - disk.FilesystemAvailBytes : 0,
            "usage_percent" => total > 0 ? Math.Round((1 - (double)disk.FilesystemAvailBytes / total) * 100, 2) : 0,
            "reads_completed" => disk.ReadsCompletedTotal,
            "writes_completed" => disk.WritesCompletedTotal,
            "read_bytes" => disk.ReadBytesTotal,
            "written_bytes" => disk.WrittenBytesTotal,
            _ => null
        };
    }

    private object? GetNetworkMetric(List<NetworkMetrics> networks, string metricName, Sensor sensor)
    {
        var interfaceName = GetParameterValue(sensor, "Interface");
        if (interfaceName == null)
        {
            _logger.LogWarning("Sensor {Name} missing Interface parameter", sensor.Name);
            return null;
        }

        // Find network interface (case-insensitive, handle sanitized names)
        var network = networks.FirstOrDefault(n =>
            n.InterfaceName.Equals(interfaceName, StringComparison.OrdinalIgnoreCase) ||
            n.InterfaceName.Replace("_", "").Equals(interfaceName.Replace("_", ""), StringComparison.OrdinalIgnoreCase));

        if (network == null)
        {
            _logger.LogDebug("Network interface not found: {Interface}", interfaceName);
            return null;
        }

        return metricName.ToLowerInvariant() switch
        {
            "bytes_sent" => network.TransmitBytesTotal,
            "bytes_received" => network.ReceiveBytesTotal,
            "packets_sent" => network.TransmitPacketsTotal,
            "packets_received" => network.ReceivePacketsTotal,
            "errors" => network.ReceiveErrsTotal + network.TransmitErrsTotal,
            "errors_in" => network.ReceiveErrsTotal,
            "errors_out" => network.TransmitErrsTotal,
            _ => null
        };
    }

    private object? GetGpuMetric(GpuMetrics gpu, string metricName)
    {
        return metricName.ToLowerInvariant() switch
        {
            "utilization" => gpu.Utilization,
            _ => null
        };
    }

    private object? GetSystemMetric(SystemMetrics system, string metricName)
    {
        return metricName.ToLowerInvariant() switch
        {
            "time" => system.TimeSeconds,
            "timex_offset" => system.TimexOffsetSeconds,
            "boot_time" => system.BootTimeSeconds,
            "filefd_allocated" => system.FilefdAllocated,
            "filefd_maximum" => system.FilefdMaximum,
            "procs_running" => system.ProcsRunning,
            "procs_blocked" => system.ProcsBlocked,
            "intr_total" => system.IntrTotal,
            _ => null
        };
    }

    private static string? GetParameterValue(Sensor sensor, string key)
    {
        if (sensor.Parameters == null) return null;
        return sensor.Parameters.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static string NormalizeMountPoint(string mountPoint)
    {
        var normalized = mountPoint.TrimEnd('\\', '/');
        return string.IsNullOrEmpty(normalized) ? "/" : normalized;
    }

    private object? GetHardwareInfoMetric(HardwareInfoMetrics? metrics, string metricName)
    {
        if (metrics == null) return null;

        return metricName.ToLowerInvariant() switch
        {
            "motherboardname" => metrics.MotherboardName,
            "manufacturer" => metrics.Manufacturer,
            "biosrevision" => metrics.BiosRevision,
            "driverversion" => metrics.DriverVersion,
            "libraryversion" => metrics.LibraryVersion,
            "ecrevision" => metrics.EcRevision,
            _ => null
        };
    }

    private object? GetTemperatureMetric(TemperatureMetrics? metrics, string metricName, Sensor sensor)
    {
        if (metrics == null || metrics.Temperatures == null) return null;

        // Return all temperatures as dictionary
        return metrics.Temperatures;
    }

    private object? GetVoltageMetric(VoltageMetrics? metrics, string metricName, Sensor sensor)
    {
        if (metrics == null || metrics.Voltages == null) return null;

        // Return all voltages as dictionary
        return metrics.Voltages;
    }

    private object? GetFanSpeedMetric(FanSpeedMetrics? metrics, string metricName, Sensor sensor)
    {
        if (metrics == null || metrics.FanSpeeds == null) return null;

        // Return all fan speeds as dictionary
        return metrics.FanSpeeds;
    }

    private object? GetGpioMetric(GpioMetrics? metrics, string metricName)
    {
        // Return full GPIO metrics object
        // Collector already fetched IsSupported + PinNames together
        return metrics;
    }

    private object? GetWatchdogMetric(WatchdogMetrics? metrics, string metricName)
    {
        // Return full Watchdog metrics object
        // Collector already fetched IsSupported + TimerIds + TimerDetails together
        return metrics;
    }

    private object? GetThermalProtectionMetric(ThermalProtectionMetrics? metrics, string metricName)
    {
        // Return full ThermalProtection metrics object
        // Collector already fetched IsSupported + ZoneIds + ZoneDetails together
        return metrics;
    }

    public Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Command execution not supported for system monitor. Command: {Command}",
            command.DeviceCmd);

        return Task.FromResult<ErrorOr<object>>(
            Error.Failure("SystemMonitor.CommandNotSupported", "System monitor does not support commands"));
    }

    public Task<bool> WriteSensorDataAsync(
        IEnumerable<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Write operations not supported for system monitor");
        return Task.FromResult(false);
    }
}
