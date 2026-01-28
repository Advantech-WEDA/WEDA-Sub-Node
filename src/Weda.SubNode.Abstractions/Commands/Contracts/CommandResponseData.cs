using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Commands.Contracts;


/// <summary>
/// Base data payload for command responses.
/// </summary>
public class CommandResponseData
{
    /// <summary>
    /// The device command name.
    /// </summary>
    [JsonPropertyName("deviceCmd")]
    public string DeviceCmd { get; set; } = string.Empty;

    /// <summary>
    /// The type of response message. (ack, progress or result for example)
    /// </summary>
    [JsonPropertyName("messageType")]
    public required string MessageType { get; set; } = "result";

    /// <summary>
    /// Status code.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int Status { get; set; }

    /// <summary>
    /// Error message (only for rejected/failed responses).
    /// </summary>
    [JsonPropertyName("errorMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    /// <summary>
    /// The result data of specific context.
    /// </summary>
    [JsonPropertyName("resultData")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ResultData { get; set; }

    /// <summary>
    /// The time the command was executed.
    /// </summary>
    [JsonPropertyName("executedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ExecutedAt { get; set; }

    /// <summary>
    /// The time the command was completed.
    /// </summary>
    [JsonPropertyName("completedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? CompletedAt { get; set; }
}