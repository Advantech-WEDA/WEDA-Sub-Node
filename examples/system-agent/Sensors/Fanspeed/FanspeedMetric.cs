using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

public enum FanspeedMetricType { fanspeed }

public enum FanspeedMetricName { fanspeed }

public class FanspeedMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public FanspeedMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public FanspeedMetricName MetricName { get; init; }
}

public class FanspeedMetricSensor : IConfigurableSensor<FanspeedMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "fanspeed-metric";
    public static string? Description   => "Fan rotational speed (RPM).";
}
