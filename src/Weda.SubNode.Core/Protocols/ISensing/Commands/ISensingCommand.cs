using System.Text.Json.Serialization;

namespace Weda.SubNode.Core.Protocols.ISensing.Commands;

/// <summary>
/// Base class for ISensing protocol commands
/// </summary>
public abstract class ISensingCommand
{
    /// <summary>
    /// Command type identifier
    /// </summary>
    [JsonPropertyName("cmd")]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Additional command parameters
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalData { get; set; }
}

/// <summary>
/// Digital output control command
/// Sets the state of a digital output channel
/// </summary>
public class DigitalOutputCommand : ISensingCommand
{
    /// <summary>
    /// Digital output channel name (e.g., "do0", "do1")
    /// </summary>
    [JsonPropertyName("do")]
    public required string OutputName { get; set; }

    /// <summary>
    /// Output state (true = ON, false = OFF)
    /// </summary>
    [JsonPropertyName("state")]
    public required bool State { get; set; }

    public DigitalOutputCommand()
    {
        Command = "SetDO";
    }
}

/// <summary>
/// Analog output control command
/// Sets the value of an analog output channel
/// </summary>
public class AnalogOutputCommand : ISensingCommand
{
    /// <summary>
    /// Analog output channel name (e.g., "ao0", "ao1")
    /// </summary>
    [JsonPropertyName("ao")]
    public required string OutputName { get; set; }

    /// <summary>
    /// Output value
    /// </summary>
    [JsonPropertyName("value")]
    public required double Value { get; set; }

    public AnalogOutputCommand()
    {
        Command = "SetAO";
    }
}

/// <summary>
/// Configuration request command
/// Requests current device configuration
/// </summary>
public class ConfigurationRequestCommand : ISensingCommand
{
    /// <summary>
    /// Configuration index (optional, 0 = all configurations)
    /// </summary>
    [JsonPropertyName("index")]
    public ushort Index { get; set; } = 0;

    public ConfigurationRequestCommand()
    {
        Command = "GetConfig";
    }
}

/// <summary>
/// Configuration update command
/// Updates device configuration
/// </summary>
public class ConfigurationUpdateCommand : ISensingCommand
{
    /// <summary>
    /// Configuration index
    /// </summary>
    [JsonPropertyName("index")]
    public required ushort Index { get; set; }

    /// <summary>
    /// Configuration data
    /// </summary>
    [JsonPropertyName("config")]
    public required Dictionary<string, object> ConfigData { get; set; }

    public ConfigurationUpdateCommand()
    {
        Command = "SetConfig";
    }
}

/// <summary>
/// Sensor enable/disable command
/// Enables or disables a specific sensor
/// </summary>
public class SensorEnableCommand : ISensingCommand
{
    /// <summary>
    /// Sensor name (e.g., "ai0", "di1")
    /// </summary>
    [JsonPropertyName("sensor")]
    public required string SensorName { get; set; }

    /// <summary>
    /// Enable state (true = enabled, false = disabled)
    /// </summary>
    [JsonPropertyName("enabled")]
    public required bool Enabled { get; set; }

    public SensorEnableCommand()
    {
        Command = "SetSensorEnable";
    }
}
