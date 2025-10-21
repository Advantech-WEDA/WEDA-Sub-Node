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
public class CalibrationTransform : ITelemetryTransform
{
    private readonly double _scale;
    private readonly double _offset;
    private readonly IReadOnlyList<CalibrationPoint>? _calibrationCurve;

    public string Name => nameof(CalibrationTransform);

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

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
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
