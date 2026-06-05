using ErrorOr;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Core.Commands.Handlers.GetDigitalInput.Models;

namespace Weda.SubNode.Core.Commands.Handlers.GetDigitalInput;

/// <summary>
/// Validator for di.get command.
/// </summary>
public class GetDigitalInputCommandValidator : ICommandValidator<GetDigitalInputCommand>
{
    public ErrorOr<Success> Validate(GetDigitalInputCommand command)
    {
        var errors = new List<Error>();

        // Validate input names if specified
        if (command.Parameters.Inputs.Length > 0)
        {
            foreach (var input in command.Parameters.Inputs)
            {
                if (string.IsNullOrWhiteSpace(input))
                {
                    errors.Add(Errors.Command.ValidationFailed("Input name cannot be empty"));
                }
            }

            // Check for duplicates
            var duplicates = command.Parameters.Inputs
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                errors.Add(Errors.Command.ValidationFailed(
                    $"Duplicate input names: {string.Join(", ", duplicates)}"));
            }
        }

        return errors.Count > 0 ? errors : Result.Success;
    }
}
