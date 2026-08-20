using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace opcua_device.Sensors;

public static class OpcUaDevice
{
    public const string DeviceTypeName = "opc-ua";
}

// Empty transport / protocol POCOs — sufficient for catalog emission. Real
// OPC-UA endpoint / security mode bind via the runtime DeviceCommunication
// dictionary today; will move into typed POCOs in a follow-up tightening.
public class OpcUaCommunication { }
public class OpcUaProperties { }

public class OpcUaConfiguration
    : IConfigurableDevice<OpcUaCommunication, OpcUaProperties>
{
    public static string DeviceTypeName => OpcUaDevice.DeviceTypeName;
    public static string? Description   => "OPC-UA client device (request-response polling or subscription).";
}

public class OpcUaNodeParameters
{
    [Required, JsonPropertyName("nodeId")]
    public string NodeId { get; init; } = string.Empty;

    [Required, JsonPropertyName("namespaceIndex")]
    public int NamespaceIndex { get; init; }

    [Required, JsonPropertyName("dataType")]
    public string DataType { get; init; } = string.Empty;
}

public class OpcUaNodeSensor : IConfigurableSensor<OpcUaNodeParameters>
{
    public static string DeviceTypeName => OpcUaDevice.DeviceTypeName;
    public static string SensorTypeName => "opc-ua-node";
    public static string? Description   => "Single OPC-UA node read by NodeId + NamespaceIndex.";
}
