using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace Weda.SubNode.Core.Transforms;

/// <summary>
/// Calibration point for non-linear (curve-based) calibration.
/// </summary>
public record CalibrationPoint
{
    [Description("Raw sensor value before calibration.")]
    [JsonPropertyName("rawValue")]
    public double RawValue { get; init; }

    [Description("Calibrated sensor value after calibration.")]
    [JsonPropertyName("calibratedValue")]
    public double CalibratedValue { get; init; }
}

/// <summary>
/// Configuration parameters for <see cref="CalibrationTransform"/>.
/// </summary>
/// <remarks>
/// Two modes are supported:
/// <list type="bullet">
/// <item><b>Linear</b>: provide <see cref="Scale"/> and <see cref="Offset"/>.</item>
/// <item><b>Curve-based</b>: provide <see cref="CalibrationCurve"/> with at least one point.</item>
/// </list>
/// When <see cref="CalibrationCurve"/> is provided, it takes precedence over Scale/Offset.
/// </remarks>
public class CalibrationParameters : IValidatableObject
{
    [Description("Linear calibration scale factor. Must not be zero.")]
    [JsonPropertyName("scale")]
    [DefaultValue(1.0)]
    public double Scale { get; init; } = 1.0;

    [Description("Linear calibration offset value.")]
    [JsonPropertyName("offset")]
    [DefaultValue(0.0)]
    public double Offset { get; init; } = 0.0;

    [Description("Curve points for non-linear calibration. Overrides Scale/Offset when provided.")]
    [JsonPropertyName("calibrationCurve")]
    public IReadOnlyList<CalibrationPoint>? CalibrationCurve { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CalibrationCurve is { Count: 0 })
        {
            yield return new ValidationResult(
                "CalibrationCurve must contain at least one point when provided.",
                [nameof(CalibrationCurve)]);
        }

        if (CalibrationCurve is null && Scale == 0)
        {
            yield return new ValidationResult(
                "Scale cannot be 0 in linear calibration mode.",
                [nameof(Scale)]);
        }
    }
}

/// <summary>
/// Calibration transform that applies linear or curve-based calibration.
/// Linear formula: calibrated_value = (raw_value * Scale) + Offset.
/// </summary>
public class CalibrationTransform
    : ITelemetryTransform,
      IConfigurableTransform<CalibrationTransform, CalibrationParameters>
{
    private double _scale;
    private double _offset;
    private IReadOnlyList<CalibrationPoint>? _calibrationCurve;

    public static string TypeName => "calibration";

    public static string? Description =>
        "Apply linear (Scale + Offset) or curve-based calibration to numeric measurements.";

    public static CalibrationTransform Create(CalibrationParameters parameters)
    {
        if (parameters.CalibrationCurve is { Count: > 0 })
        {
            return new CalibrationTransform(parameters.CalibrationCurve);
        }
        return new CalibrationTransform(parameters.Scale, parameters.Offset);
    }

    public string Name => nameof(CalibrationTransform);

    public bool Enabled { get; set; } = true;

    public CalibrationTransform(double scale = 1.0, double offset = 0.0)
    {
        _scale = scale;
        _offset = offset;
        _calibrationCurve = null;
    }

    public CalibrationTransform(IReadOnlyList<CalibrationPoint> calibrationCurve)
    {
        if (calibrationCurve is null || calibrationCurve.Count == 0)
        {
            throw new ArgumentException(
                "Calibration curve must contain at least one point", nameof(calibrationCurve));
        }
        _calibrationCurve = calibrationCurve;
        _scale = 1.0;
        _offset = 0.0;
    }

    public void UpdateParameters(CalibrationParameters parameters)
    {
        if (parameters.CalibrationCurve is { Count: > 0 })
        {
            _calibrationCurve = parameters.CalibrationCurve;
            return;
        }
        _calibrationCurve = null;
        _scale = parameters.Scale;
        _offset = parameters.Offset;
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
            if (measure.Value is not double and not int and not float and not decimal)
            {
                return measure;
            }

            var rawValue = Convert.ToDouble(measure.Value);
            var calibratedValue = ApplyCalibration(rawValue);
            return measure with { Value = calibratedValue };
        }).ToList();

        return Task.FromResult(transformed);
    }

    private double ApplyCalibration(double rawValue)
    {
        if (_calibrationCurve is { Count: > 0 })
        {
            return ApplyCurve(rawValue);
        }
        return (rawValue * _scale) + _offset;
    }

    private double ApplyCurve(double rawValue)
    {
        if (_calibrationCurve is null || _calibrationCurve.Count == 0)
        {
            return rawValue;
        }

        var sortedPoints = _calibrationCurve.OrderBy(p => p.RawValue).ToList();

        if (rawValue <= sortedPoints[0].RawValue)
        {
            return sortedPoints[0].CalibratedValue;
        }
        if (rawValue >= sortedPoints[^1].RawValue)
        {
            return sortedPoints[^1].CalibratedValue;
        }

        for (int i = 0; i < sortedPoints.Count - 1; i++)
        {
            var p1 = sortedPoints[i];
            var p2 = sortedPoints[i + 1];
            if (rawValue >= p1.RawValue && rawValue <= p2.RawValue)
            {
                var ratio = (rawValue - p1.RawValue) / (p2.RawValue - p1.RawValue);
                return p1.CalibratedValue + ratio * (p2.CalibratedValue - p1.CalibratedValue);
            }
        }

        return rawValue;
    }
}
