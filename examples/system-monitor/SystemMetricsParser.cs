using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace SystemMonitorExample;

/// <summary>
/// Protocol parser for system metrics.
/// Converts raw system metrics data to TelemetryMeasure.
/// Parser owns DeviceConfiguration and handles all mapping logic internally.
/// Architecture: Device -> Parser -> Communication (SystemResourceCollector)
/// </summary>
public class SystemMetricsParser : IRequestResponseProtocolParser
{
    private readonly DeviceConfiguration _configuration;
    private readonly LocalSystemCommunication _communication;
    private readonly SystemResourceCollector _collector;
    private readonly ILogger<SystemMetricsParser> _logger;

    public SystemMetricsParser(
        DeviceConfiguration configuration,
        LocalSystemCommunication communication,
        ILogger<SystemMetricsParser> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _collector = new SystemResourceCollector(logger);
    }

    public ICommunication Communication => _communication;

    public string ProtocolName => "LocalSystem";

    public IReadOnlyList<string> SupportedDataTypes => ["cpu", "gpu", "ram", "disk", "network", "system"];

    public bool SupportsBidirectional => false;

    /// <summary>
    /// Read telemetry data from system resources.
    /// Parser internally handles all mapping logic using DeviceConfiguration.
    /// </summary>
    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        if (!_communication.IsConnected)
        {
            _logger.LogWarning("Cannot read sensor data: communication not connected");
            return [];
        }

