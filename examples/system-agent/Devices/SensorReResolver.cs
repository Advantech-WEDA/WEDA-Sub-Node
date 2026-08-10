using Microsoft.Extensions.Logging;

using SystemAgentExample.Communication;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Utilities;

namespace SystemAgentExample.Devices;

/// <summary>
/// Re-applies sensor resolution to a live <see cref="DeviceConfiguration"/> after the
/// framework has replaced its sensor list with the cloud's desired state.
/// </summary>
/// <remarks>
/// <para>
/// Startup resolution happens once in <see cref="SystemAgentDeviceBase"/>'s constructor.
/// A cloud <c>device-config</c> update, however, is applied in REPLACE mode by
/// <c>ConfigurationUpdateHelper.ReplaceSensors</c>: every sensor absent from the desired
/// state is removed and every sensor present is added. The cloud's desired state holds the
/// original <em>template</em> sensors from <c>devicecfg.json</c> (e.g. <c>network_bytes_sent</c>
/// with <c>Interfaces: []</c>), so the per-resource sensors produced at startup
/// (<c>network_bytes_sent_eth0</c>, …) are dropped and the templates come back unresolved.
/// </para>
/// <para>
/// The parser then skips those templates on every poll ("missing Interface parameter"),
/// silently stopping their telemetry until the process restarts. Re-resolving after each
/// applied update restores the per-resource sensors.
/// </para>
/// </remarks>
internal static class SensorReResolver
{
    /// <summary>
    /// Re-normalizes and re-resolves <paramref name="configuration"/>'s sensors in place.
    /// </summary>
    /// <param name="configuration">The live device configuration to update.</param>
    /// <param name="resources">Freshly discovered system resources.</param>
    /// <param name="logger">Logger for resolution diagnostics.</param>
    /// <returns>
    /// <c>true</c> when the sensor list actually changed and the caller must restart
    /// background tasks; <c>false</c> when nothing needed resolving.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="configuration"/> or <paramref name="resources"/> is null.
    /// </exception>
    internal static bool ReResolve(
        DeviceConfiguration configuration,
        DiscoveredResources resources,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(resources);

        // Cheap pre-filter: nothing to do when every sensor is already bound to a resource.
        if (!SensorResolver.NeedsResolution(configuration.Sensors))
            return false;

        // No IConfigurationSection here: it is indexed positionally against devicecfg.json,
        // and after a cloud update the live sensor list no longer matches that ordering.
        // Cloud-delivered Parameters arrive as native types, which the normalizer handles.
        ParameterNormalizer.Normalize(configuration.Sensors);

        var resolved = SensorResolver.Resolve(configuration.Sensors, resources, logger);

        if (!HasChanged(configuration.Sensors, resolved))
        {
            // Templates were present but no resources were discovered for them —
            // SensorResolver already logged a warning and kept them as-is.
            return false;
        }

        AssignResourceIds(configuration, resolved, logger);

        configuration.Sensors = resolved;
        configuration.InvalidateSensorLookup();

        return true;
    }

    private static bool HasChanged(IReadOnlyList<Sensor> before, IReadOnlyList<Sensor> after)
    {
        if (before.Count != after.Count)
            return true;

        return !before.Select(s => s.Name).SequenceEqual(after.Select(s => s.Name), StringComparer.Ordinal);
    }

    /// <summary>
    /// Assigns ResourceIds to sensors newly created by resolution.
    /// </summary>
    /// <remarks>
    /// At startup the framework enriches the configuration (assigning ResourceIds) <em>after</em>
    /// resolution has run. On the runtime path enrichment has already happened, so sensors cloned
    /// from a template carry no ResourceId and would be unaddressable for telemetry. This mirrors
    /// <c>DeviceInitializer.EnrichConfiguration</c>'s formula so the two paths agree.
    /// </remarks>
    private static void AssignResourceIds(
        DeviceConfiguration configuration,
        IReadOnlyList<Sensor> sensors,
        ILogger logger)
    {
        var deviceId = configuration.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            // Enrichment has not run yet (constructor path) — the framework assigns them shortly.
            logger.LogDebug("DeviceId not yet assigned, skipping ResourceId generation for resolved sensors");
            return;
        }

        var assigned = 0;
        foreach (var sensor in sensors)
        {
            if (!string.IsNullOrWhiteSpace(sensor.ResourceId))
                continue;

            sensor.ResourceId = ResourceIdGenerator.GenerateSensorResourceId(
                deviceId,
                configuration.DeviceName,
                sensor.Name,
                groupId: "weda");
            sensor.DeviceResourceId = deviceId;
            assigned++;
        }

        if (assigned > 0)
            logger.LogDebug("Generated ResourceIds for {Count} newly resolved sensors", assigned);
    }
}
