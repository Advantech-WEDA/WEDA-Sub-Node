using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Protocols;

/// <summary>
/// Shared rules for onboard (hardware driver) sensors — temperature, voltage and fan
/// speed — whose collectors return one reading per source, keyed by the source name
/// the driver reports (e.g. <c>Vcore</c>, <c>CMOS Battery</c>).
/// </summary>
/// <remarks>
/// A sensor whose <c>MetricName</c> is the generic name for its type (<c>therm</c>,
/// <c>voltage</c>, <c>fanspeed</c>) is a template: <c>SensorResolver</c> expands it
/// into one sensor per discovered source and puts the source in <c>Source</c>. Any
/// other <c>MetricName</c> names a source directly (the v1.0 configuration style).
/// </remarks>
internal static class OnboardMetricLookup
{
    private static readonly Dictionary<string, string> GenericMetricNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [SupportedDataType.Temperature] = "therm",
        [SupportedDataType.Voltage] = "voltage",
        [SupportedDataType.Fanspeed] = "fanspeed",
    };

    /// <summary>
    /// The generic <c>MetricName</c> of an onboard metric type, or <c>null</c> when
    /// <paramref name="metricType"/> is not an onboard type.
    /// </summary>
    public static string? GenericMetricName(string? metricType)
        => metricType != null && GenericMetricNames.TryGetValue(metricType, out var name) ? name : null;

    /// <summary>
    /// True when the sensor already targets one source: it has a <c>Source</c>, or it
    /// has no <c>Sources</c> list and a <c>MetricName</c> other than the generic one.
    /// </summary>
    public static bool IsBound(Sensor sensor, string genericMetricName)
    {
        if (GetParam(sensor, "Source") != null)
            return true;

        var metricName = GetParam(sensor, "MetricName");
        return GetParam(sensor, "Sources") == null
            && metricName != null
            && !metricName.Equals(genericMetricName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Picks the sensor's reading from the per-source readings.
    /// </summary>
    /// <returns>
    /// The reading for the sensor's source (case-insensitive match). For a template
    /// that was never expanded, the only reading when there is exactly one. Otherwise
    /// <c>null</c>, which the parser skips rather than publishing a placeholder.
    /// </returns>
    public static double? Find(IReadOnlyDictionary<string, double?>? readings, Sensor sensor, string genericMetricName)
    {
        if (readings == null || readings.Count == 0)
            return null;

        var source = GetParam(sensor, "Source");
        if (source == null)
        {
            var metricName = GetParam(sensor, "MetricName");
            if (metricName == null || metricName.Equals(genericMetricName, StringComparison.OrdinalIgnoreCase))
            {
                // A template that was never expanded: only an unambiguous reading will do
                return readings.Count == 1 ? readings.Values.First() : null;
            }

            source = metricName;
        }

        if (readings.TryGetValue(source, out var exact))
            return exact;

        foreach (var (name, value) in readings)
        {
            if (name.Equals(source, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return null;
    }

    private static string? GetParam(Sensor sensor, string key)
        => sensor.Parameters != null && sensor.Parameters.TryGetValue(key, out var value) ? value?.ToString() : null;
}