        try
        {
            // Collect raw metrics from system
            var rawData = await _collector.CollectAllMetricsAsync(cancellationToken);

            // Convert raw data to telemetry measures using configuration
            return ConvertToTelemetryMeasures(rawData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read sensor data from local system");
            return [];
        }
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

    /// <summary>
    /// Convert raw system metrics to TelemetryMeasures using DeviceConfiguration.
    /// </summary>
    private List<TelemetryMeasure> ConvertToTelemetryMeasures(SystemMetricsRawData rawData)
    {
        var measures = new List<TelemetryMeasure>();

        // Build sensor lookup from configuration (enabled sensors only)
        var sensorLookup = _configuration.Sensors
            .Where(s => s.Config.Enabled)
            .ToDictionary(s => s.Name, s => s.ResourceId);

        // CPU metrics (per core)
        foreach (var core in rawData.Cpu.Cores)
        {
            var corePrefix = $"cpu{core.CoreNumber}";
            AddMeasureIfEnabled(measures, sensorLookup, $"{corePrefix}.cpuSecondsUser", core.SecondsUser);
            AddMeasureIfEnabled(measures, sensorLookup, $"{corePrefix}.cpuSecondsNice", core.SecondsNice);
            AddMeasureIfEnabled(measures, sensorLookup, $"{corePrefix}.cpuSecondsSystem", core.SecondsSystem);
            AddMeasureIfEnabled(measures, sensorLookup, $"{corePrefix}.cpuSecondsIdle", core.SecondsIdle);
            AddMeasureIfEnabled(measures, sensorLookup, $"{corePrefix}.cpuSecondsIowait", core.SecondsIowait);
            AddMeasureIfEnabled(measures, sensorLookup, $"{corePrefix}.cpuSecondsIrq", core.SecondsIrq);
            AddMeasureIfEnabled(measures, sensorLookup, $"{corePrefix}.cpuSecondsSoftirq", core.SecondsSoftirq);
            AddMeasureIfEnabled(measures, sensorLookup, $"{corePrefix}.cpuSecondsSteal", core.SecondsSteal);
            AddMeasureIfEnabled(measures, sensorLookup, $"{corePrefix}.cpuSecondsTotal", core.SecondsTotal);
        }

        // CPU load averages
        AddMeasureIfEnabled(measures, sensorLookup, "cpu.cpuLoad1", rawData.Cpu.Load1);
        AddMeasureIfEnabled(measures, sensorLookup, "cpu.cpuLoad5", rawData.Cpu.Load5);
        AddMeasureIfEnabled(measures, sensorLookup, "cpu.cpuLoad15", rawData.Cpu.Load15);
        AddMeasureIfEnabled(measures, sensorLookup, "cpu.cpuContextSwitchesTotal", rawData.Cpu.ContextSwitchesTotal);

        // GPU metrics
        AddMeasureIfEnabled(measures, sensorLookup, "gpu.gpuUtilization", rawData.Gpu.Utilization);

        // RAM metrics
        AddMeasureIfEnabled(measures, sensorLookup, "ram.ramMemAvailableBytes", rawData.Ram.MemAvailableBytes);
        AddMeasureIfEnabled(measures, sensorLookup, "ram.ramMemFreeBytes", rawData.Ram.MemFreeBytes);
        AddMeasureIfEnabled(measures, sensorLookup, "ram.ramBuffersBytes", rawData.Ram.BuffersBytes);
        AddMeasureIfEnabled(measures, sensorLookup, "ram.ramCachedBytes", rawData.Ram.CachedBytes);
        AddMeasureIfEnabled(measures, sensorLookup, "ram.ramSwapTotalBytes", rawData.Ram.SwapTotalBytes);
        AddMeasureIfEnabled(measures, sensorLookup, "ram.ramSwapFreeBytes", rawData.Ram.SwapFreeBytes);

        // Disk metrics (per device)
        foreach (var disk in rawData.Disks)
        {
            var diskPrefix = $"disk.{disk.DeviceName}";
            AddMeasureIfEnabled(measures, sensorLookup, $"{diskPrefix}.diskFilesystemAvailBytes", disk.FilesystemAvailBytes);
            AddMeasureIfEnabled(measures, sensorLookup, $"{diskPrefix}.diskFilesystemFreeBytes", disk.FilesystemFreeBytes);
            AddMeasureIfEnabled(measures, sensorLookup, $"{diskPrefix}.diskFilesystemFilesFree", disk.FilesystemFilesFree);
            AddMeasureIfEnabled(measures, sensorLookup, $"{diskPrefix}.diskFilesystemFiles", disk.FilesystemFiles);
            AddMeasureIfEnabled(measures, sensorLookup, $"{diskPrefix}.diskReadsCompletedTotal", disk.ReadsCompletedTotal);
            AddMeasureIfEnabled(measures, sensorLookup, $"{diskPrefix}.diskWritesCompletedTotal", disk.WritesCompletedTotal);
            AddMeasureIfEnabled(measures, sensorLookup, $"{diskPrefix}.diskReadBytesTotal", disk.ReadBytesTotal);
            AddMeasureIfEnabled(measures, sensorLookup, $"{diskPrefix}.diskWrittenBytesTotal", disk.WrittenBytesTotal);
            AddMeasureIfEnabled(measures, sensorLookup, $"{diskPrefix}.diskIOTimeSecondsTotal", disk.IOTimeSecondsTotal);
        }

        // Network metrics (per interface)
        foreach (var network in rawData.Networks)
        {
            var netPrefix = $"network.{network.InterfaceName}";
            AddMeasureIfEnabled(measures, sensorLookup, $"{netPrefix}.networkReceiveBytesTotal", network.ReceiveBytesTotal);
            AddMeasureIfEnabled(measures, sensorLookup, $"{netPrefix}.networkTransmitBytesTotal", network.TransmitBytesTotal);
            AddMeasureIfEnabled(measures, sensorLookup, $"{netPrefix}.networkReceiveErrsTotal", network.ReceiveErrsTotal);
            AddMeasureIfEnabled(measures, sensorLookup, $"{netPrefix}.networkTransmitErrsTotal", network.TransmitErrsTotal);
        }

        // System metrics
        AddMeasureIfEnabled(measures, sensorLookup, "system.systemTimeSeconds", rawData.System.TimeSeconds);
        AddMeasureIfEnabled(measures, sensorLookup, "system.systemTimexOffsetSeconds", rawData.System.TimexOffsetSeconds);
        AddMeasureIfEnabled(measures, sensorLookup, "system.systemBootTimeSeconds", rawData.System.BootTimeSeconds);
        AddMeasureIfEnabled(measures, sensorLookup, "system.systemFilefdAllocated", rawData.System.FilefdAllocated);
        AddMeasureIfEnabled(measures, sensorLookup, "system.systemFilefdMaximum", rawData.System.FilefdMaximum);
        AddMeasureIfEnabled(measures, sensorLookup, "system.systemProcsRunning", rawData.System.ProcsRunning);
        AddMeasureIfEnabled(measures, sensorLookup, "system.systemProcsBlocked", rawData.System.ProcsBlocked);
        AddMeasureIfEnabled(measures, sensorLookup, "system.systemIntrTotal", rawData.System.IntrTotal);

        return measures;
    }

    private static void AddMeasureIfEnabled(
        List<TelemetryMeasure> measures,
        Dictionary<string, string> sensorLookup,
        string sensorName,
        object value)
    {
        if (sensorLookup.TryGetValue(sensorName, out var resourceId))
        {
            measures.Add(new TelemetryMeasure
            {
                ResourceId = resourceId,
                Value = value
            });
        }
    }
}
