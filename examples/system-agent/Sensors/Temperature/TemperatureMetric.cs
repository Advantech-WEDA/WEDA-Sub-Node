using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

public enum TemperatureMetricType { temperature }

public enum TemperatureMetricName { therm }

public class TemperatureMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public TemperatureMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public TemperatureMetricName MetricName { get; init; }

    /// <summary>
    /// Thermal sources to read. Null / empty = all discovered sources (runtime
    /// fans out to one sensor per source). Not required — the
    /// <c>MetricType=temperature</c> enum already disambiguates this shape.
    /// </summary>
    [JsonPropertyName("sources")]
    public List<string>? Sources { get; init; }
}

public class TemperatureMetricSensor : IConfigurableSensor<TemperatureMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "temperature-metric";
    public static string? Description   => "Hardware temperature sensor reading (Celsius), per discovered source.";
}
