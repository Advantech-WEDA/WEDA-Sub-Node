using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

public enum DiskMetricType { disk }

public enum DiskMetricName { total, available, used, usage_percent }

public class DiskMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public DiskMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public DiskMetricName MetricName { get; init; }

    /// <summary>
    /// Mount point to query — required by the runtime <c>ResourceCollector</c>.
    /// Its presence (vs absence on cpu / memory etc.) is what disambiguates
    /// disk Parameters from the simple metric families during typed dispatch.
    /// </summary>
    [Required, JsonPropertyName("mountPoint")]
    public string MountPoint { get; init; } = string.Empty;
}

public class DiskMetricSensor : IConfigurableSensor<DiskMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "disk-metric";
    public static string? Description   => "Per-mount-point disk capacity counters (bytes / percent).";
}
