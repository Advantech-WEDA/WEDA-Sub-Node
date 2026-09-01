using ErrorOr;

using FeatureTransformPipeline.commands.Math.Models;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Context;

namespace FeatureTransformPipeline.commands.Math;

[Logging(LogLevel.Information)]
public class SquareCommandHandler : ICommandHandler<SquareCommand, SquareResult>
{
    public Task<ErrorOr<SquareResult>> HandleAsync(
        SquareCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<SquareCommandHandler>();
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        cancellationToken.ThrowIfCancellationRequested();
        
        return Task.FromResult<ErrorOr<SquareResult>>(
            SquareResult.Success(new SquareResultData { Result = command.Parameters.Value * command.Parameters.Value }, executedAt));
    }
}
