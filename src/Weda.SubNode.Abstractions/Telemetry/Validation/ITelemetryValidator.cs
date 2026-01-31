using ErrorOr;

namespace Weda.SubNode.Abstractions.Telemetry.Validation;

public interface ITelemetryValidator
{
    ErrorOr<Success> Validate(object? value, string schema);
}