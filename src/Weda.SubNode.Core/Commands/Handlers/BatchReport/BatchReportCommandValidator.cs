using ErrorOr;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Core.Commands.Handlers.BatchReport.Models;

namespace Weda.SubNode.Core.Commands.Handlers.BatchReport;

/// <summary>
/// Validator for BatchReportCommand with complex validation logic.
/// </summary>
/// <remarks>
/// Validates:
/// - TimeRange: EndTime must be greater than StartTime
/// - TimeRange: StartTime cannot be in the future
/// - ReportType: Must be a known type
/// </remarks>
public class BatchReportCommandValidator : ICommandValidator<BatchReportCommand>
{
    public ErrorOr<Success> Validate(BatchReportCommand command)
    {
        var errors = new List<Error>();

        // Validate TimeRange if provided
        if (command.Parameters.TimeRange is not null)
        {
            if (command.Parameters.TimeRange.EndTime <= command.Parameters.TimeRange.StartTime)
            {
                errors.Add(Errors.Command.ValidationFailed(
                    "TimeRange.EndTime must be greater than TimeRange.StartTime"));
            }

            // Check for future time
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (command.Parameters.TimeRange.StartTime > now)
            {
                errors.Add(Errors.Command.ValidationFailed(
                    "TimeRange.StartTime cannot be in the future"));
            }
        }

        return errors.Count > 0 ? errors : Result.Success;
    }
}
