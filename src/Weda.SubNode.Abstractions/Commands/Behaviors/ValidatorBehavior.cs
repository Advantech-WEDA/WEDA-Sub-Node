using ErrorOr;

using Weda.SubNode.Abstractions.Context;

namespace Weda.SubNode.Abstractions.Commands.Behaviors;

/// <summary>
/// Pipeline behavior that validates commands using a custom validator.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public class ValidatorBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, TResult>
    where TCommand : ICommand
{
    private readonly ICommandValidator<TCommand>? _validator;

    /// <summary>
    /// Initializes a new instance with no validator.
    /// </summary>
    public ValidatorBehavior()
    {
        _validator = null;
    }

    /// <summary>
    /// Initializes a new instance with the specified validator.
    /// </summary>
    /// <param name="validator">The validator to use.</param>
    public ValidatorBehavior(ICommandValidator<TCommand>? validator)
    {
        _validator = validator;
    }

    public async Task<ErrorOr<TResult>> HandleAsync(
        TCommand command,
        IWedaApplicationContext context,
        Func<Task<ErrorOr<TResult>>> next,
        CancellationToken cancellationToken = default)
    {
        if (_validator is null)
        {
            return await next();
        }

        var validationResult = _validator.Validate(command);

        if (validationResult.IsError)
        {
            return validationResult.Errors;
        }

        return await next();
    }
}