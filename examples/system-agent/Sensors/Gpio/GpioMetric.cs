using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

public enum GpioMetricType { gpio }

public enum GpioMetricName { isSupported, pinState }

public class GpioMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public GpioMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public GpioMetricName MetricName { get; init; }

    /// <summary>
    /// Pins to query when <c>MetricName == pinState</c>. Absent / empty for
    /// <c>isSupported</c>. Optional (not [Required]) so both <c>gpio_isSupported</c>
    /// (no PinIds) and <c>gpio_pinState</c> (with PinIds) bind to the same POCO.
    /// </summary>
    [JsonPropertyName("pinIds")]
    public List<int>? PinIds { get; init; }
}

public class GpioMetricSensor : IConfigurableSensor<GpioMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "gpio-metric";
    public static string? Description   => "GPIO platform support flag and per-pin state.";
}
