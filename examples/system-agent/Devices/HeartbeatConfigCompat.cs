using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Configuration;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Devices;

/// <summary>
/// Completes the reserved heartbeat sensor where a deployed devicecfg.json leaves it out or
/// leaves it incomplete.
/// </summary>
/// <remarks>
/// <para>Installed devicecfg.json files cannot be changed, so system-agent covers two gaps:</para>
/// <list type="bullet">
/// <item><b>No <c>hb</c> sensor.</b> The SDK only beats when the sensor is declared, so the
/// SubNode would publish no liveness signal and the platform would read it as Disconnected.
/// The default heartbeat is added instead: enabled, every
/// <see cref="Heartbeat.DefaultIntervalMilliseconds"/> ms, not recorded.</item>
/// <item><b><c>hb</c> without <c>SensorInfo.Schema</c></b>, as the setup wizard template ships
/// it. The SDK stamps the schema onto the in-memory sensor, but a cloud config reset
/// re-validates devicecfg.json as written and is refused with "Sensor 'hb': Schema is
/// required", and the reported devicecfg echoes the raw file, which the cloud rejects with
/// "sensorInfo.schema is missing". The schema is filled with <see cref="Heartbeat.Schema"/>,
/// the value the SDK stamps anyway.</item>
/// </list>
/// <para>
/// A declared heartbeat is otherwise left as written, including one disabled on purpose, and
/// so is every other sensor.
/// </para>
/// </remarks>
internal static class HeartbeatConfigCompat
{
    private const string DefaultSensorGroup = "SYS";
    private const string DefaultDisplayName = "Heartbeat";
    private const string DefaultDescription =
        "Reserved platform liveness ping, added by system-agent because devicecfg.json declares none. "
        + "It is never polled; its value is synthesized by the SDK.";

