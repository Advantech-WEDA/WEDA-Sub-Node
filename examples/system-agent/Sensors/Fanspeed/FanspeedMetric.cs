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

    /// <summary>
    /// Fans to read, by the names the hardware driver reports. Null / empty = all
    /// discovered fans (runtime fans out to one sensor per fan).
    /// </summary>
    [JsonPropertyName("sources")]
    public List<string>? Sources { get; init; }

    /// <summary>
    /// The single fan this sensor reads. Set by the runtime on each per-fan sensor.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }
}

public class FanspeedMetricSensor : IConfigurableSensor<FanspeedMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "fanspeed-metric";
    public static string? Description   => "Fan rotational speed (RPM), per discovered fan.";
}
