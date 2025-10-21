using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Devices;

public class DeviceInfo
{
    /// <summary>
    /// Device name (e.g., "adam4612", "temp-sensor-1")
    /// </summary>
    [JsonPropertyName("deviceName")]
    public required string DeviceName { get; set; }

    /// <summary>
    /// Device type (e.g., "adamEthernet", "modbusRTU")
    /// </summary>
    [JsonPropertyName("deviceType")]
    public required DeviceType DeviceType { get; set; }

    /// <summary>
    /// Device manufacturer (e.g., "Advantech")
    /// </summary>
    [JsonPropertyName("manufacturer")]
    public required string Manufacturer { get; set; }

    /// <summary>
    /// Device model (e.g., "ADAM-4612")
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; set; }
}