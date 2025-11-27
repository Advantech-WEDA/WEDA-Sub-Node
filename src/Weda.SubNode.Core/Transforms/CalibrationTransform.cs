using ErrorOr;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace Weda.SubNode.Core.Transforms;

/// <summary>
/// Calibration point for non-linear calibration
/// </summary>
public record CalibrationPoint
{
    /// <summary>
    /// Raw sensor value (before calibration)
    /// </summary>
    public required double RawValue { get; init; }

    /// <summary>
    /// Calibrated sensor value (after calibration)
    /// </summary>
    public required double CalibratedValue { get; init; }
}

/// <summary>
/// Calibration transform that applies linear or curve-based calibration
/// Formula: calibrated_value = (raw_value * Scale) + Offset
/// </summary>
public class CalibrationTransform : ITelemetryTransform, IConfigurableTransform<CalibrationTransform>
{
    private double _scale;
    private double _offset;
    private readonly IReadOnlyList<CalibrationPoint>? _calibrationCurve;

    // === Static Abstract Implementation (Self-Registration) ===

    /// <inheritdoc/>
    public static string TypeName => "calibration";

    /// <inheritdoc/>
    public static CalibrationTransform Create(Dictionary<string, object> parameters)
    {
        // Support both linear and curve-based calibration
        if (parameters.TryGetValue("CalibrationCurve", out var curveObj))
        {
            var curvePoints = ParseCalibrationCurve(curveObj);
            return new CalibrationTransform(curvePoints);
        }

        // Linear calibration (Scale and Offset)
        var scale = GetDoubleParameter(parameters, "Scale", 1.0);
        var offset = GetDoubleParameter(parameters, "Offset", 0.0);
        return new CalibrationTransform(scale, offset);
    }

    private static List<CalibrationPoint> ParseCalibrationCurve(object curveObj)
    {
        if (curveObj is not System.Collections.IEnumerable enumerable)
            throw new ArgumentException("CalibrationCurve must be an enumerable collection");

        var points = new List<CalibrationPoint>();

        foreach (var item in enumerable)
        {
            if (item is Dictionary<string, object> dict)
            {
                var rawValue = GetDoubleParameter(dict, "RawValue", 0);
                var calibratedValue = GetDoubleParameter(dict, "CalibratedValue", 0);

                points.Add(new CalibrationPoint
                {
                    RawValue = rawValue,
                    CalibratedValue = calibratedValue
                });
            }
        }

        if (points.Count == 0)
            throw new ArgumentException("CalibrationCurve must contain at least one point");

        return points;
    }

    private static double GetDoubleParameter(Dictionary<string, object> parameters, string key, double defaultValue)
    {
        if (parameters.TryGetValue(key, out var value))
            return Convert.ToDouble(value);
        return defaultValue;
    }

    // === Instance Members ===

    public string Name => nameof(CalibrationTransform);

    /// <inheritdoc/>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Creates linear calibration transform
    /// </summary>
    /// <param name="scale">Scale factor (default: 1.0)</param>
    /// <param name="offset">Offset value (default: 0.0)</param>
    public CalibrationTransform(double scale = 1.0, double offset = 0.0)
    {
        _scale = scale;
        _offset = offset;
        _calibrationCurve = null;
    }

    /// <summary>
    /// Creates curve-based calibration transform
    /// </summary>
    /// <param name="calibrationCurve">Calibration curve points for non-linear calibration</param>
    public CalibrationTransform(IReadOnlyList<CalibrationPoint> calibrationCurve)
    {
        if (calibrationCurve == null || calibrationCurve.Count == 0)
            throw new ArgumentException("Calibration curve must contain at least one point", nameof(calibrationCurve));

        _calibrationCurve = calibrationCurve;
        _scale = 1.0;
        _offset = 0.0;
    }

    /// <inheritdoc/>
    public ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("Scale", out var s))
        {
            var scale = Convert.ToDouble(s);
            if (scale == 0)
                return Error.Validation("CalibrationTransform.Scale", "Scale cannot be 0");
        }

        return Result.Success;
    }

    /// <inheritdoc/>
    public void UpdateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("Scale", out var s))
            _scale = Convert.ToDouble(s);

        if (parameters.TryGetValue("Offset", out var o))
            _offset = Convert.ToDouble(o);
    }

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // Pass through if disabled
        if (!Enabled)
            return Task.FromResult(measures);

        var transformed = measures.Select(measure =>
        {
            // Skip non-numeric values
            if (measure.Value is not double and not int and not float and not decimal)
                return measure;

            var rawValue = Convert.ToDouble(measure.Value);
            var calibratedValue = ApplyCalibration(rawValue);

            return measure with { Value = calibratedValue };
        }).ToList();

        return Task.FromResult(transformed);
    }

    private double ApplyCalibration(double rawValue)
    {
        // Use calibration curve if provided
        if (_calibrationCurve != null && _calibrationCurve.Count > 0)
        {
            return ApplyCurve(rawValue);
        }

        // Otherwise use linear calibration
        return (rawValue * _scale) + _offset;
    }

    private double ApplyCurve(double rawValue)
    {
        if (_calibrationCurve == null || _calibrationCurve.Count == 0)
            return rawValue;

        // Sort points by raw value
        var sortedPoints = _calibrationCurve.OrderBy(p => p.RawValue).ToList();

        // If below first point, use first point's calibration
        if (rawValue <= sortedPoints[0].RawValue)
            return sortedPoints[0].CalibratedValue;

        // If above last point, use last point's calibration
        if (rawValue >= sortedPoints[^1].RawValue)
            return sortedPoints[^1].CalibratedValue;

        // Find surrounding points and interpolate
        for (int i = 0; i < sortedPoints.Count - 1; i++)
        {
            var p1 = sortedPoints[i];
            var p2 = sortedPoints[i + 1];

            if (rawValue >= p1.RawValue && rawValue <= p2.RawValue)
            {
                // Linear interpolation
                var ratio = (rawValue - p1.RawValue) / (p2.RawValue - p1.RawValue);
                return p1.CalibratedValue + ratio * (p2.CalibratedValue - p1.CalibratedValue);
            }
        }

        return rawValue; // Fallback
    }
}
