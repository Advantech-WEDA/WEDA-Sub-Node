using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace FeatureTransformPipeline.commands.DeviceSummary.Models;

public class DeviceSummaryResult : IResult
{
    public int Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public DeviceSummaryResultData? ResultData { get; init; }

    object? IResult.ResultData => ResultData;

    public static DeviceSummaryResult Success(DeviceSummaryResultData resultData) => new()
    {
        Status = CommandStatusCode.Success,
        Message = "Device summary collected successfully",
        ResultData = resultData
    };

    public static DeviceSummaryResult Error(int status, string message) => new()
    {
        Status = status,
        Message = message
    };
}

public class DeviceSummaryResultData
{
    [JsonPropertyName("deviceCount")]
    public int DeviceCount { get; set; }

    [JsonPropertyName("devices")]
    public List<DeviceSummaryEntry> Devices { get; set; } = [];
}

public class DeviceSummaryEntry
{
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string? Host { get; set; }

    [JsonPropertyName("port")]
    public int? Port { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("swVersion")]
    public string? SwVersion { get; set; }

    [JsonPropertyName("subNodeType")]
    public string? SubNodeType { get; set; }

    [JsonPropertyName("sensorCount")]
    public int SensorCount { get; set; }

    /// <summary>
    /// Sensor inventory, flattened to "name|group|reportEnabled" entries to keep
    /// the command's DTDL Result within the 5-level nesting limit.
    /// </summary>
    [JsonPropertyName("sensors")]
    public List<string> Sensors { get; set; } = [];
}
