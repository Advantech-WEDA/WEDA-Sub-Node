using System.Text.Json;

using Microsoft.Extensions.Configuration;

using Weda.SubNode.Abstractions.Telemetry;

namespace SystemAgentExample.Devices;

/// <summary>
/// Normalizes sensor Parameters values from various source formats
/// (IConfiguration binding, JsonElement, raw strings) into clean typed values
/// that SensorResolver can consume without knowing the data source.
///
/// IConfiguration binding behavior for Dictionary&lt;string, object&gt;:
/// - Scalar values ("MetricType": "cpu") → string
/// - Empty arrays ("Interfaces": []) → empty string ""
/// - Populated arrays ("Interfaces": ["eth0"]) → System.Object (content lost)
/// - Comma-separated strings ("Interfaces": "eth0,eth1") → string
///
/// By accepting an IConfigurationSection for the Sensors array, this normalizer
/// can recover array values that were lost during Dictionary binding by reading
/// the raw section children (e.g., "Sensors:0:Parameters:Interfaces:0" = "eth0").
///
/// After normalization, list parameters (Interfaces, PinIds, Sources) are always string[].
/// </summary>
internal static class ParameterNormalizer
{
    private static readonly string[] ListParameterKeys = ["Interfaces", "PinIds", "Sources"];

    /// <summary>
    /// Normalizes all sensors' Parameters in-place.
    /// Uses the raw IConfigurationSection to recover array values lost during binding.
    /// </summary>
    /// <param name="sensors">The bound sensor list to normalize in-place.</param>
    /// <param name="sensorsSection">
    /// The raw IConfigurationSection for the Sensors array (e.g., "DeviceConfig:DeviceConfigs:MyDevice:Sensors").
    /// When provided, array values that were lost during Dictionary binding are recovered from section children.
    /// </param>
    internal static void Normalize(IEnumerable<Sensor> sensors, IConfigurationSection? sensorsSection = null)
    {
        var sensorList = sensors as IList<Sensor> ?? sensors.ToList();

        for (int i = 0; i < sensorList.Count; i++)
        {
            var sensor = sensorList[i];
            if (sensor.Parameters == null)
                continue;

            // Get the raw section for this sensor's Parameters if available
            var parametersSection = sensorsSection?.GetSection($"{i}:Parameters");

            foreach (var key in ListParameterKeys)
            {
                if (!sensor.Parameters.TryGetValue(key, out var value) || value == null)
                {
                    // Key not in bound dictionary — try recovering from raw section
                    // (IConfiguration may have dropped it entirely for populated arrays)
                    if (parametersSection != null)
                    {
                        var recovered = RecoverFromSection(parametersSection, key);
                        if (recovered != null)
                            sensor.Parameters[key] = recovered;
                    }
                    continue;
                }

                var normalized = NormalizeToStringArray(value, parametersSection, key);
                if (normalized != null)
                    sensor.Parameters[key] = normalized;
                else
                {
                    // Value is unrecoverable from binding (System.Object) — try section recovery
                    var recovered = RecoverFromSection(parametersSection, key);
                    if (recovered != null)
                        sensor.Parameters[key] = recovered;
                    else
                        sensor.Parameters.Remove(key); // Truly unrecoverable — remove so SensorResolver sees "not configured"
                }
            }
        }
    }

    /// <summary>
    /// Converts a parameter value to string[] from any supported format.
    /// Returns null if the value cannot be meaningfully converted.
    /// </summary>
    private static string[]? NormalizeToStringArray(object value, IConfigurationSection? parametersSection, string key)
    {
        return value switch
        {
            string[] arr => arr,
            IEnumerable<string> enumerable => enumerable.ToArray(),
            JsonElement element => NormalizeJsonElement(element),
            string str => ParseCommaSeparated(str),
            _ => RecoverFromSection(parametersSection, key) // System.Object — try section recovery
        };
    }

    /// <summary>
    /// Recovers an array value from the raw IConfigurationSection.
    /// IConfiguration stores JSON arrays as indexed children: "Interfaces:0" = "eth0", "Interfaces:1" = "eth1".
    /// </summary>
    private static string[]? RecoverFromSection(IConfigurationSection? parametersSection, string key)
    {
        if (parametersSection == null)
            return null;

        var keySection = parametersSection.GetSection(key);
        if (!keySection.Exists())
            return null;

        var children = keySection.GetChildren().ToArray();
        if (children.Length == 0)
        {
            // Section exists but no children — could be a scalar value or empty array
            var scalarValue = keySection.Value;
            return ParseCommaSeparated(scalarValue);
        }

        // Has indexed children — this is an array
        var values = children
            .Where(c => c.Value != null)
            .Select(c => c.Value!)
            .ToArray();

        return values.Length > 0 ? values : [];
    }

    private static string[]? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Array => element.Deserialize<string[]>() ?? [],
            JsonValueKind.String => ParseCommaSeparated(element.GetString()),
            _ => null
        };
    }

    private static string[]? ParseCommaSeparated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
