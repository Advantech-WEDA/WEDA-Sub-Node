using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Wise4012ISensingExample.Sensors;

public static class MqttISensingDevice
{
    public const string DeviceTypeName = "mqtt-isensing";
}

public class MqttISensingCommunication { }
public class MqttISensingProperties { }

public class MqttISensingConfiguration
    : IConfigurableDevice<MqttISensingCommunication, MqttISensingProperties>
{
    public static string DeviceTypeName => MqttISensingDevice.DeviceTypeName;
    public static string? Description   => "MQTT iSensing transport for Advantech WISE-4012-style devices.";
}

public class ISensingChannelParameters
{
    /// <summary>
    /// Channel identifier in the iSensing payload (e.g. "ai1", "do2").
    /// The sensor's <c>SensorGroup</c> and <c>SensorInfo.Schema</c> tell the
    /// runtime whether to interpret the channel as analog or digital.
    /// </summary>
    [Required, JsonPropertyName("fieldName")]
    public string FieldName { get; init; } = string.Empty;
}

public class ISensingChannelSensor : IConfigurableSensor<ISensingChannelParameters>
{
    public static string DeviceTypeName => MqttISensingDevice.DeviceTypeName;
    public static string SensorTypeName => "isensing-channel";
    public static string? Description   => "One channel (analog or digital) of an iSensing-protocol device addressed by its FieldName.";
}
