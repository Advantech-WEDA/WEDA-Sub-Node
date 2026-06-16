using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

public enum VoltageMetricType { voltage }

public enum VoltageMetricName { voltage }

public class VoltageMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public VoltageMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public VoltageMetricName MetricName { get; init; }
}

public class VoltageMetricSensor : IConfigurableSensor<VoltageMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "voltage-metric";
    public static string? Description   => "Hardware voltage reading (volts).";
}
