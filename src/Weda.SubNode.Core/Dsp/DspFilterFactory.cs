using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Dsp;

/// <summary>
/// Factory for creating IDspFilter instances from DspFilterConfig
/// </summary>
public static class DspFilterFactory
{
    /// <summary>
    /// Creates a list of DSP filters from configuration
    /// Execution order is determined by the array index in the configuration (not by Order property)
    /// </summary>
    /// <param name="configs">DSP filter configurations</param>
    /// <returns>List of instantiated filters</returns>
    public static List<IDspFilter> CreateFromConfigs(List<DspFilterConfig> configs)
    {
        if (configs == null || configs.Count == 0)
            return new List<IDspFilter>();

        var filters = new List<IDspFilter>();

        // Process in array order (index 0, 1, 2, ...) - no sorting
        // Only filter out disabled filters
        foreach (var config in configs.Where(c => c.Enabled))
        {
            var filter = CreateFilter(config);
            if (filter != null)
            {
                filters.Add(filter);
            }
        }

        return filters;
    }

    /// <summary>
    /// Creates a single DSP filter from configuration
    /// </summary>
    /// <param name="config">DSP filter configuration</param>
    /// <returns>Filter instance or null if type is not recognized</returns>
    public static IDspFilter? CreateFilter(DspFilterConfig config)
    {
        if (!config.Enabled)
            return null;

        return config.Type.ToLowerInvariant() switch
        {
            "kalman" => CreateKalmanFilter(config.Parameters),
            "movingaverage" => CreateMovingAverageFilter(config.Parameters),
            "relu" => CreateReluFilter(config.Parameters),
            _ => throw new NotSupportedException($"DSP filter type '{config.Type}' is not supported")
        };
    }

    private static IDspFilter CreateKalmanFilter(Dictionary<string, object> parameters)
    {
        var processNoise = GetDoubleParameter(parameters, "ProcessNoise", 0.01);
        var measurementNoise = GetDoubleParameter(parameters, "MeasurementNoise", 0.1);

        return new KalmanFilter(processNoise, measurementNoise);
    }

    private static IDspFilter CreateMovingAverageFilter(Dictionary<string, object> parameters)
    {
        var window = GetIntParameter(parameters, "Window", 5);

        if (window <= 0)
            throw new ArgumentException("MovingAverage Window parameter must be greater than 0");

        return new MovingAverageFilter(window);
    }

    private static IDspFilter CreateReluFilter(Dictionary<string, object> parameters)
    {
        // ReluFilter has no constructor parameters
        return new ReluFilter();
    }

    private static double GetDoubleParameter(Dictionary<string, object> parameters, string key, double defaultValue)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            return Convert.ToDouble(value);
        }
        return defaultValue;
    }

    private static int GetIntParameter(Dictionary<string, object> parameters, string key, int defaultValue)
    {
        if (parameters.TryGetValue(key, out var value))
        {
            return Convert.ToInt32(value);
        }
        return defaultValue;
    }
}