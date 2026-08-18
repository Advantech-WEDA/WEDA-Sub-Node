using CommandHandlerExample.Commands.GetDeviceProps.Models;

using ErrorOr;

using Weda.SubNode.Abstractions.Commands;

namespace CommandHandlerExample.Commands.GetDeviceProps;

/// <summary>
/// Validator for GetDevicePropsCommand.
/// </summary>
/// <remarks>
/// The options map may be empty (defaults apply), but when options are
/// supplied they must be well-formed:
/// - Option keys must be non-empty and non-whitespace
/// - Only known option keys are accepted (deviceName, includeSensors)
/// - includeSensors, when present and non-blank, must parse as a boolean
/// </remarks>
public class GetDevicePropsCommandValidator : ICommandValidator<GetDevicePropsCommand>
{
    private static readonly HashSet<string> KnownOptions =
        new(StringComparer.OrdinalIgnoreCase) { "deviceName", "includeSensors" };

    public ErrorOr<Success> Validate(GetDevicePropsCommand command)
    {
        var options = command.Parameters?.Options;
        if (options is null || options.Count == 0)
        {
            return Result.Success;
        }

        var errors = new List<Error>();

        if (options.Keys.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add(Errors.Command.ValidationFailed(
                "Option keys must be non-empty strings"));
        }

        var unknown = options.Keys
            .Where(k => !string.IsNullOrWhiteSpace(k) && !KnownOptions.Contains(k))
            .ToList();
        if (unknown.Count > 0)
        {
            errors.Add(Errors.Command.ValidationFailed(
                $"Unknown options: [{string.Join(", ", unknown)}]. " +
                $"Supported: [{string.Join(", ", KnownOptions)}]"));
        }

        var includeSensors = options.FirstOrDefault(o =>
            o.Key.Equals("includeSensors", StringComparison.OrdinalIgnoreCase)).Value;
        if (!string.IsNullOrWhiteSpace(includeSensors) && !bool.TryParse(includeSensors, out _))
        {
            errors.Add(Errors.Command.ValidationFailed(
                $"Option 'includeSensors' must be \"true\" or \"false\", got \"{includeSensors}\""));
        }

        return errors.Count > 0 ? errors : Result.Success;
    }
}
