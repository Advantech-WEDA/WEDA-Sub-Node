using Microsoft.Extensions.Logging;

using SystemAgentExample.Communication;
using SystemAgentExample.Protocols;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Devices;

/// <summary>
/// Resolves sensor configuration mode (Bound / Explicit list / Auto-detect) and
/// produces per-resource sensors from template sensors based on discovered system resources.
/// </summary>
internal static class SensorResolver
{
    /// <summary>
    /// Resolves each sensor's mode (Bound / Explicit list / Auto-detect) and produces
    /// per-resource sensors accordingly. Each sensor is independently resolved.
    /// Priority: Bound > Explicit list > Auto-detect.
    /// </summary>
    internal static List<Sensor> Resolve(
        IEnumerable<Sensor> sensors,
        DiscoveredResources resources,
        ILogger logger)
    {
        return sensors
            .SelectMany(sensor => ResolveSensor(sensor, resources, logger))
            .ToList();
    }

    /// <summary>
    /// Resolves a single sensor based on its MetricType.
    /// Returns one or more sensors: either the resolved per-resource sensors,
    /// or the original sensor unchanged if already in Bound mode.
    /// </summary>
    private static List<Sensor> ResolveSensor(
        Sensor sensor,
        DiscoveredResources resources,
        ILogger logger)
    {
        var metricType = GetParam(sensor, "MetricType")?.ToLowerInvariant();

        return metricType switch
        {
            SupportedDataType.Network => ResolveNetwork(sensor, resources.NetworkInterfaces, logger),
            SupportedDataType.Gpio => ResolveGpio(sensor, resources.GpioPins, logger),
            SupportedDataType.Temperature => ResolveTemperature(sensor, resources.TemperatureSources, logger),
            _ => [sensor]
        };
    }

    private static List<Sensor> ResolveNetwork(Sensor sensor, IReadOnlyList<string> discovered, ILogger logger)
    {
        // Already bound to a specific interface → keep as-is
        if (GetParam(sensor, "Interface") != null)
            return [sensor];

        var interfaces = ResolveResourceList(sensor, "Interfaces", discovered);
        if (interfaces.Count == 0)
        {
            logger.LogWarning("No network interfaces discovered for sensor '{Name}', keeping as-is", sensor.Name);
            return [sensor];
        }

        logger.LogInformation(
            "Resolving sensor '{Name}' into {Count} sensors for interfaces: {Interfaces}",
            sensor.Name, interfaces.Count, string.Join(", ", interfaces));

        return interfaces
            .Select(iface => CloneSensor(sensor, iface, "Interfaces", "Interface"))
            .ToList();
    }

    private static List<Sensor> ResolveGpio(Sensor sensor, IReadOnlyList<string> discovered, ILogger logger)
    {
        var metricName = GetParam(sensor, "MetricName")?.ToLowerInvariant();
        if (metricName != "pinstate")
            return [sensor];

        // Already bound to a specific pin → keep as-is
        if (GetParam(sensor, "PinId") != null)
            return [sensor];

        var pins = ResolveResourceList(sensor, "PinIds", discovered);
        if (pins.Count == 0)
        {
            logger.LogWarning("No GPIO pins discovered for sensor '{Name}', keeping as-is", sensor.Name);
            return [sensor];
        }

        logger.LogInformation(
            "Resolving sensor '{Name}' into {Count} sensors for pins: {Pins}",
            sensor.Name, pins.Count, string.Join(", ", pins));

        return pins
            .Select(pin => CloneSensor(sensor, pin, "PinIds", "PinId"))
            .ToList();
    }

    private static List<Sensor> ResolveTemperature(Sensor sensor, IReadOnlyList<string> discovered, ILogger logger)
    {
        // Already bound to a specific source → keep as-is
        if (GetParam(sensor, "Source") != null)
            return [sensor];

        // v1.0 compat: MetricName without Source means already bound (MetricName acts as source)
        if (GetParam(sensor, "Source") == null && GetParam(sensor, "Sources") == null
            && GetParam(sensor, "MetricName") != null)
            return [sensor];

        var sources = ResolveResourceList(sensor, "Sources", discovered);
        if (sources.Count == 0)
        {
            logger.LogWarning("No temperature sources discovered for sensor '{Name}', keeping as-is", sensor.Name);
            return [sensor];
        }

        logger.LogInformation(
            "Resolving sensor '{Name}' into {Count} sensors for sources: {Sources}",
            sensor.Name, sources.Count, string.Join(", ", sources));

        return sources
            .Select(source => CloneSensor(sensor, source, "Sources", "Source"))
            .ToList();
    }

    /// <summary>
    /// Resolves the resource list from a pre-normalized string[] parameter.
    /// Explicit list mode: array has elements → use it.
    /// Auto-detect mode: empty array or missing key → fall back to discovered resources.
    /// Expects ParameterNormalizer to have already converted all formats to string[].
    /// </summary>
    private static IReadOnlyList<string> ResolveResourceList(
        Sensor sensor, string arrayParamKey, IReadOnlyList<string> discovered)
    {
        if (sensor.Parameters == null ||
            !sensor.Parameters.TryGetValue(arrayParamKey, out var arrayValue))
        {
            return discovered;
        }

        if (arrayValue is string[] arr && arr.Length > 0)
            return arr;

        // Empty array or removed key → Auto-detect mode
        return discovered;
    }

    /// <summary>
    /// Creates a new sensor for a specific resource, copying config from the template.
    /// </summary>
    private static Sensor CloneSensor(
        Sensor template, string resourceName, string arrayParamKey, string singleParamKey)
    {
        var parameters = new Dictionary<string, object>(
            template.Parameters?.Where(p => p.Key != arrayParamKey) 
                ?? Enumerable.Empty<KeyValuePair<string, object>>())
        {
            [singleParamKey] = resourceName
        };

        return new Sensor
        {
            Name = $"{template.Name}_{resourceName}",
            Dtmi = template.Dtmi,
            SensorGroup = template.SensorGroup,
            SensorInfo = new SensorInfo
            {
                Schema = template.SensorInfo.Schema,
                DisplayName = $"{resourceName} {template.SensorInfo.DisplayName ?? template.Name}",
                Description = $"{template.SensorInfo.Description ?? template.Name} ({resourceName})"
            },
            Parameters = parameters,
            Report = new SensorReport
            {
                Enabled = template.Report.Enabled,
                Interval = template.Report.Interval,
                Unit = template.Report.Unit
            },
            Record = template.Record,
            Metadata = template.Metadata,
            DeviceResourceId = template.DeviceResourceId,
            DeviceEnabled = template.DeviceEnabled,
        };
    }

    private static string? GetParam(Sensor sensor, string key)
    {
        if (sensor.Parameters == null) return null;
        return sensor.Parameters.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}
