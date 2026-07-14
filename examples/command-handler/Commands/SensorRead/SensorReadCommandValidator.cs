using ErrorOr;

using CommandHandlerExample.Commands.SensorRead.Models;

using Weda.SubNode.Abstractions.Commands;

namespace CommandHandlerExample.Commands.SensorRead;

/// <summary>
/// Validator for SensorReadCommand.
/// </summary>
/// <remarks>
/// Validates:
/// - SensorName: Must be non-empty (whitespace-only names are rejected,
///   which DataAnnotation [Required] alone does not catch)
/// </remarks>
public class SensorReadCommandValidator : ICommandValidator<SensorReadCommand>
{
    public ErrorOr<Success> Validate(SensorReadCommand command)
    {
        var errors = new List<Error>();

        if (command.Parameters is null || string.IsNullOrWhiteSpace(command.Parameters.SensorName))
        {
            errors.Add(Errors.Command.ValidationFailed(
                "Sensor name must be a non-empty string"));
        }

        return errors.Count > 0 ? errors : Result.Success;
    }
}
