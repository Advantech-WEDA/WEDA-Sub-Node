using NCalc;

using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace FeatureTransformPipeline.Transforms;

/// <summary>
/// Evaluates a user-supplied NCalc expression against each numeric measurement
/// at runtime, letting operators express real-time calculations without code
/// changes (a lambda-like transform configured entirely from devicecfg).
/// </summary>
/// <remarks>
/// The raw value is bound to the configured variable (default <c>value</c>) and
/// the expression result replaces the measurement value. Non-numeric measures
/// and evaluations that do not yield a number pass through unchanged.
/// </remarks>
public class ExpressionTransform
    : ITelemetryTransform,
      IConfigurableTransform<ExpressionTransform, ExpressionParameters>
{
    private Expression _expression;
    private string _variableName;

    public static string TypeName => "expression";

    public static string? Description =>
        "Evaluate a user-defined NCalc expression per measurement at runtime, "
        + "e.g. \"(value * 9 / 5) + 32\".";

    public static ExpressionTransform Create(ExpressionParameters parameters) =>
        new(parameters.Expression, parameters.VariableName);

    public string Name => nameof(ExpressionTransform);

    public bool Enabled { get; set; } = true;

    public ExpressionTransform(string expression, string variableName = "value")
    {
        _expression = new Expression(expression);
        _variableName = variableName;
    }

    public void UpdateParameters(ExpressionParameters parameters)
    {
        _expression = new Expression(parameters.Expression);
        _variableName = parameters.VariableName;
    }

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled)
        {
            return Task.FromResult(measures);
        }

        var transformed = measures.Select(measure =>
        {
            if (measure.Value is not double and not int and not float and not decimal and not long)
            {
                return measure;
            }

            var input = Convert.ToDouble(measure.Value);
            return TryEvaluate(input, out var output)
                ? measure with { Value = output }
                : measure;
        }).ToList();

        return Task.FromResult(transformed);
    }

    private bool TryEvaluate(double input, out double output)
    {
        output = 0;
        _expression.Parameters[_variableName] = input;

        object? result;
        try
        {
            result = _expression.Evaluate();
        }
        catch
        {
            return false;
        }

        if (result is null or bool or string)
        {
            return false;
        }

        try
        {
            output = Convert.ToDouble(result);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
