using CommandHandlerExample.Commands.SetSensorTags.Models;

using ErrorOr;

using Weda.SubNode.Abstractions.Commands;

namespace CommandHandlerExample.Commands.SetSensorTags;

/// <summary>
/// Validator for SetSensorTagsCommand.
/// </summary>
/// <remarks>
/// Validates:
/// - SensorName: Must be non-empty
/// - Tags: Must contain at least one entry
/// - Tags: Keys must be non-empty and non-whitespace
/// - Tags: Values must not be null
/// </remarks>
public class SetSensorTagsCommandValidator : ICommandValidator<SetSensorTagsCommand>
{
    public ErrorOr<Success> Validate(SetSensorTagsCommand command)
    {
        var errors = new List<Error>();

        if (command.Parameters is null)
        {
            return new List<Error>
            {
                Errors.Command.ValidationFailed("Parameters are required")
            };
        }

        if (string.IsNullOrWhiteSpace(command.Parameters.SensorName))
        {
            errors.Add(Errors.Command.ValidationFailed(
                "Sensor name must be a non-empty string"));
        }

        var tags = command.Parameters.Tags;
        if (tags is null || tags.Count == 0)
        {
            errors.Add(Errors.Command.ValidationFailed(
                "At least one tag must be specified"));
        }
        else
        {
            if (tags.Keys.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add(Errors.Command.ValidationFailed(
                    "Tag keys must be non-empty strings"));
            }

            var nullValueKeys = tags
                .Where(t => t.Value is null)
                .Select(t => t.Key)
                .ToList();
            if (nullValueKeys.Count > 0)
            {
                errors.Add(Errors.Command.ValidationFailed(
                    $"Tag values must not be null: [{string.Join(", ", nullValueKeys)}]"));
            }
        }

        return errors.Count > 0 ? errors : Result.Success;
    }
}
