using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

public enum HwinfoMetricType { hwinfo }

public enum HwinfoMetricName { motherboardname, manufacturer, biosrevision }

public class HwinfoMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public HwinfoMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public HwinfoMetricName MetricName { get; init; }
}

public class HwinfoMetricSensor : IConfigurableSensor<HwinfoMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "hwinfo-metric";
    public static string? Description   => "Hardware information strings — motherboard / manufacturer / BIOS revision.";
}
