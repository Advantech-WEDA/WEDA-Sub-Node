using ErrorOr;

using Weda.SubNode.Abstractions.Context;

namespace Weda.SubNode.Abstractions.Commands.Behaviors;
public class ValidatorBehavior<TCommand, TResult>(ICommandValidator<TCommand>? validator) : IPipelineBehavior<TCommand, TResult>
    where TCommand : ICommand
{
    public async Task<ErrorOr<TResult>> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        Func<Task<ErrorOr<TResult>>> next,
        CancellationToken cancellationToken = default)
    {
        if (validator is null)
        {
            return await next();
        }

        var validationResult = validator.Validate(command);
        
        if (validationResult.IsError)
        {
            return validationResult.Errors;
        }
        
        return await next();
    }
}