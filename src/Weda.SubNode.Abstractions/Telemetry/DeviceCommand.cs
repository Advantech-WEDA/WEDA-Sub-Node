namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Device command
/// </summary>
public class DeviceCommand
{
    /// <summary>
    /// Device command name (e.g., "Start", "Pause", "Stop")
    /// </summary>
    public string DeviceCmd { get; set; } = string.Empty;

    /// <summary>
    /// Command timeout in milliseconds
    /// </summary>
    public uint Timeout { get; set; }

    /// <summary>
    /// Response topic for command result
    /// </summary>
    public string RespTopic { get; set; } = string.Empty;

    /// <summary>
    /// Command parameters (protocol-specific)
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = [];
}