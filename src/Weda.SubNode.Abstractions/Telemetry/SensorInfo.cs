namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// DTDL-related information for a sensor.
/// Used for auto-generating DTDL content when AutoGenDtdl is true.
/// </summary>
public class SensorInfo
{
    /// <summary>
    /// DTDL schema type for this sensor's telemetry value.
    /// Examples: "double", "integer", "boolean", "string", "float"
    /// If not specified, inferred from SensorGroup:
    /// - AI, AO, TEMP, PWR, SYS → "double"
    /// - DI, DO → "boolean"
    /// </summary>
    public string? Schema { get; set; }

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
    /// Gets the effective schema type for DTDL generation.
    /// Returns Schema if specified, otherwise infers from the provided SensorGroup.
    /// </summary>
    /// <param name="sensorGroup">The sensor group to use for inference when Schema is not specified.</param>
    public string GetEffectiveSchema(SensorGroup sensorGroup)
    {
        if (!string.IsNullOrEmpty(Schema))
            return Schema;

        return sensorGroup switch
        {
            SensorGroup.DI => "boolean",
            SensorGroup.DO => "boolean",
            _ => "double"  // AI, AO, TEMP, PWR, SYS default to double
        };
    }

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
