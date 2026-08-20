using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

using VisionObjectDetection.Devices;

namespace VisionObjectDetection.Sensors;

/// <summary>
/// Strongly-typed capability catalog for the <c>mqtt-vision</c> device kind.
/// Discovered by the host's assembly scan at startup and emitted as DTDL v3
/// Interfaces, so the vision device + sensor shapes resolve via typed dispatch
/// (instead of falling back to autogen). Mirrors the image-sensor TypedCatalog.
/// </summary>
public static class MqttVisionDevice
{
    public const string DeviceTypeName = VisionDetectionDevice.DeviceTypeName;
}

/// <summary>Transport-layer settings surfaced into the device DTDL Interface.</summary>
public sealed class VisionCommunication
{
    [JsonPropertyName("brokerUrl")] public string BrokerUrl { get; init; } = "mqtt://localhost:1883";
    [JsonPropertyName("clientId")] public string ClientId { get; init; } = string.Empty;

    /// <summary>CV container device id to ingest; <c>"+"</c> = any device on the broker.</summary>
    [JsonPropertyName("deviceId")] public string DeviceId { get; init; } = "+";
}

/// <summary>The vision device exposes no protocol-specific properties.</summary>
public sealed class VisionProperties
{
}

/// <summary>Device-kind descriptor for the YOLO object-detection MQTT integration.</summary>
public sealed class VisionDeviceConfiguration
    : IConfigurableDevice<VisionCommunication, VisionProperties>
{
    public static string DeviceTypeName => MqttVisionDevice.DeviceTypeName;
    public static string? Description =>
        "MQTT subscriber for the Advantech YOLO object-detection container — maps the 1 Hz detection stream into scalar telemetry.";
}

/// <summary>Per-sensor parameter shape: which detection metric this sensor reports.</summary>
public sealed class VisionFieldParameters
{
    /// <summary>
    /// Field selector, e.g. <c>objectCount</c>, <c>fps</c>, <c>confidence.max</c>,
    /// <c>confidence.avg</c>, or <c>classCount.&lt;name&gt;</c>.
    /// </summary>
    [Required, JsonPropertyName("Field")]
    public string Field { get; init; } = string.Empty;
}

/// <summary>Sensor-kind descriptor for a single vision detection metric.</summary>
public sealed class VisionFieldSensor : IConfigurableSensor<VisionFieldParameters>
{
    public static string DeviceTypeName => MqttVisionDevice.DeviceTypeName;
    public static string SensorTypeName => "vision-field";
    public static string? Description =>
        "One scalar metric extracted from a YOLO detections frame (count, fps, confidence, or per-class count).";
}
