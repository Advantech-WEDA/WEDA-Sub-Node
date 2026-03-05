using ErrorOr;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Core.Commands.Handlers.SetDigitalOutput.Models;

namespace Weda.SubNode.Core.Commands.Handlers.SetDigitalOutput;

/// <summary>
/// Validator for SetDigitalOutputCommand with complex validation logic.
/// </summary>
/// <remarks>
/// Validates:
/// - Outputs: Must have at least one output
/// - Outputs: Each output must have a non-empty name
/// - Outputs: No duplicate output names
/// </remarks>
public class SetDigitalOutputCommandValidator : ICommandValidator<SetDigitalOutputCommand>
{
    public ErrorOr<Success> Validate(SetDigitalOutputCommand command)
    {
        var errors = new List<Error>();

        // Validate Outputs array
        if (command.Parameters.Outputs is null || command.Parameters.Outputs.Length == 0)
        {
            errors.Add(Errors.Command.ValidationFailed(
                "At least one output must be specified"));
        }
        else
        {
            // Check for empty output names
            for (int i = 0; i < command.Parameters.Outputs.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(command.Parameters.Outputs[i].Name))
                {
                    errors.Add(Errors.Command.ValidationFailed(
                        $"Output at index {i} has an empty name"));
                }
            }

            // Check for duplicate output names
            var duplicates = command.Parameters.Outputs
                .GroupBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                errors.Add(Errors.Command.ValidationFailed(
                    $"Duplicate output names: {string.Join(", ", duplicates)}"));
            }
        }

        return errors.Count > 0 ? errors : Result.Success;
    }
}
