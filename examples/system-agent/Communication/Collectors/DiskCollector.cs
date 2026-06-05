using Microsoft.Extensions.Logging;

using SystemAgentExample.Communication.Utilities;
using SystemAgentExample.Models;

namespace SystemAgentExample.Communication.Collectors;

public class DiskCollector
{
    private readonly ILogger _logger;

    public DiskCollector(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<List<DiskMetrics>> CollectDiskMetricsAsync(CancellationToken ct)
    {
        var diskList = new List<DiskMetrics>();

        try
        {
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives.Where(d => d.IsReady))
            {
                var deviceName = PlatformHelpers.NormalizeDriveName(drive.Name);
                var disk = new DiskMetrics
                {
                    DeviceName = deviceName,
                    MountPoint = drive.Name,
                    FilesystemTotalBytes = drive.TotalSize,
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

    private async Task<Dictionary<string, DiskIoStats>> ReadLinuxDiskStatsAsync(CancellationToken ct)
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
                    if (!long.TryParse(parts[3], out var readsCompleted)) continue;
                    if (!long.TryParse(parts[5], out var sectorsRead)) continue;
                    if (!long.TryParse(parts[7], out var writesCompleted)) continue;
                    if (!long.TryParse(parts[9], out var sectorsWritten)) continue;
                    if (!long.TryParse(parts[12], out var ioTimeMs)) continue;

                    result[deviceName] = new DiskIoStats
                    {
                        ReadsCompleted = readsCompleted,
                        SectorsRead = sectorsRead,
                        WritesCompleted = writesCompleted,
                        SectorsWritten = sectorsWritten,
                        IoTimeMs = ioTimeMs
                    };
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read /proc/diskstats — I/O stats will be unavailable");
        }

        return result;
    }

    private static (long total, long free) GetLinuxInodeInfo(string _)
    {
        // This would require P/Invoke to statvfs, return 0 for now
        return (0, 0);
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
