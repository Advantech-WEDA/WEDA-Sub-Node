using System.Text.Json;

using Microsoft.Extensions.Logging;

using SystemAgentExample.Communication;
using SystemAgentExample.Protocols;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Devices;

/// <summary>
/// Expands template sensors (without specific resource identifiers) into
/// individual per-resource sensors based on discovered system resources.
/// Supports explicit resource lists (Interfaces/PinIds/MetricNames) and auto-detection.
/// </summary>
internal static class SensorExpander
{
    /// <summary>
    /// Expands sensors that lack specific resource identifiers into per-resource sensors.
    /// Each sensor is independently expanded based on its own MetricType.
    /// Priority: explicit array param > auto-detect from system.
    /// </summary>
    internal static List<Sensor> Expand(
        IEnumerable<Sensor> sensors,
        DiscoveredResources resources,
        ILogger logger)
    {
        return sensors
            .SelectMany(sensor => ExpandSensor(sensor, resources, logger))
            .ToList();
    }

    /// <summary>
    /// Expands a single sensor based on its MetricType.
    /// Returns one or more sensors: either the expanded per-resource sensors,
    /// or the original sensor unchanged if expansion is not applicable.
    /// </summary>
    private static List<Sensor> ExpandSensor(
        Sensor sensor,
        DiscoveredResources resources,
        ILogger logger)
    {
        var metricType = GetParam(sensor, "MetricType")?.ToLowerInvariant();

        return metricType switch
        {
            SupportedDataType.Network => ExpandNetwork(sensor, resources.NetworkInterfaces, logger),
            SupportedDataType.Gpio => ExpandGpio(sensor, resources.GpioPins, logger),
            SupportedDataType.Temperature => ExpandTemperature(sensor, resources.TemperatureSources, logger),
            _ => [sensor]
        };
    }

    private static List<Sensor> ExpandNetwork(Sensor sensor, IReadOnlyList<string> discovered, ILogger logger)
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
            "Expanding sensor '{Name}' into {Count} sensors for interfaces: {Interfaces}",
            sensor.Name, interfaces.Count, string.Join(", ", interfaces));

        return interfaces
            .Select(iface => CloneSensor(sensor, iface, "Interfaces", "Interface"))
            .ToList();
    }

    private static List<Sensor> ExpandGpio(Sensor sensor, IReadOnlyList<string> discovered, ILogger logger)
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
            "Expanding sensor '{Name}' into {Count} sensors for pins: {Pins}",
            sensor.Name, pins.Count, string.Join(", ", pins));

        return pins
            .Select(pin => CloneSensor(sensor, pin, "PinIds", "PinId"))
            .ToList();
    }

    private static List<Sensor> ExpandTemperature(Sensor sensor, IReadOnlyList<string> discovered, ILogger logger)
    {
        // Already bound to a specific source → keep as-is
        if (GetParam(sensor, "MetricName") != null)
            return [sensor];

        var sources = ResolveResourceList(sensor, "MetricNames", discovered);
        if (sources.Count == 0)
        {
            logger.LogWarning("No temperature sources discovered for sensor '{Name}', keeping as-is", sensor.Name);
            return [sensor];
        }

        logger.LogInformation(
            "Expanding sensor '{Name}' into {Count} sensors for sources: {Sources}",
            sensor.Name, sources.Count, string.Join(", ", sources));

        return sources
            .Select(source => CloneSensor(sensor, source, "MetricNames", "MetricName"))
            .ToList();
    }

    /// <summary>
    /// Resolves the resource list: explicit array param takes priority, otherwise use auto-discovered.
    /// </summary>
    private static IReadOnlyList<string> ResolveResourceList(
        Sensor sensor, string arrayParamKey, IReadOnlyList<string> discovered)
    {
        if (sensor.Parameters != null &&
            sensor.Parameters.TryGetValue(arrayParamKey, out var arrayValue))
        {
            var arr = arrayValue is JsonElement element
                ? element.Deserialize<string[]>()
                : JsonSerializer.Deserialize<string[]>(JsonSerializer.Serialize(arrayValue));

            if (arr is { Length: > 0 })
                return arr;
        }

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
