using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.Options;

using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Telemetry.Validation;

namespace Weda.SubNode.Core.Telemetry.Validation;

public class SchemaBasedValidator(IOptions<TelemetryOptions> options) : ITelemetryValidator
{
    private readonly TelemetryOptions _options = options.Value;
    public SchemaBasedValidator() : this(Options.Create(TelemetryOptions.Default))
    {   
    }

    public ErrorOr<Success> Validate(object? value, string schema)
    {
        if (value is null)
            return Error.Failure("Value cannot be null");
        
        return schema.ToLowerInvariant() switch
        {
            "double" or "float" or "integer" or "long" => ValidateNumeric(value),
            "boolean" => ValidateBoolean(value),
            "string" => ValidateString(value),
            var s when s.StartsWith("application/json") => ValidateJson(value),
            var s when s.Contains('/') => ValidateBase64(value, _options.MaxBinarySize),
            _ => Result.Success
        };
    }

    private static ErrorOr<Success> ValidateNumeric(object value)
    {
        return value switch
        {
            double or float or int or long or decimal => Result.Success,
            string s when double.TryParse(s, out _) => Result.Success,
            _ => Error.Failure($"Expected numeric value, got {value.GetType().Name}")
        };
    }

    private static ErrorOr<Success> ValidateBoolean(object value)
    {
        return value switch
        {
            bool => Result.Success,
            string s when bool.TryParse(s, out _) => Result.Success,
            _ => Error.Failure($"Expected boolean value, got {value.GetType().Name}")
        };
    }


    private static ErrorOr<Success> ValidateString(object value)
    {
        return value is string ? Result.Success : Error.Failure($"Expected string value, got {value.GetType().Name}");
    }


    private static ErrorOr<Success> ValidateJson(object value)
    {
        if (value is not string s)
            return Error.Failure($"Expected JSON string, got {value.GetType().Name}");

        try
        {
            JsonDocument.Parse(s);
            return Result.Success;
        }
        catch (Exception ex)
        {
            return Error.Failure($"Invalid JSON: {ex.Message}");
        }
    }


    private static ErrorOr<Success> ValidateBase64(object value, int maxSize)
    {
        if (value is not string s)
            return Error.Failure(description: $"Expected Base64 string, got {value.GetType().Name}");

        if (string.IsNullOrEmpty(s))
            return Error.Failure(description: "Base64 string cannot be empty");

        if (s.Length > maxSize)
            return Error.Failure(description: $"Base64 data exceeds maximum size of {maxSize / 1024 / 1024}MB ({s.Length} bytes encoded)");

        try
        {
            Convert.FromBase64String(s);
            return Result.Success;
        }
        catch (FormatException)
        {
            return Error.Failure("Invalid Base64 format");
        }

    }
}