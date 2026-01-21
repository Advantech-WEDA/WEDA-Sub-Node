using ErrorOr;

namespace Weda.SubNode.Abstractions.Commands;

/// <summary>
/// Defines a validator for specific command type.
/// </summary>
/// <typeparam name="TCommand">The type of command to validate.</typeparam>
public interface ICommandValidator<in TCommand>
    where TCommand : ICommand
{
    ErrorOr<Success> Validate(TCommand command);
}