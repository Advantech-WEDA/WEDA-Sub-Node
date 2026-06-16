using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

// Enum members deliberately use lowercase / snake_case to match the wire
// format in devicecfg.json directly. JsonStringEnumConverter's
// case-insensitive parse handles "cpu" → Cpu shaped values; multi-word
// snake_case values like "context_switches" need the literal name match.

public enum CpuMetricType { cpu }

public enum CpuMetricName { usage, load1, load5, load15, context_switches }

public class CpuMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public CpuMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public CpuMetricName MetricName { get; init; }
}

public class CpuMetricSensor : IConfigurableSensor<CpuMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "cpu-metric";
    public static string? Description   => "CPU utilisation, load averages and context-switch counters.";
}
