using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace CommandHandlerExample.Commands.GetDeviceProps.Models;

/// <summary>
/// Result of props.get: a DTDL Map keyed by device name
/// (string → <see cref="DevicePropsEntry"/>).
/// </summary>
public class GetDevicePropsResult : IResult
{
    public int Status { get; init; } = CommandStatusCode.Success;
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, DevicePropsEntry>? ResultData { get; init; }

    object? IResult.ResultData => ResultData;

    public static GetDevicePropsResult Success(Dictionary<string, DevicePropsEntry> data) =>
        new() { Message = "Device properties retrieved successfully", ResultData = data };

    public static GetDevicePropsResult Error(int status, string message) =>
        new() { Status = status, Message = message };
}

/// <summary>
/// Properties of one device (the map value type — must be concrete for DTDL).
/// </summary>
public record DevicePropsEntry
{
    [JsonPropertyName("deviceName")] public string DeviceName { get; init; } = string.Empty;
    [JsonPropertyName("subNodeType")] public string SubNodeType { get; init; } = string.Empty;
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("sensorCount")] public int SensorCount { get; init; }
    [JsonPropertyName("manufacturer")] public string? Manufacturer { get; init; }
    [JsonPropertyName("model")] public string? Model { get; init; }
    [JsonPropertyName("swVersion")] public string? SwVersion { get; init; }
    [JsonPropertyName("sensors")] public List<SensorPropsEntry>? Sensors { get; init; }
}

/// <summary>
/// Properties of one sensor within a device.
/// </summary>
public record SensorPropsEntry
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("group")] public string Group { get; init; } = string.Empty;
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("intervalMs")] public double IntervalMs { get; init; }
    [JsonPropertyName("unit")] public string? Unit { get; init; }
}
