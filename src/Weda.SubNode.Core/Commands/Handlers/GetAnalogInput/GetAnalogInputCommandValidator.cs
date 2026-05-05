using ErrorOr;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Core.Commands.Handlers.GetAnalogInput.Models;

namespace Weda.SubNode.Core.Commands.Handlers.GetAnalogInput;

/// <summary>
/// Validator for ai.get command.
/// </summary>
public class GetAnalogInputCommandValidator : ICommandValidator<GetAnalogInputCommand>
{
    public ErrorOr<Success> Validate(GetAnalogInputCommand command)
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
