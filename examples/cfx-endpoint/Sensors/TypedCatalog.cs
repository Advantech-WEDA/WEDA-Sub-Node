using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace CfxEndpointExample.Sensors;

/// <summary>
/// Device-type identifier shared by the CFX device and its sensor shape.
/// </summary>
public static class MqttCfxDevice
{
    /// <summary>Stable device kind identifier, matched case-insensitively cloud-side.</summary>
    public const string DeviceTypeName = "mqtt-cfx";
}

/// <summary>
/// Broker and subscription settings for a bridged CFX endpoint.
/// </summary>
public class MqttCfxCommunication
{
    /// <summary>MQTT broker host of the CFX bridge.</summary>
    [JsonPropertyName("brokerHost")]
    public string BrokerHost { get; init; } = "localhost";

    /// <summary>MQTT broker port.</summary>
    [JsonPropertyName("brokerPort")]
    public int BrokerPort { get; init; } = 1883;

    /// <summary>MQTT client identifier.</summary>
    [JsonPropertyName("clientId")]
    public string? ClientId { get; init; }

    /// <summary>
    /// CFX handle of the endpoint to subscribe to, conventionally
    /// <c>{Vendor}.{Model}.{Serial}</c>. Omit to capture every endpoint on the broker.
    /// </summary>
    [JsonPropertyName("cfxHandle")]
    public string? CfxHandle { get; init; }

    /// <summary>Topic root segment separating the handle from the CFX message path.</summary>
    [JsonPropertyName("topicRoot")]
    public string TopicRoot { get; init; } = "CFX";

    /// <summary>
    /// Handle segment count matched when <see cref="CfxHandle"/> is omitted.
    /// </summary>
    [JsonPropertyName("handleSegments")]
    public int HandleSegments { get; init; } = 3;
}

/// <summary>
/// Device-level properties. CFX endpoints are configured entirely through their subscription, so
/// there is nothing further to declare here.
/// </summary>
public class MqttCfxProperties
{
}

/// <summary>
/// Capability descriptor for the CFX endpoint device kind.
/// </summary>
public class MqttCfxConfiguration
    : IConfigurableDevice<MqttCfxCommunication, MqttCfxProperties>
{
    /// <inheritdoc />
    public static string DeviceTypeName => MqttCfxDevice.DeviceTypeName;

    /// <inheritdoc />
    public static string? Description =>
        "IPC-CFX endpoint captured over an AMQP-to-MQTT bridge (subscribe-only).";
}

/// <summary>
/// Per-sensor parameters: the CFX message a sensor reports.
/// </summary>
public class CfxMessageParameters
{
    /// <summary>
    /// Fully-qualified CFX message name, for example
    /// <c>CFX.ResourcePerformance.StationStateChanged</c>.
    /// </summary>
    /// <remarks>
    /// This is the whole binding. Unlike register-based protocols there is no address or width to
    /// configure: the reported value is the message's body, carried as raw JSON.
    /// </remarks>
    [Required, JsonPropertyName("messageName")]
    public string MessageName { get; init; } = string.Empty;
}

/// <summary>
/// Capability descriptor for a sensor bound to one CFX message type.
/// </summary>
public class CfxMessageSensor : IConfigurableSensor<CfxMessageParameters>
{
    /// <inheritdoc />
    public static string DeviceTypeName => MqttCfxDevice.DeviceTypeName;

    /// <inheritdoc />
    public static string SensorTypeName => "cfx-message";

    /// <inheritdoc />
    public static string? Description =>
        "One CFX message type; the reported value is the message body as raw JSON.";
}
