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
/// Architecture: Device -> Parser -> Communication (SystemResourceCollector)
/// </summary>
public class SystemMetricsParser : IRequestResponseProtocolParser
{
    private readonly LocalSystemCommunication _communication;
    private readonly SystemResourceCollector _collector;
    private readonly ILogger<SystemMetricsParser> _logger;

    public SystemMetricsParser(
        LocalSystemCommunication communication,
        ILogger<SystemMetricsParser> logger)
    {
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _collector = new SystemResourceCollector(logger);
    }

    public ICommunication Communication => _communication;

    public string ProtocolName => "LocalSystem";

    public IReadOnlyList<string> SupportedDataTypes => ["cpu", "gpu", "ram", "disk", "network", "system"];

    public bool SupportsBidirectional => false;

    public async Task<List<TelemetryMeasure>> ReadSensorDataAsync(
        SensorMapping sensorMapping,
        CancellationToken cancellationToken = default)
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

            // Convert raw data to telemetry measures using sensor mapping
            return ConvertToTelemetryMeasures(rawData, sensorMapping);
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

    private List<TelemetryMeasure> ConvertToTelemetryMeasures(
        SystemMetricsRawData rawData,
        SensorMapping sensorMapping)
    {
        var measures = new List<TelemetryMeasure>();

        // CPU metrics (per core)
        foreach (var core in rawData.Cpu.Cores)
        {
            var corePrefix = $"cpu{core.CoreNumber}";
            AddMeasureIfMapped(measures, sensorMapping, $"{corePrefix}.cpuSecondsUser", core.SecondsUser);
            AddMeasureIfMapped(measures, sensorMapping, $"{corePrefix}.cpuSecondsNice", core.SecondsNice);
            AddMeasureIfMapped(measures, sensorMapping, $"{corePrefix}.cpuSecondsSystem", core.SecondsSystem);
            AddMeasureIfMapped(measures, sensorMapping, $"{corePrefix}.cpuSecondsIdle", core.SecondsIdle);
            AddMeasureIfMapped(measures, sensorMapping, $"{corePrefix}.cpuSecondsIowait", core.SecondsIowait);
            AddMeasureIfMapped(measures, sensorMapping, $"{corePrefix}.cpuSecondsIrq", core.SecondsIrq);
            AddMeasureIfMapped(measures, sensorMapping, $"{corePrefix}.cpuSecondsSoftirq", core.SecondsSoftirq);
            AddMeasureIfMapped(measures, sensorMapping, $"{corePrefix}.cpuSecondsSteal", core.SecondsSteal);
            AddMeasureIfMapped(measures, sensorMapping, $"{corePrefix}.cpuSecondsTotal", core.SecondsTotal);
        }

        // CPU load averages
        AddMeasureIfMapped(measures, sensorMapping, "cpu.cpuLoad1", rawData.Cpu.Load1);
        AddMeasureIfMapped(measures, sensorMapping, "cpu.cpuLoad5", rawData.Cpu.Load5);
        AddMeasureIfMapped(measures, sensorMapping, "cpu.cpuLoad15", rawData.Cpu.Load15);
        AddMeasureIfMapped(measures, sensorMapping, "cpu.cpuContextSwitchesTotal", rawData.Cpu.ContextSwitchesTotal);

        // GPU metrics
        AddMeasureIfMapped(measures, sensorMapping, "gpu.gpuUtilization", rawData.Gpu.Utilization);

        // RAM metrics
        AddMeasureIfMapped(measures, sensorMapping, "ram.ramMemAvailableBytes", rawData.Ram.MemAvailableBytes);
        AddMeasureIfMapped(measures, sensorMapping, "ram.ramMemFreeBytes", rawData.Ram.MemFreeBytes);
        AddMeasureIfMapped(measures, sensorMapping, "ram.ramBuffersBytes", rawData.Ram.BuffersBytes);
        AddMeasureIfMapped(measures, sensorMapping, "ram.ramCachedBytes", rawData.Ram.CachedBytes);
        AddMeasureIfMapped(measures, sensorMapping, "ram.ramSwapTotalBytes", rawData.Ram.SwapTotalBytes);
        AddMeasureIfMapped(measures, sensorMapping, "ram.ramSwapFreeBytes", rawData.Ram.SwapFreeBytes);

        // Disk metrics (per device)
        foreach (var disk in rawData.Disks)
        {
            var diskPrefix = $"disk.{disk.DeviceName}";
            AddMeasureIfMapped(measures, sensorMapping, $"{diskPrefix}.diskFilesystemAvailBytes", disk.FilesystemAvailBytes);
            AddMeasureIfMapped(measures, sensorMapping, $"{diskPrefix}.diskFilesystemFreeBytes", disk.FilesystemFreeBytes);
            AddMeasureIfMapped(measures, sensorMapping, $"{diskPrefix}.diskFilesystemFilesFree", disk.FilesystemFilesFree);
            AddMeasureIfMapped(measures, sensorMapping, $"{diskPrefix}.diskFilesystemFiles", disk.FilesystemFiles);
            AddMeasureIfMapped(measures, sensorMapping, $"{diskPrefix}.diskReadsCompletedTotal", disk.ReadsCompletedTotal);
            AddMeasureIfMapped(measures, sensorMapping, $"{diskPrefix}.diskWritesCompletedTotal", disk.WritesCompletedTotal);
            AddMeasureIfMapped(measures, sensorMapping, $"{diskPrefix}.diskReadBytesTotal", disk.ReadBytesTotal);
            AddMeasureIfMapped(measures, sensorMapping, $"{diskPrefix}.diskWrittenBytesTotal", disk.WrittenBytesTotal);
            AddMeasureIfMapped(measures, sensorMapping, $"{diskPrefix}.diskIOTimeSecondsTotal", disk.IOTimeSecondsTotal);
        }

        // Network metrics (per interface)
        foreach (var network in rawData.Networks)
        {
            var netPrefix = $"network.{network.InterfaceName}";
            AddMeasureIfMapped(measures, sensorMapping, $"{netPrefix}.networkReceiveBytesTotal", network.ReceiveBytesTotal);
            AddMeasureIfMapped(measures, sensorMapping, $"{netPrefix}.networkTransmitBytesTotal", network.TransmitBytesTotal);
            AddMeasureIfMapped(measures, sensorMapping, $"{netPrefix}.networkReceiveErrsTotal", network.ReceiveErrsTotal);
            AddMeasureIfMapped(measures, sensorMapping, $"{netPrefix}.networkTransmitErrsTotal", network.TransmitErrsTotal);
        }

        // System metrics
        AddMeasureIfMapped(measures, sensorMapping, "system.systemTimeSeconds", rawData.System.TimeSeconds);
        AddMeasureIfMapped(measures, sensorMapping, "system.systemTimexOffsetSeconds", rawData.System.TimexOffsetSeconds);
        AddMeasureIfMapped(measures, sensorMapping, "system.systemBootTimeSeconds", rawData.System.BootTimeSeconds);
        AddMeasureIfMapped(measures, sensorMapping, "system.systemFilefdAllocated", rawData.System.FilefdAllocated);
        AddMeasureIfMapped(measures, sensorMapping, "system.systemFilefdMaximum", rawData.System.FilefdMaximum);
        AddMeasureIfMapped(measures, sensorMapping, "system.systemProcsRunning", rawData.System.ProcsRunning);
        AddMeasureIfMapped(measures, sensorMapping, "system.systemProcsBlocked", rawData.System.ProcsBlocked);
        AddMeasureIfMapped(measures, sensorMapping, "system.systemIntrTotal", rawData.System.IntrTotal);

        return measures;
    }

    private static void AddMeasureIfMapped(
        List<TelemetryMeasure> measures,
        SensorMapping sensorMapping,
        string fieldName,
        object value)
    {
        if (sensorMapping.FieldToResourceId.TryGetValue(fieldName, out var resourceId))
        {
            measures.Add(new TelemetryMeasure
            {
                ResourceId = resourceId,
                Value = value
            });
        }
    }
}
