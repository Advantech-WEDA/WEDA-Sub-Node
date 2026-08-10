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
    /// <remarks>
    /// An expanded template is kept in the result as a disabled capability-only entry,
    /// so the original sensor name (e.g. <c>network_bytes_sent</c>) still appears in
    /// <c>deviceCapabilities.sensors[]</c> alongside its per-resource clones. Clones
    /// whose names already exist in the input list are not generated again, which makes
    /// resolution idempotent over a list that already contains template + clones.
    /// </remarks>
    internal static List<Sensor> Resolve(
        IEnumerable<Sensor> sensors,
        DiscoveredResources resources,
        ILogger logger)
    {
        var sensorList = sensors.ToList();
        var existingNames = new HashSet<string>(
            sensorList.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);

        return sensorList
            .SelectMany(sensor => ResolveSensor(sensor, resources, existingNames, logger))
            .ToList();
    }

    /// <summary>
    /// Returns true when at least one sensor is still an unresolved template
    /// (Explicit list / Auto-detect mode) and would be expanded by <see cref="Resolve"/>.
    /// </summary>
    /// <remarks>
    /// Used by the runtime re-resolution path: a cloud configuration update replaces the
    /// live sensor list with the cloud's desired state, which holds the original template
    /// sensors (e.g. <c>network_bytes_sent</c> with <c>Interfaces: []</c>) rather than the
    /// per-resource sensors produced at startup. Without re-resolution those templates are
    /// polled unresolved and skipped by the parser, silently stopping their telemetry.
    /// </remarks>
    internal static bool NeedsResolution(IEnumerable<Sensor> sensors)
        => sensors.Any(IsUnresolvedTemplate);

    /// <summary>
    /// Returns true when a sensor is a template that <see cref="Resolve"/> would expand.
    /// Single source of truth for the Bound-mode checks used by the Resolve* methods.
    /// </summary>
    private static bool IsUnresolvedTemplate(Sensor sensor)
    {
        var metricType = GetParam(sensor, "MetricType")?.ToLowerInvariant();

        return metricType switch
        {
            SupportedDataType.Network => !IsNetworkBound(sensor),
            SupportedDataType.Gpio => IsGpioPinStateSensor(sensor) && !IsGpioBound(sensor),
            SupportedDataType.Temperature => !IsTemperatureBound(sensor),
            _ => false
        };
    }

    /// <summary>Already bound to a specific interface.</summary>
    private static bool IsNetworkBound(Sensor sensor) => GetParam(sensor, "Interface") != null;

    /// <summary>Only the 'pinState' metric is per-pin; other GPIO metrics are device-wide.</summary>
    private static bool IsGpioPinStateSensor(Sensor sensor)
        => GetParam(sensor, "MetricName")?.ToLowerInvariant() == "pinstate";

    /// <summary>Already bound to a specific pin.</summary>
    private static bool IsGpioBound(Sensor sensor) => GetParam(sensor, "PinId") != null;

    /// <summary>
    /// Already bound to a specific source. Includes the v1.0 compatibility rule:
    /// a MetricName without Source/Sources means the MetricName itself acts as the source.
    /// </summary>
    private static bool IsTemperatureBound(Sensor sensor)
    {
        if (GetParam(sensor, "Source") != null)
            return true;

        return GetParam(sensor, "Sources") == null && GetParam(sensor, "MetricName") != null;
    }

    /// <summary>
    /// Resolves a single sensor based on its MetricType.
    /// Returns one or more sensors: either the resolved per-resource sensors,
    /// or the original sensor unchanged if already in Bound mode.
    /// </summary>
    private static List<Sensor> ResolveSensor(
        Sensor sensor,
        DiscoveredResources resources,
        ISet<string> existingNames,
        ILogger logger)
    {
        var metricType = GetParam(sensor, "MetricType")?.ToLowerInvariant();

        return metricType switch
        {
            SupportedDataType.Network => ResolveNetwork(sensor, resources.NetworkInterfaces, existingNames, logger),
            SupportedDataType.Gpio => ResolveGpio(sensor, resources.GpioPins, existingNames, logger),
            SupportedDataType.Temperature => ResolveTemperature(sensor, resources.TemperatureSources, existingNames, logger),
            _ => [sensor]
        };
    }

    private static List<Sensor> ResolveNetwork(
        Sensor sensor, IReadOnlyList<string> discovered, ISet<string> existingNames, ILogger logger)
    {
        // Already bound to a specific interface → keep as-is
        if (IsNetworkBound(sensor))
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

        return ExpandTemplate(sensor, interfaces, "Interfaces", "Interface", existingNames);
    }

    private static List<Sensor> ResolveGpio(
        Sensor sensor, IReadOnlyList<string> discovered, ISet<string> existingNames, ILogger logger)
    {
        if (!IsGpioPinStateSensor(sensor))
            return [sensor];

        // Already bound to a specific pin → keep as-is
        if (IsGpioBound(sensor))
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

        return ExpandTemplate(sensor, pins, "PinIds", "PinId", existingNames);
    }

    private static List<Sensor> ResolveTemperature(
        Sensor sensor, IReadOnlyList<string> discovered, ISet<string> existingNames, ILogger logger)
    {
        // Already bound to a specific source, or v1.0 compat where MetricName acts as the source
        if (IsTemperatureBound(sensor))
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

        return ExpandTemplate(sensor, sources, "Sources", "Source", existingNames);
    }

    /// <summary>
    /// Expands a template into per-resource clones, keeping the template itself at the
    /// head of the result as a disabled capability-only entry — disabled sensors are
    /// excluded from sampling but still uploaded in <c>deviceCapabilities.sensors[]</c>,
    /// which is exactly why the original name is kept. Clones whose names already exist
    /// in the input list are skipped: the live sensor passes through its own resolution
    /// unchanged, so re-resolving a template+clones list never duplicates sensors.
    /// </summary>
    private static List<Sensor> ExpandTemplate(
        Sensor template,
        IReadOnlyList<string> resourceNames,
        string arrayParamKey,
        string singleParamKey,
        ISet<string> existingNames)
    {
        var resolved = new List<Sensor> { AsCapabilityTemplate(template) };

        resolved.AddRange(resourceNames
            .Select(resource => CloneSensor(template, resource, arrayParamKey, singleParamKey))
            .Where(clone => !existingNames.Contains(clone.Name)));

        return resolved;
    }

    /// <summary>
    /// Returns the template as a capability-only entry: same identity (name, DTMI,
    /// ResourceId) and parameters, but with reporting disabled so it is never polled —
    /// the per-resource clones carry the live telemetry. A template that is already
    /// disabled is returned as-is to preserve object identity across re-resolutions.
    /// </summary>
    private static Sensor AsCapabilityTemplate(Sensor template)
    {
        if (!template.Report.Enabled)
            return template;

        return new Sensor
        {
            Name = template.Name,
            Dtmi = template.Dtmi,
            ResourceId = template.ResourceId,
            SensorGroup = template.SensorGroup,
            SensorInfo = template.SensorInfo,
            Parameters = template.Parameters,
            Report = new SensorReport
            {
                Enabled = false,
                Interval = template.Report.Interval,
                Unit = template.Report.Unit
            },
            Record = template.Record,
            Metadata = template.Metadata,
            DeviceResourceId = template.DeviceResourceId,
            DeviceEnabled = template.DeviceEnabled,
        };
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
