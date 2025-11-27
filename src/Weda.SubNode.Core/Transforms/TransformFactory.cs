using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace Weda.SubNode.Core.Transforms;

/// <summary>
/// Factory for creating ITelemetryTransform instances from TransformConfig
/// </summary>
public static class TransformFactory
{
    /// <summary>
    /// Creates a list of transforms from configuration
    /// Execution order is determined by the array index in the configuration (not by Order property)
    /// </summary>
    /// <param name="configs">Transform configurations</param>
    /// <returns>List of instantiated transforms</returns>
    public static List<ITelemetryTransform> CreateFromConfigs(List<TransformConfig> configs)
    {
        if (configs == null || configs.Count == 0)
            return new List<ITelemetryTransform>();

        var transforms = new List<ITelemetryTransform>();

        // Process in array order (index 0, 1, 2, ...) - no sorting
        // Only filter out disabled transforms
        foreach (var config in configs.Where(c => c.Enabled))
        {
            var transform = CreateTransform(config);
            if (transform != null)
            {
                transforms.Add(transform);
            }
        }

        return transforms;
    }

    /// <summary>
    /// Creates a single transform from configuration
    /// </summary>
    /// <param name="config">Transform configuration</param>
    /// <returns>Transform instance or null if type is not recognized</returns>
    public static ITelemetryTransform? CreateTransform(TransformConfig config)
    {
        if (!config.Enabled)
            return null;

        return config.Type.ToLowerInvariant() switch
        {
            "calibration" => CreateCalibrationTransform(config.Parameters),
            "unitconversion" => CreateUnitConversionTransform(config.Parameters),
            _ => throw new NotSupportedException($"Transform type '{config.Type}' is not supported")
        };
    }

    private static ITelemetryTransform CreateCalibrationTransform(Dictionary<string, object> parameters)
    {
        // Support both linear and curve-based calibration
        if (parameters.TryGetValue("CalibrationCurve", out var curveObj))
        {
            // Curve-based calibration
            var curvePoints = ParseCalibrationCurve(curveObj);
            return new CalibrationTransform(curvePoints);
        }
        else
        {
            // Linear calibration (Scale and Offset)
            var scale = GetDoubleParameter(parameters, "Scale", 1.0);
            var offset = GetDoubleParameter(parameters, "Offset", 0.0);
            return new CalibrationTransform(scale, offset);
        }
    }

    private static ITelemetryTransform CreateUnitConversionTransform(Dictionary<string, object> parameters)
    {
        var fromUnit = GetStringParameter(parameters, "FromUnit", string.Empty);
        var toUnit = GetStringParameter(parameters, "ToUnit", string.Empty);

        if (string.IsNullOrEmpty(fromUnit) || string.IsNullOrEmpty(toUnit))
            throw new ArgumentException("UnitConversion requires FromUnit and ToUnit parameters");

        return new UnitConversionTransform(fromUnit, toUnit);
    }

    private static List<CalibrationPoint> ParseCalibrationCurve(object curveObj)
    {
        // Handle different curve object types
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
        {
            return Convert.ToDouble(value);
        }
        return defaultValue;
    }

    private static string GetStringParameter(Dictionary<string, object> parameters, string key, string defaultValue)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }
}