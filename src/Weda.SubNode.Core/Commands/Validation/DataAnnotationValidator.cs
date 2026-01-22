using System.ComponentModel.DataAnnotations;

using ErrorOr;

using Weda.SubNode.Abstractions.Commands;

namespace Weda.SubNode.Core.Commands.Validation;

/// <summary>
/// Validator that uses DataAnnotations attributes for command validation.
/// Supports [Required], [Range], [StringLength], [RegularExpression], etc.
/// </summary>
public class DataAnnotationValidator<TCommand> : ICommandValidator<TCommand>
    where TCommand : ICommand
{
    public ErrorOr<Success> Validate(TCommand command)
    {
        var context = new ValidationContext(command);
        var results = new List<ValidationResult>();

        if (Validator.TryValidateObject(command, context, results, validateAllProperties: true))
        {
            return Result.Success;
        }

        var errors = results
            .Where(r => r.ErrorMessage is not null)
            .Select(r => Errors.Command.ValidationFailed(r.ErrorMessage!))
            .ToList();

        return errors.Count > 0 ? errors : Result.Success;
    }
}