    private static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Returns configuration entries declaring the default heartbeat for a device whose
    /// devicecfg.json declares none.
    /// </summary>
    /// <param name="configuration">Host configuration, with devicecfg.json loaded under
    /// <c>DeviceConfig</c>.</param>
    /// <param name="deviceConfigKey">The device's key under <c>DeviceConfigs</c>.</param>
    /// <returns>
    /// Entries to add as an in-memory configuration source, appending the heartbeat after the
    /// declared sensors; <c>null</c> when the device declares a heartbeat or does not exist.
    /// </returns>
    /// <remarks>
    /// Added before the host binds the device, the heartbeat is handled exactly like a declared
    /// one: the SDK stamps its reserved contract (dtmi, schema, empty <c>Parameters</c>),
    /// generates it into the DTDL, uploads it with the capabilities and starts beating.
    /// </remarks>
    internal static IReadOnlyDictionary<string, string?>? DefaultSensorSettings(
        IConfiguration configuration, string deviceConfigKey)
    {
        var device = configuration.GetSection($"DeviceConfig:DeviceConfigs:{deviceConfigKey}");
        if (!device.Exists())
            return null;

        var sensors = device.GetSection("Sensors").GetChildren().ToList();
        if (sensors.Any(sensor => IsHeartbeat(sensor["Name"], sensor["Dtmi"])))
            return null;

        // Configuration arrays are keyed by index; append after the highest one in use.
        var next = sensors
            .Select(sensor => int.TryParse(sensor.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? index + 1 : 0)
            .DefaultIfEmpty(0)
            .Max();
        var prefix = $"{device.Path}:Sensors:{next}:";

        return new Dictionary<string, string?>
        {
            [prefix + "Name"] = Heartbeat.SensorName,
            [prefix + "SensorGroup"] = DefaultSensorGroup,
            [prefix + "Report:Enabled"] = "true",
            [prefix + "Report:Interval"] = Heartbeat.DefaultIntervalMilliseconds.ToString(CultureInfo.InvariantCulture),
            [prefix + "Record:Enabled"] = "false",
            [prefix + "SensorInfo:Schema"] = Heartbeat.Schema,
            [prefix + "SensorInfo:DisplayName"] = DefaultDisplayName,
            [prefix + "SensorInfo:Description"] = DefaultDescription,
        };
    }

    /// <summary>
    /// Completes the heartbeat in a desired configuration message, in both the typed device
    /// configs that validation and apply read and the raw JSON that the report echoes.
    /// </summary>
    /// <param name="message">The configuration update message. Updated in place, because
    /// the SDK validates, applies and echoes this same instance.</param>
    /// <param name="deviceName">The system-agent device, the one that carries the heartbeat.</param>
    /// <returns><c>true</c> when anything was added or filled in.</returns>
    /// <remarks>
    /// A sensor list replaces the device's sensors, so a list without a heartbeat (a reset to
    /// a devicecfg.json that declares none, for one) would remove the running heartbeat; the
    /// default is added to it. A message without a sensor list keeps the current sensors and is
    /// left alone. Idempotent: a second call finds nothing to do.
    /// </remarks>
    internal static bool ApplyDefaults(SubNodeConfigUpdateMessage? message, string deviceName)
    {
        var desired = message?.Data?.Cfg?.Desired;
        if (desired is null)
            return false;

        var applied = false;
        var deviceConfigs = desired.DeviceCfg?.DeviceConfigs;

        if (deviceConfigs != null
            && deviceConfigs.TryGetValue(deviceName, out var device)
            && device?.Sensors is { } ownSensors
            && !ownSensors.Any(sensor => IsHeartbeat(sensor.Name, sensor.Dtmi)))
        {
            ownSensors.Add(DefaultSensorJson(pascalCase: false).Deserialize<SubNodeSensorReportDto>(CaseInsensitive)!);
            applied = true;
        }

        foreach (var sensor in deviceConfigs?.Values.SelectMany(config => config?.Sensors ?? []) ?? [])
        {
            if (!IsHeartbeat(sensor.Name, sensor.Dtmi) || HasText(sensor.SensorInfo?.Schema))
                continue;

            sensor.SensorInfo ??= new SubNodeSensorInfoDto();
            sensor.SensorInfo.Schema = Heartbeat.Schema;
            applied = true;
        }

        if (desired.RawDeviceCfg is { } raw && TryApplyDefaults(raw, deviceName, out var patched))
        {
            desired.RawDeviceCfg = patched;
            applied = true;
        }

        return applied;
    }

    /// <summary>
    /// Returns raw devicecfg JSON with the heartbeat completed: the default one appended to
    /// <paramref name="deviceName"/>'s sensors when it declares none, and a missing schema
    /// filled in on any declared one.
    /// </summary>
    /// <param name="rawDeviceCfg">Raw devicecfg JSON, PascalCase (devicecfg.json) or camelCase
    /// (cloud wire). Not modified.</param>
    /// <param name="deviceName">The system-agent device, the one that carries the heartbeat.</param>
    /// <param name="patched">The completed copy, or <paramref name="rawDeviceCfg"/> when
    /// nothing was missing.</param>
    /// <returns><c>true</c> when anything was added or filled in.</returns>
    internal static bool TryApplyDefaults(JsonElement rawDeviceCfg, string deviceName, out JsonElement patched)
    {
        patched = rawDeviceCfg;

        // A JSON null devicecfg is the cloud's reset marker; anything but an object is left alone.
        if (rawDeviceCfg.ValueKind != JsonValueKind.Object
            || JsonNode.Parse(rawDeviceCfg.GetRawText()) is not JsonObject root)
        {
            return false;
        }

        var applied = false;
        foreach (var (key, device) in Child(root, "DeviceConfigs") as JsonObject ?? [])
        {
            if (device is not JsonObject deviceObject || Child(deviceObject, "Sensors") is not JsonArray sensors)
                continue;

            var heartbeats = sensors.OfType<JsonObject>()
                .Where(sensor => IsHeartbeat(Text(sensor, "Name"), Text(sensor, "Dtmi")))
                .ToList();

            foreach (var heartbeat in heartbeats)
                applied |= FillSchema(heartbeat);

            if (heartbeats.Count == 0 && string.Equals(key, deviceName, StringComparison.OrdinalIgnoreCase))
            {
                // New keys follow the document's casing: PascalCase in devicecfg.json, camelCase on the wire.
                sensors.Add(DefaultSensorJson(pascalCase: deviceObject.ContainsKey("Sensors")));
                applied = true;
            }
        }

        if (applied)
            patched = JsonSerializer.SerializeToElement(root);

        return applied;
    }

    private static bool FillSchema(JsonObject heartbeat)
    {
        var pascalCase = heartbeat.ContainsKey("Name");

        if (Child(heartbeat, "SensorInfo") is not JsonObject sensorInfo)
        {
            sensorInfo = new JsonObject();
            heartbeat[KeyOf(heartbeat, "SensorInfo", pascalCase)] = sensorInfo;
        }

        if (HasText(Text(sensorInfo, "Schema")))
            return false;

        sensorInfo[KeyOf(sensorInfo, "Schema", pascalCase)] = Heartbeat.Schema;
        return true;
    }

    /// <summary>
    /// The default heartbeat as devicecfg JSON. Single source for the raw report and the
    /// desired-configuration DTO; <see cref="DefaultSensorSettings"/> mirrors it.
    /// </summary>
    private static JsonObject DefaultSensorJson(bool pascalCase)
    {
        string Key(string name) => pascalCase ? name : CamelCase(name);

        return new JsonObject
        {
            [Key("Name")] = Heartbeat.SensorName,
            [Key("SensorGroup")] = DefaultSensorGroup,
            [Key("Parameters")] = new JsonObject(),
            [Key("Report")] = new JsonObject
            {
                [Key("Enabled")] = true,
                [Key("Interval")] = Heartbeat.DefaultIntervalMilliseconds
            },
            [Key("Record")] = new JsonObject { [Key("Enabled")] = false },
            [Key("SensorInfo")] = new JsonObject
            {
                [Key("Schema")] = Heartbeat.Schema,
                [Key("DisplayName")] = DefaultDisplayName,
                [Key("Description")] = DefaultDescription
            }
        };
    }

    /// <summary>Mirrors <see cref="Heartbeat.IsHeartbeat"/> for configuration that is not yet a <see cref="Sensor"/>.</summary>
    private static bool IsHeartbeat(string? name, string? dtmi) =>
        string.Equals(name, Heartbeat.SensorName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(dtmi, Heartbeat.Dtmi, StringComparison.OrdinalIgnoreCase);

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static JsonNode? Child(JsonNode? node, string name) =>
        node is JsonObject obj
            ? obj.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value
            : null;

    private static string? Text(JsonNode? node, string name) =>
        Child(node, name) is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>The existing key for <paramref name="name"/> in any casing, so it is replaced rather than duplicated.</summary>
    private static string KeyOf(JsonObject obj, string name, bool pascalCase) =>
        obj.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Key
        ?? (pascalCase ? name : CamelCase(name));
}
