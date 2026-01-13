using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Configuration;

/// <summary>
/// Backup snapshot of device configuration for rollback purposes.
/// </summary>
public class DeviceConfigurationBackup
{
    public int ReportHealthPeriod { get; set; }
    public int ReportConfigurationPeriod { get; set; }
    public List<SensorReportBackup> SensorBackups { get; set; } = [];
}

/// <summary>
/// Backup snapshot of sensor configuration.
/// </summary>
public class SensorReportBackup
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public double Interval { get; set; }
    public string? Unit { get; set; }
    public ThresholdConfig? Thresholds { get; set; }
}
