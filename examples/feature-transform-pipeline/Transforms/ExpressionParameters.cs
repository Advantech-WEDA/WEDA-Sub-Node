using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using NCalc;

namespace FeatureTransformPipeline.Transforms;

/// <summary>
/// Configuration for <see cref="ExpressionTransform"/>.
/// </summary>
/// <remarks>
/// The expression is evaluated once per numeric measurement with the raw value
/// bound to <see cref="VariableName"/> (default <c>value</c>). Any NCalc syntax
/// is accepted, e.g. <c>(value * 9 / 5) + 32</c> or <c>Abs(value) &gt; 10 ? value : 0</c>.
/// </remarks>
public class ExpressionParameters : IValidatableObject
{
    [Required(ErrorMessage = "Expression is required")]
    [Description("NCalc expression evaluated per measurement, e.g. \"(value * 9 / 5) + 32\".")]
    [JsonPropertyName("expression")]
    public string Expression { get; init; } = string.Empty;

    [Description("Name of the variable the raw measurement value is bound to.")]
    [JsonPropertyName("variableName")]
    [DefaultValue("value")]
    public string VariableName { get; init; } = "value";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Expression))
        {
            yield return new ValidationResult(
                "Expression cannot be empty.", [nameof(Expression)]);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(VariableName))
        {
            yield return new ValidationResult(
                "VariableName cannot be empty.", [nameof(VariableName)]);
        }

        var probe = new Expression(Expression);
        probe.Parameters[VariableName] = 0.0;
        string? evaluationError = null;
        try
        {
            probe.Evaluate();
        }
        catch (Exception ex)
        {
            evaluationError = ex.Message;
        }

        if (evaluationError is not null)
        {
            yield return new ValidationResult(
                $"Expression is not valid: {evaluationError}", [nameof(Expression)]);
        }
    }
}
