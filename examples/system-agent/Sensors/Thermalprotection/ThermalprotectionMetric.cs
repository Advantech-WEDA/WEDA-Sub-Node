using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

public enum ThermalprotectionMetricType { thermalprotection }

public enum ThermalprotectionMetricName { isSupported }

public class ThermalprotectionMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public ThermalprotectionMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public ThermalprotectionMetricName MetricName { get; init; }
}

public class ThermalprotectionMetricSensor : IConfigurableSensor<ThermalprotectionMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "thermalprotection-metric";
    public static string? Description   => "Thermal-protection hardware presence flag.";
}
