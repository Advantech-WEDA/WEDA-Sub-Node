using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

public enum SystemMetricType { system }

public enum SystemMetricName { time, boot_time }

public class SystemMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public SystemMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public SystemMetricName MetricName { get; init; }
}

public class SystemMetricSensor : IConfigurableSensor<SystemMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "system-metric";
    public static string? Description   => "System wall clock and boot time (Unix epoch seconds).";
}
