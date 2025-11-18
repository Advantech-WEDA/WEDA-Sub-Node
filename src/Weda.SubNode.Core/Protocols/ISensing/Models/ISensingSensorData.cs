using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Core.Protocols.ISensing.Models;

/// <summary>
/// ISensing sensor data model (standard fields)
/// </summary>
public class ISensingSensorData
{
    /// <summary>
    /// Sequence number (monotonically increasing)
    /// </summary>
    [JsonPropertyName("s")]
    public uint SequenceNumber { get; set; }

    /// <summary>
    /// Timestamp (Unix epoch or ISO 8601)
    /// </summary>
    [JsonPropertyName("t")]
    public JsonElement Timestamp { get; set; } // Can be long or string

    /// <summary>
    /// Quality code (OPC-based: 192=Good, 0-28=Bad, 64-88=Uncertain, 255=NoQuality)
    /// </summary>
    [JsonPropertyName("q")]
    public byte QualityCode { get; set; }

    /// <summary>
    /// Configuration index (increments on config changes)
    /// </summary>
    [JsonPropertyName("c")]
    public ushort ConfigurationIndex { get; set; }

    /// <summary>
    /// Dynamic sensor properties (ai1, ai2, do1, temp1, etc.)
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

/// <summary>
/// Connection status message
/// </summary>
public class ConnectionStatusMessage
{
    /// <summary>
    /// Status: "connect" or "disconnect"
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    /// <summary>
    /// Device model name (e.g., "WISE-4012SE")
    /// </summary>
    [JsonPropertyName("name")]
    public required string DeviceName { get; set; }

    /// <summary>
    /// MAC address (unique device identifier)
    /// </summary>
    [JsonPropertyName("macid")]
    public required string MacAddress { get; set; }

    /// <summary>
    /// IP address (optional)
    /// </summary>
    [JsonPropertyName("ipaddr")]
    public string? IpAddress { get; set; }
}

/// <summary>
/// ISensing quality code utilities
/// </summary>
public static class ISensingQualityCode
{
    public const byte Good = 192;
    public const byte Bad = 0;
    public const byte Uncertain = 64;
    public const byte NoQuality = 255;

    /// <summary>
    /// Map quality code to string representation
    /// </summary>
    public static string MapToQuality(byte qualityCode)
    {
        return qualityCode switch
        {
            255 => "NoQuality",
            >= 192 => "Good",
            >= 64 and < 192 => "Uncertain",
            _ => "Bad"
        };
    }

    /// <summary>
    /// Check if quality code indicates good quality
    /// </summary>
    public static bool IsGoodQuality(byte qualityCode) => qualityCode >= 192;
}
