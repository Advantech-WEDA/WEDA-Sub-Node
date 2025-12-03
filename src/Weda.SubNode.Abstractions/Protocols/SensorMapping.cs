namespace Weda.SubNode.Abstractions.Protocols;

/// <summary>
/// Sensor mapping for converting protocol field names to ResourceIds
/// </summary>
public class SensorMapping
{
    /// <summary>
    /// Maps protocol field names to ResourceIds (e.g., "ai1" -> "AnalogInput1")
    /// </summary>
    public Dictionary<string, string> FieldToResourceId { get; set; } = new();

    /// <summary>
    /// Maps protocol field names to sensor types (for type-specific parsing)
    /// </summary>
    public Dictionary<string, SensorType> FieldToSensorType { get; set; } = new();

    /// <summary>
    /// Reverse mapping: ResourceId to protocol field name (for encoding)
    /// </summary>
    public Dictionary<string, string> ResourceIdToField
    {
        get
        {
            return FieldToResourceId.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        }
    }
}
