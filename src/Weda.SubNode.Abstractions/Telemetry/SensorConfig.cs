namespace Weda.SubNode.Abstractions.Telemetry;

using System.Text.Json.Serialization;
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Abstractions.Dsp;

/// <summary>
/// Transformation configuration (for serialization/deserialization)
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
    /// Transformation pipeline (config-based, for serialization)
    /// Transforms are applied in order before DSP filters
    /// Examples: CalibrationTransform, UnitConversionTransform, etc.
    /// </summary>
    public List<TransformConfig> TransformPipeline { get; set; } = [];

    /// <summary>
    /// DSP filter pipeline (config-based, for serialization)
    /// Filters are applied in order after transformations
    /// </summary>
    public List<DspFilterConfig> DspPipeline { get; set; } = [];

    /// <summary>
    /// Runtime transformation pipeline (programmatic, not serialized)
    /// Use this for design-time configuration in code
    /// Execution order is the List order (index 0, 1, 2, ...)
    /// Thread-safe: All operations are protected by lock
    /// </summary>
    [JsonIgnore]
    private readonly List<ITelemetryTransform> _runtimeTransforms = [];

    /// <summary>
    /// Runtime DSP filter pipeline (programmatic, not serialized)
    /// Use this for design-time configuration in code
    /// Execution order is the List order (index 0, 1, 2, ...)
    /// Thread-safe: All operations are protected by lock
    /// </summary>
    [JsonIgnore]
    private readonly List<IDspFilter> _runtimeDspFilters = [];

    /// <summary>
    /// Lock for thread-safe access to runtime transforms
    /// </summary>
    [JsonIgnore]
    private readonly object _transformLock = new();

    /// <summary>
    /// Lock for thread-safe access to runtime DSP filters
    /// </summary>
    [JsonIgnore]
    private readonly object _dspFilterLock = new();

    /// <summary>
    /// Gets a thread-safe snapshot of runtime transforms
    /// Returns a copy to prevent modification during iteration
    /// </summary>
    [JsonIgnore]
    public List<ITelemetryTransform> RuntimeTransforms
    {
        get
        {
            lock (_transformLock)
            {
                return [.. _runtimeTransforms];
            }
        }
    }

    /// <summary>
    /// Gets a thread-safe snapshot of runtime DSP filters
    /// Returns a copy to prevent modification during iteration
    /// </summary>
    [JsonIgnore]
    public List<IDspFilter> RuntimeDspFilters
    {
        get
        {
            lock (_dspFilterLock)
            {
                return [.. _runtimeDspFilters];
            }
        }
    }

    /// <summary>
    /// Gets the count of runtime transforms (thread-safe)
    /// </summary>
    [JsonIgnore]
    public int RuntimeTransformsCount
    {
        get
        {
            lock (_transformLock)
            {
                return _runtimeTransforms.Count;
            }
        }
    }

    /// <summary>
    /// Gets the count of runtime DSP filters (thread-safe)
    /// </summary>
    [JsonIgnore]
    public int RuntimeDspFiltersCount
    {
        get
        {
            lock (_dspFilterLock)
            {
                return _runtimeDspFilters.Count;
            }
        }
    }

    /// <summary>
    /// Threshold configuration for alerts
    /// </summary>
    public ThresholdConfig? Thresholds { get; set; }

    // ===== Transform Pipeline Methods (Thread-Safe) =====

    /// <summary>
    /// Adds a transform to the end of the transform pipeline (thread-safe)
    /// </summary>
    public SensorConfig AddTransform(ITelemetryTransform transform)
    {
        lock (_transformLock)
        {
            _runtimeTransforms.Add(transform);
        }
        return this;
    }

    /// <summary>
    /// Inserts a transform at the specified index in the transform pipeline (thread-safe)
    /// </summary>
    public SensorConfig InsertTransformAt(int index, ITelemetryTransform transform)
    {
        lock (_transformLock)
        {
            _runtimeTransforms.Insert(index, transform);
        }
        return this;
    }

    /// <summary>
    /// Removes the transform at the specified index (thread-safe)
    /// </summary>
    public SensorConfig RemoveTransformAt(int index)
    {
        lock (_transformLock)
        {
            _runtimeTransforms.RemoveAt(index);
        }
        return this;
    }

    /// <summary>
    /// Removes the specified transform from the pipeline (thread-safe)
    /// </summary>
    public SensorConfig RemoveTransform(ITelemetryTransform transform)
    {
        lock (_transformLock)
        {
            _runtimeTransforms.Remove(transform);
        }
        return this;
    }

    /// <summary>
    /// Moves a transform from one index to another (thread-safe)
    /// </summary>
    public SensorConfig MoveTransform(int fromIndex, int toIndex)
    {
        lock (_transformLock)
        {
            var transform = _runtimeTransforms[fromIndex];
            _runtimeTransforms.RemoveAt(fromIndex);
            _runtimeTransforms.Insert(toIndex, transform);
        }
        return this;
    }

    /// <summary>
    /// Clears all transforms from the pipeline (thread-safe)
    /// </summary>
    public SensorConfig ClearTransforms()
    {
        lock (_transformLock)
        {
            _runtimeTransforms.Clear();
        }
        return this;
    }

    // ===== DSP Filter Pipeline Methods (Thread-Safe) =====

    /// <summary>
    /// Adds a DSP filter to the end of the DSP filter pipeline (thread-safe)
    /// </summary>
    public SensorConfig AddDspFilter(IDspFilter filter)
    {
        lock (_dspFilterLock)
        {
            _runtimeDspFilters.Add(filter);
        }
        return this;
    }

    /// <summary>
    /// Inserts a DSP filter at the specified index in the DSP filter pipeline (thread-safe)
    /// </summary>
    public SensorConfig InsertDspFilterAt(int index, IDspFilter filter)
    {
        lock (_dspFilterLock)
        {
            _runtimeDspFilters.Insert(index, filter);
        }
        return this;
    }

    /// <summary>
    /// Removes the DSP filter at the specified index (thread-safe)
    /// </summary>
    public SensorConfig RemoveDspFilterAt(int index)
    {
        lock (_dspFilterLock)
        {
            _runtimeDspFilters.RemoveAt(index);
        }
        return this;
    }

    /// <summary>
    /// Removes the specified DSP filter from the pipeline (thread-safe)
    /// </summary>
    public SensorConfig RemoveDspFilter(IDspFilter filter)
    {
        lock (_dspFilterLock)
        {
            _runtimeDspFilters.Remove(filter);
        }
        return this;
    }

    /// <summary>
    /// Moves a DSP filter from one index to another (thread-safe)
    /// </summary>
    public SensorConfig MoveDspFilter(int fromIndex, int toIndex)
    {
        lock (_dspFilterLock)
        {
            var filter = _runtimeDspFilters[fromIndex];
            _runtimeDspFilters.RemoveAt(fromIndex);
            _runtimeDspFilters.Insert(toIndex, filter);
        }
        return this;
    }

    /// <summary>
    /// Clears all DSP filters from the pipeline (thread-safe)
    /// </summary>
    public SensorConfig ClearDspFilters()
    {
        lock (_dspFilterLock)
        {
            _runtimeDspFilters.Clear();
        }
        return this;
    }
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
