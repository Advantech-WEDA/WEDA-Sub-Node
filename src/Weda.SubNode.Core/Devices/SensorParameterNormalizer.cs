using Microsoft.Extensions.Configuration;

using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Repairs <see cref="Weda.SubNode.Abstractions.Telemetry.Sensor.Parameters"/>
/// values that <see cref="IConfiguration"/> binding mangles when the source JSON
/// contains arrays.
///
/// <para>
/// <c>Get&lt;Dictionary&lt;string, object&gt;&gt;</c> on a configuration section
/// does NOT round-trip JSON arrays:
/// <list type="bullet">
///   <item>Empty arrays (<c>"Interfaces": []</c>) bind to an empty string <c>""</c>.</item>
///   <item>Populated arrays (<c>"Interfaces": ["eth0"]</c>) bind to an opaque
///         <c>System.Object</c> with content lost.</item>
/// </list>
/// </para>
///
/// <para>This helper walks the raw <see cref="IConfigurationSection"/> children
/// for each sensor's Parameters block — IConfiguration stores arrays as indexed
/// children (<c>Interfaces:0 = "eth0"</c>, <c>Interfaces:1 = "eth1"</c>) — and
/// replaces the broken dictionary value with a clean <c>string[]</c>. Scalars
/// (correctly bound) and nested objects (rare) are left alone.</para>
///
/// <para>Called by <c>WedaApplicationContext</c> immediately after binding each
/// <see cref="DeviceConfiguration"/> and before
/// <see cref="DeviceConfiguration.InitializeDtdl"/> — so the typed
/// <c>SensorTypeRegistry</c> dispatch sees the recovered values.</para>
/// </summary>
public static class SensorParameterNormalizer
{
    /// <summary>
    /// Normalises array-valued sensor Parameters in-place using the raw
    /// device-section as the source of truth.
    /// </summary>
    /// <param name="deviceConfig">The freshly-bound device configuration.</param>
    /// <param name="deviceSection">
    /// The <see cref="IConfigurationSection"/> the device configuration was
    /// bound from — i.e. <c>configuration.GetSection("DeviceConfigs:&lt;key&gt;")</c>.
    /// </param>
    public static void NormalizeArrayParameters(
        DeviceConfiguration deviceConfig, IConfigurationSection deviceSection)
    {
        var sensorsSection = deviceSection.GetSection("Sensors");
        if (!sensorsSection.Exists()) return;

        for (var i = 0; i < deviceConfig.Sensors.Count; i++)
        {
            var sensor = deviceConfig.Sensors[i];
            if (sensor.Parameters is null) continue;

            var paramsSection = sensorsSection.GetSection($"{i}:Parameters");
            if (!paramsSection.Exists()) continue;

            // Pass 1 — recover POPULATED arrays from the raw section's indexed children.
            //   IConfiguration drops populated array content from Dictionary binding;
            //   the raw section preserves it as numbered children (e.g. "Interfaces:0").
            foreach (var paramSection in paramsSection.GetChildren())
            {
                var children = paramSection.GetChildren().ToArray();
                if (children.Length > 0 && children.All(c => int.TryParse(c.Key, out _)))
                {
                    sensor.Parameters[paramSection.Key] = children
                        .Where(c => c.Value is not null)
                        .Select(c => c.Value!)
                        .ToArray();
                }
            }

            // Pass 2 — fix EMPTY arrays that IConfiguration bound as the string "".
            //   An empty JSON array in source becomes a zero-length string in the
            //   bound dict; a real scalar "" is meaningless as a Parameters value,
            //   so it's safe to unconditionally replace empty-string values with
            //   an empty string[]. Mirrors system-agent's local ParameterNormalizer.
            foreach (var key in sensor.Parameters.Keys.ToList())
            {
                if (sensor.Parameters[key] is string s && s.Length == 0)
                {
                    sensor.Parameters[key] = Array.Empty<string>();
                }
            }
        }
    }
}
