using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

public enum WatchdogMetricType { watchdog }

public enum WatchdogMetricName { isSupported }

public class WatchdogMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public WatchdogMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public WatchdogMetricName MetricName { get; init; }
}

public class WatchdogMetricSensor : IConfigurableSensor<WatchdogMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "watchdog-metric";
    public static string? Description   => "Watchdog hardware presence flag.";
}
