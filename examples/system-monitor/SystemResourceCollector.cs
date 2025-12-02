using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace SystemMonitorExample;

/// <summary>
/// Collects system resource metrics using cross-platform .NET APIs.
/// </summary>
public class SystemResourceCollector
{
    private readonly ILogger _logger;
    private DateTime _lastCpuCheck = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime;
    private Dictionary<int, CpuCoreStats> _lastCoreStats = new();

    public SystemResourceCollector(ILogger logger)
    {
        _logger = logger;
        _lastTotalProcessorTime = Process.GetCurrentProcess().TotalProcessorTime;
    }

    /// <summary>
    /// Collects CPU usage metrics (overall usage, per-core usage, load averages).
    /// </summary>
    public async Task<List<TelemetryMeasure>> CollectCpuMetricsAsync(List<Sensor> sensors, CancellationToken ct)
    {
        var measures = new List<TelemetryMeasure>();

        try
        {
            // Overall CPU usage (cross-platform approximation)
            var now = DateTime.UtcNow;
            var elapsedTime = now - _lastCpuCheck;

            if (elapsedTime.TotalMilliseconds > 100)
            {
                var currentProcess = Process.GetCurrentProcess();
                var currentTotalTime = currentProcess.TotalProcessorTime;
                var cpuUsedMs = (currentTotalTime - _lastTotalProcessorTime).TotalMilliseconds;
                var totalMsPassed = elapsedTime.TotalMilliseconds * Environment.ProcessorCount;
                var cpuUsagePercent = totalMsPassed > 0 ? (cpuUsedMs / totalMsPassed) * 100 : 0;

                var cpuSensor = sensors.FirstOrDefault(s => s.Name == "cpu.usage");
                if (cpuSensor != null)
                {
                    measures.Add(new TelemetryMeasure
                    {
                        ResourceId = cpuSensor.ResourceId,
                        Value = Math.Round(Math.Min(cpuUsagePercent, 100), 2)
                    });
                }

                _lastCpuCheck = now;
                _lastTotalProcessorTime = currentTotalTime;
            }

            // Per-core CPU usage (Linux only)
            if (OperatingSystem.IsLinux())
            {
                var coreMetrics = await ReadLinuxCpuStatsAsync(ct);
                foreach (var (coreNum, usage) in coreMetrics)
                {
                    var coreSensor = sensors.FirstOrDefault(s => s.Name == $"cpu.core.{coreNum}");
                    if (coreSensor != null)
                    {
                        measures.Add(new TelemetryMeasure
                        {
                            ResourceId = coreSensor.ResourceId,
                            Value = Math.Round(usage, 2)
                        });
                    }
                }

                // Load averages (Linux only)
                var loadAvg = await ReadLinuxLoadAverageAsync(ct);
                if (loadAvg != null)
                {
                    var load1mSensor = sensors.FirstOrDefault(s => s.Name == "cpu.load.1m");
                    if (load1mSensor != null)
                    {
                        measures.Add(new TelemetryMeasure
                        {
                            ResourceId = load1mSensor.ResourceId,
                            Value = loadAvg.Value.OneMinute
                        });
                    }

                    var load5mSensor = sensors.FirstOrDefault(s => s.Name == "cpu.load.5m");
                    if (load5mSensor != null)
                    {
                        measures.Add(new TelemetryMeasure
                        {
                            ResourceId = load5mSensor.ResourceId,
                            Value = loadAvg.Value.FiveMinute
                        });
                    }

                    var load15mSensor = sensors.FirstOrDefault(s => s.Name == "cpu.load.15m");
                    if (load15mSensor != null)
                    {
                        measures.Add(new TelemetryMeasure
                        {
                            ResourceId = load15mSensor.ResourceId,
                            Value = loadAvg.Value.FifteenMinute
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect CPU metrics");
        }

        return measures;
    }

    /// <summary>
    /// Collects memory usage metrics (total, used, available, cached).
    /// </summary>
    public async Task<List<TelemetryMeasure>> CollectMemoryMetricsAsync(List<Sensor> sensors, CancellationToken ct)
    {
        var measures = new List<TelemetryMeasure>();

        try
        {
            // GC memory info (cross-platform)
            var gcInfo = GC.GetGCMemoryInfo();

            var totalSensor = sensors.FirstOrDefault(s => s.Name == "memory.total");
            if (totalSensor != null)
            {
                measures.Add(new TelemetryMeasure
                {
                    ResourceId = totalSensor.ResourceId,
                    Value = gcInfo.TotalAvailableMemoryBytes
                });
            }

            // Process memory (cross-platform)
            var currentProcess = Process.GetCurrentProcess();
            var usedSensor = sensors.FirstOrDefault(s => s.Name == "memory.used");
            if (usedSensor != null)
            {
                measures.Add(new TelemetryMeasure
                {
                    ResourceId = usedSensor.ResourceId,
                    Value = currentProcess.WorkingSet64
                });
            }

            // Linux-specific memory details
            if (OperatingSystem.IsLinux())
            {
                var memInfo = await ReadLinuxMemInfoAsync(ct);

                if (memInfo.TryGetValue("MemAvailable", out var memAvailable))
                {
                    var availableSensor = sensors.FirstOrDefault(s => s.Name == "memory.available");
                    if (availableSensor != null)
                    {
                        measures.Add(new TelemetryMeasure
                        {
                            ResourceId = availableSensor.ResourceId,
                            Value = memAvailable * 1024 // KB to bytes
                        });
                    }
                }

                if (memInfo.TryGetValue("Cached", out var cached))
                {
                    var cachedSensor = sensors.FirstOrDefault(s => s.Name == "memory.cached");
                    if (cachedSensor != null)
                    {
                        measures.Add(new TelemetryMeasure
                        {
                            ResourceId = cachedSensor.ResourceId,
                            Value = cached * 1024
                        });
                    }
                }

                if (memInfo.TryGetValue("Buffers", out var buffers))
                {
                    var buffersSensor = sensors.FirstOrDefault(s => s.Name == "memory.buffers");
                    if (buffersSensor != null)
                    {
                        measures.Add(new TelemetryMeasure
                        {
                            ResourceId = buffersSensor.ResourceId,
                            Value = buffers * 1024
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect memory metrics");
        }

        return measures;
    }

    /// <summary>
    /// Collects disk usage metrics for all mounted drives.
    /// </summary>
    public Task<List<TelemetryMeasure>> CollectDiskMetricsAsync(List<Sensor> sensors, CancellationToken ct)
    {
        var measures = new List<TelemetryMeasure>();

        try
        {
            var drives = DriveInfo.GetDrives();

            foreach (var drive in drives.Where(d => d.IsReady))
            {
                var driveName = NormalizeDriveName(drive.Name);

                // Total space
                var totalSensor = sensors.FirstOrDefault(s => s.Name == $"disk.{driveName}.total");
                if (totalSensor != null)
                {
                    measures.Add(new TelemetryMeasure
                    {
                        ResourceId = totalSensor.ResourceId,
                        Value = drive.TotalSize
                    });
                }

                // Used space
                var usedSensor = sensors.FirstOrDefault(s => s.Name == $"disk.{driveName}.used");
                if (usedSensor != null)
                {
                    measures.Add(new TelemetryMeasure
                    {
                        ResourceId = usedSensor.ResourceId,
                        Value = drive.TotalSize - drive.AvailableFreeSpace
                    });
                }

                // Available space
                var availableSensor = sensors.FirstOrDefault(s => s.Name == $"disk.{driveName}.available");
                if (availableSensor != null)
                {
                    measures.Add(new TelemetryMeasure
                    {
                        ResourceId = availableSensor.ResourceId,
                        Value = drive.AvailableFreeSpace
                    });
                }

                // Usage percentage
                var usagePercentSensor = sensors.FirstOrDefault(s => s.Name == $"disk.{driveName}.usage_percent");
                if (usagePercentSensor != null && drive.TotalSize > 0)
                {
                    var usagePercent = (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100;
                    measures.Add(new TelemetryMeasure
                    {
                        ResourceId = usagePercentSensor.ResourceId,
                        Value = Math.Round(usagePercent, 2)
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect disk metrics");
        }

        return Task.FromResult(measures);
    }

    /// <summary>
    /// Collects network interface metrics (bytes sent/received, packets, errors).
    /// </summary>
    public Task<List<TelemetryMeasure>> CollectNetworkMetricsAsync(List<Sensor> sensors, CancellationToken ct)
    {
        var measures = new List<TelemetryMeasure>();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var iface in interfaces.Where(i => i.OperationalStatus == OperationalStatus.Up))
            {
                var ifaceName = SanitizeInterfaceName(iface.Name);
                var stats = iface.GetIPv4Statistics();

                // Bytes sent
                var sentSensor = sensors.FirstOrDefault(s => s.Name == $"network.{ifaceName}.bytes_sent");
                if (sentSensor != null)
                {
                    measures.Add(new TelemetryMeasure
                    {
                        ResourceId = sentSensor.ResourceId,
                        Value = stats.BytesSent
                    });
                }

                // Bytes received
                var receivedSensor = sensors.FirstOrDefault(s => s.Name == $"network.{ifaceName}.bytes_received");
                if (receivedSensor != null)
                {
                    measures.Add(new TelemetryMeasure
                    {
                        ResourceId = receivedSensor.ResourceId,
                        Value = stats.BytesReceived
                    });
                }

                // Packets sent
                var packetsSentSensor = sensors.FirstOrDefault(s => s.Name == $"network.{ifaceName}.packets_sent");
                if (packetsSentSensor != null)
                {
                    measures.Add(new TelemetryMeasure
                    {
                        ResourceId = packetsSentSensor.ResourceId,
                        Value = stats.UnicastPacketsSent
                    });
                }

                // Packets received
                var packetsReceivedSensor = sensors.FirstOrDefault(s => s.Name == $"network.{ifaceName}.packets_received");
                if (packetsReceivedSensor != null)
                {
                    measures.Add(new TelemetryMeasure
                    {
                        ResourceId = packetsReceivedSensor.ResourceId,
                        Value = stats.UnicastPacketsReceived
                    });
                }

                // Errors
                var errorsSensor = sensors.FirstOrDefault(s => s.Name == $"network.{ifaceName}.errors");
                if (errorsSensor != null)
                {
                    measures.Add(new TelemetryMeasure
                    {
                        ResourceId = errorsSensor.ResourceId,
                        Value = stats.IncomingPacketsWithErrors + stats.OutgoingPacketsWithErrors
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect network metrics");
        }

        return Task.FromResult(measures);
    }

    // Linux-specific helpers

    private async Task<Dictionary<int, double>> ReadLinuxCpuStatsAsync(CancellationToken ct)
    {
        try
        {
            var stats = await File.ReadAllTextAsync("/proc/stat", ct);
            var lines = stats.Split('\n');
            var result = new Dictionary<int, double>();

            foreach (var line in lines)
            {
                if (line.StartsWith("cpu") && line.Length > 3 && char.IsDigit(line[3]))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var coreNum = int.Parse(parts[0][3..]);
                    var user = long.Parse(parts[1]);
                    var nice = long.Parse(parts[2]);
                    var system = long.Parse(parts[3]);
                    var idle = long.Parse(parts[4]);

                    var total = user + nice + system + idle;

                    // Calculate usage based on change from last measurement
                    if (_lastCoreStats.TryGetValue(coreNum, out var lastStats))
                    {
                        var totalDelta = total - lastStats.Total;
                        var idleDelta = idle - lastStats.Idle;
                        var usage = totalDelta > 0 ? (double)(totalDelta - idleDelta) / totalDelta * 100 : 0;
                        result[coreNum] = usage;
                    }

                    _lastCoreStats[coreNum] = new CpuCoreStats { Total = total, Idle = idle };
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read /proc/stat");
            return new Dictionary<int, double>();
        }
    }

    private async Task<LoadAverage?> ReadLinuxLoadAverageAsync(CancellationToken ct)
    {
        try
        {
            var content = await File.ReadAllTextAsync("/proc/loadavg", ct);
            var parts = content.Split(' ');
            if (parts.Length >= 3)
            {
                return new LoadAverage
                {
                    OneMinute = double.Parse(parts[0]),
                    FiveMinute = double.Parse(parts[1]),
                    FifteenMinute = double.Parse(parts[2])
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read /proc/loadavg");
        }

        return null;
    }

    private async Task<Dictionary<string, long>> ReadLinuxMemInfoAsync(CancellationToken ct)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read /proc/meminfo");
            return new Dictionary<string, long>();
        }
    }

    // Helper methods

    private static string NormalizeDriveName(string driveName)
    {
        var normalized = driveName.TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(normalized) || normalized == "/")
            return "root";
        if (normalized.EndsWith(':'))
            return normalized.TrimEnd(':');
        return normalized.Replace("/", "_");
    }

    private static string SanitizeInterfaceName(string name)
    {
        return name.Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
    }

    private record struct CpuCoreStats
    {
        public long Total { get; init; }
        public long Idle { get; init; }
    }

    private record struct LoadAverage
    {
        public double OneMinute { get; init; }
        public double FiveMinute { get; init; }
        public double FifteenMinute { get; init; }
    }
}
