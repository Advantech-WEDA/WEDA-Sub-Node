using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Sensors;

public enum NetworkMetricType { network }

public enum NetworkMetricName { bytes_sent, bytes_received, packets_sent, packets_received, errors }

public class NetworkMetricParameters
{
    [Required, JsonPropertyName("metricType")]
    public NetworkMetricType MetricType { get; init; }

    [Required, JsonPropertyName("metricName")]
    public NetworkMetricName MetricName { get; init; }

    /// <summary>
    /// Network interfaces to query. Null / empty = all discovered interfaces
    /// (the runtime <c>SensorResolver</c> fans the sensor out to one per
    /// interface). Not required — the <c>MetricType=network</c> enum already
    /// disambiguates this Parameters shape from cpu / memory / disk / etc.
    /// </summary>
    [JsonPropertyName("interfaces")]
    public List<string>? Interfaces { get; init; }
}

public class NetworkMetricSensor : IConfigurableSensor<NetworkMetricParameters>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;
    public static string SensorTypeName => "network-metric";
    public static string? Description   => "Per-interface network throughput / error counters.";
}
