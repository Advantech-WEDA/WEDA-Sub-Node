using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

public enum MemoryMetricType { memory }

public enum MemoryMetricName { total, available, used, cached, swap_total, swap_free }

public class MemoryMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public MemoryMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public MemoryMetricName MetricName { get; init; }
}

public class MemoryMetricSensor : IConfigurableSensor<MemoryMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "memory-metric";
    public static string? Description   => "System and swap memory counters in bytes.";
}
