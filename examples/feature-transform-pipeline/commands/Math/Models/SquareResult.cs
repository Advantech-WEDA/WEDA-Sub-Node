using System.Text.Json.Serialization;


using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace FeatureTransformPipeline.commands.Math.Models;

/// <summary>
/// Result of the gpio.list command execution.
/// </summary>
public class SquareResult : IResult
{
    public int Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public SquareResultData? ResultData { get; init; }

    object? IResult.ResultData => ResultData;

    public static SquareResult Success(SquareResultData resultData) => new()
    {
        Status = CommandStatusCode.Success,
        Message = "Calculate successfully",
        ResultData = resultData
    };

    public static SquareResult Error(int status, string message) => new()
    {
        Status = status,
        Message = message
    };
}

public class SquareResultData
{
    [JsonPropertyName("Result")]
    public int Result { get; set; }
}
