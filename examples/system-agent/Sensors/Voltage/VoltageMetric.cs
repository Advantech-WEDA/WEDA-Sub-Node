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

    /// <summary>
    /// Voltage rails to read, by the names the hardware driver reports (e.g.
    /// <c>Vcore</c>, <c>12V</c>, <c>CMOS Battery</c>). Null / empty = all discovered
    /// rails (runtime fans out to one sensor per rail).
    /// </summary>
    [JsonPropertyName("sources")]
    public List<string>? Sources { get; init; }

    /// <summary>
    /// The single rail this sensor reads. Set by the runtime on each per-rail sensor.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }
}

public class VoltageMetricSensor : IConfigurableSensor<VoltageMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "voltage-metric";
    public static string? Description   => "Hardware voltage reading (volts), per discovered rail.";
}
