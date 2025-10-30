namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Transformation configuration
/// </summary>
public class TransformConfig
{
    /// <summary>
    /// Transform type (e.g., "Calibration", "UnitConversion", etc.)
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Transform enabled/disabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Transform-specific parameters
    /// For Calibration: { "Scale": 1.0, "Offset": 0.0 }
    /// For UnitConversion: { "FromUnit": "celsius", "ToUnit": "fahrenheit" }
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = [];

    /// <summary>
    /// Order/priority in the pipeline (lower number = earlier in pipeline)
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// Sensor-specific configuration including transforms, DSP, and thresholds
/// </summary>
public class SensorConfig
{
    /// <summary>
    /// Sensor enabled/disabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Interval in milliseconds (milliseconds per sample)
    /// </summary>
    public double Interval { get; set; } = 1000;

    /// <summary>
    /// Unit of measurement (e.g., "celsius", "percent", "pascal")
    /// </summary>
    public string? Unit { get; set; } = string.Empty;

    /// <summary>
    /// Transformation pipeline (ordered list)
    /// Transforms are applied in order before DSP filters
    /// Examples: CalibrationTransform, UnitConversionTransform, etc.
    /// </summary>
    public List<TransformConfig> TransformPipeline { get; set; } = [];

    /// <summary>
    /// DSP filter pipeline (ordered list)
    /// Filters are applied in order after transformations
    /// </summary>
    public List<DspFilterConfig> DspPipeline { get; set; } = [];

    /// <summary>
    /// Threshold configuration for alerts
    /// </summary>
    public ThresholdConfig? Thresholds { get; set; }
}

/// <summary>
/// DSP filter configuration
/// </summary>
public class DspFilterConfig
{
    /// <summary>
    /// Filter type (e.g., "kalman", "movingAverage", "lowpass", "highpass")
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Filter enabled/disabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Filter-specific parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = [];

    /// <summary>
    /// Order/priority in the pipeline (lower number = earlier in pipeline)
    /// </summary>
    public int Order { get; set; }
}

/// <summary>
/// Threshold configuration for sensor alerts
/// </summary>
public class ThresholdConfig
{
    /// <summary>
    /// Upper warning threshold
    /// </summary>
    public double? UpperWarning { get; set; }

    /// <summary>
    /// Upper critical threshold
    /// </summary>
    public double? UpperCritical { get; set; }

    /// <summary>
    /// Lower warning threshold
    /// </summary>
    public double? LowerWarning { get; set; }

    /// <summary>
    /// Lower critical threshold
    /// </summary>
    public double? LowerCritical { get; set; }

    /// <summary>
    /// Check if value exceeds any threshold
    /// </summary>
    public ThresholdLevel CheckValue(double value)
    {
        if (UpperCritical.HasValue && value >= UpperCritical.Value)
            return ThresholdLevel.UpperCritical;

        if (LowerCritical.HasValue && value <= LowerCritical.Value)
            return ThresholdLevel.LowerCritical;

        if (UpperWarning.HasValue && value >= UpperWarning.Value)
            return ThresholdLevel.UpperWarning;

        if (LowerWarning.HasValue && value <= LowerWarning.Value)
            return ThresholdLevel.LowerWarning;

        return ThresholdLevel.Normal;
    }
}

/// <summary>
/// Threshold level enumeration
/// </summary>
public enum ThresholdLevel
{
    Normal,
    LowerWarning,
    UpperWarning,
    LowerCritical,
    UpperCritical
}
