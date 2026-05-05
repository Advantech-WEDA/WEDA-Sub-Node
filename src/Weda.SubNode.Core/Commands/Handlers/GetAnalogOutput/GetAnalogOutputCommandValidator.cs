using ErrorOr;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Core.Commands.Handlers.GetAnalogOutput.Models;

namespace Weda.SubNode.Core.Commands.Handlers.GetAnalogOutput;

/// <summary>
/// Validator for ao.get command.
/// </summary>
public class GetAnalogOutputCommandValidator : ICommandValidator<GetAnalogOutputCommand>
{
    public ErrorOr<Success> Validate(GetAnalogOutputCommand command)
    {
        var errors = new List<Error>();

        // Validate output names if specified
        if (command.Parameters.Outputs.Length > 0)
        {
            foreach (var output in command.Parameters.Outputs)
            {
                if (string.IsNullOrWhiteSpace(output))
                {
                    errors.Add(Errors.Command.ValidationFailed("Output name cannot be empty"));
                }
            }

            // Check for duplicates
            var duplicates = command.Parameters.Outputs
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
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
