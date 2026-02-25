namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// DTDL-related information for a sensor.
/// Used for auto-generating DTDL content when AutoGenEnabled is true.
/// </summary>
public class SensorInfo
{
    /// <summary>
    /// DTDL schema type for this sensor's telemetry value.
    /// Must be DTDL primitives or MIME type.
    /// Examples: "double", "integer", "boolean", "string", "image/jpeg", "application/json"
    /// Note: Validated at configuration time by SensorsValidator. Defaults to "double".
    /// </summary>
    public string Schema { get; set; } = "double";

    /// <summary>
    /// Human-readable display name for DTDL generation.
    /// If not specified, derived from sensor Name (e.g., "temperature.sensor" → "Temperature Sensor")
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Description for DTDL generation.
    /// Optional field for documentation purposes.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets the effective display name for DTDL generation.
    /// Returns DisplayName if specified, otherwise derives from the provided sensor name.
    /// </summary>
    /// <param name="sensorName">The sensor name to derive DisplayName from when not specified.</param>
    public string GetEffectiveDisplayName(string sensorName)
    {
        if (!string.IsNullOrEmpty(DisplayName))
            return DisplayName;

        // Convert "temperature.sensor" or "temperature_sensor" to "Temperature Sensor"
        return string.Join(" ",
            sensorName.Replace('.', ' ')
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpper(word[0]) + word[1..].ToLower()));
    }
}
