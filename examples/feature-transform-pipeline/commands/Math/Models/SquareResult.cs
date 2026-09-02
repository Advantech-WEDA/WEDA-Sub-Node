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
    public long ExecutedAt { get; init; }
    public long CompletedAt { get; init; }

    object? IResult.ResultData => ResultData;
    long? IResult.ExecutedAt => ExecutedAt;
    long? IResult.CompletedAt => CompletedAt > 0 ? CompletedAt : null;

    public static SquareResult Success(SquareResultData resultData, long executedAt) => new()
    {
        Status = CommandStatusCode.Success,
        Message = "Calculate successfully",
        ResultData = resultData,
        ExecutedAt = executedAt,
        CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };

    public static SquareResult Error(int status, string message, long executedAt) => new()
    {
        Status = status,
        Message = message,
        ExecutedAt = executedAt,
        CompletedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };
}

public class SquareResultData
{
    [JsonPropertyName("Result")]
    public int Result { get; set; }
}
