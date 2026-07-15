using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

public enum GpuMetricType { gpu }

public enum GpuMetricName { utilization }

public class GpuMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public GpuMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public GpuMetricName MetricName { get; init; }
}

public class GpuMetricSensor : IConfigurableSensor<GpuMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "gpu-metric";
    public static string? Description   => "GPU utilisation percentage.";
}
