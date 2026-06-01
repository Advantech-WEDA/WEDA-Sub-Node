using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace ImageSensor.Sensors;

public static class MqttImageDevice
{
    public const string DeviceTypeName = "mqtt-image";
}

public class MqttImageCommunication { }
public class MqttImageProperties { }

public class MqttImageConfiguration
    : IConfigurableDevice<MqttImageCommunication, MqttImageProperties>
{
    public static string DeviceTypeName => MqttImageDevice.DeviceTypeName;
    public static string? Description   => "MQTT image subscriber — receives binary image payloads on a topic and surfaces them as MIME-typed telemetry.";
}

public class MqttImageTopicParameters
{
    [Required, JsonPropertyName("topic")]
    public string Topic { get; init; } = string.Empty;
}

public class MqttImageTopicSensor : IConfigurableSensor<MqttImageTopicParameters>
{
    public static string DeviceTypeName => MqttImageDevice.DeviceTypeName;
    public static string SensorTypeName => "mqtt-image-topic";
    public static string? Description   => "One MQTT topic carrying image frames (e.g. MNIST PNG, JPEG, etc.).";
}
