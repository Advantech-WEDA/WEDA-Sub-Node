using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Protocols.OpcUa;

/// <summary>
/// OPC-UA specific configuration extensions for DeviceConfiguration.
/// </summary>
public static class OpcUaDeviceConfigurationExtensions
{
    /// <summary>
    /// Get OPC-UA node mapping from Sensor
    /// </summary>
    public static OpcUaNodeMapping ToOpcUaNodeMapping(this Sensor sensor)
    {
        var parameters = sensor.Parameters ?? new Dictionary<string, object>();

        return new OpcUaNodeMapping
        {
            ResourceId = sensor.ResourceId,
            Name = sensor.Name,
            Dtmi = sensor.Dtmi ?? string.Empty,
            SensorGroup = sensor.SensorGroup,
            NodeId = parameters.GetValueOrDefault("NodeId")?.ToString() ?? string.Empty,
            NamespaceIndex = Convert.ToUInt16(parameters.GetValueOrDefault("NamespaceIndex", 2)),
            DataType = Enum.Parse<OpcUaDataType>(
                parameters.GetValueOrDefault("DataType")?.ToString() ?? "Double"),
            Scale = Convert.ToDouble(parameters.GetValueOrDefault("Scale", 1.0)),
            Offset = Convert.ToDouble(parameters.GetValueOrDefault("Offset", 0.0)),
            Metadata = sensor.Metadata
        };
    }
}

/// <summary>
/// OPC-UA sensor node mapping (internal representation).
/// Maps a sensor configuration to an OPC-UA node for reading/writing.
/// </summary>
public class OpcUaNodeMapping
{
    public string? ResourceId { get; set; }
    public required string Name { get; set; }
    public required string Dtmi { get; set; }
    public SensorGroup? SensorGroup { get; set; }

    /// <summary>
    /// OPC-UA Node identifier string (e.g., "Temperature", "ns=2;s=Temperature")
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// OPC-UA namespace index (default: 2, the application namespace)
    /// </summary>
    public ushort NamespaceIndex { get; set; } = 2;

    /// <summary>
    /// Expected data type of the node value
    /// </summary>
    public OpcUaDataType DataType { get; set; } = OpcUaDataType.Double;

    /// <summary>
    /// Scale factor applied after reading: result = raw * Scale + Offset
    /// </summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// Offset applied after reading: result = raw * Scale + Offset
    /// </summary>
    public double Offset { get; set; } = 0.0;

    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Convert to OPC Foundation NodeId
    /// </summary>
    public Opc.Ua.NodeId ToNodeId()
    {
        // If NodeId already contains namespace prefix (e.g., "ns=2;s=Temperature"), parse it directly
        if (NodeId.StartsWith("ns=", StringComparison.OrdinalIgnoreCase) ||
            NodeId.StartsWith("i=", StringComparison.OrdinalIgnoreCase) ||
            NodeId.StartsWith("s=", StringComparison.OrdinalIgnoreCase) ||
            NodeId.StartsWith("g=", StringComparison.OrdinalIgnoreCase) ||
            NodeId.StartsWith("b=", StringComparison.OrdinalIgnoreCase))
        {
            return Opc.Ua.NodeId.Parse(NodeId);
        }

        // Otherwise, treat as string identifier in the specified namespace
        return new Opc.Ua.NodeId(NodeId, NamespaceIndex);
    }
}

/// <summary>
/// OPC-UA data types supported for node value reading/writing.
/// Maps to OPC-UA built-in types (Part 6, Table A.1).
/// </summary>
public enum OpcUaDataType
{
    Boolean,
    SByte,
    Byte,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Float,
    Double,
    String,
    DateTime,
    ByteString
}
