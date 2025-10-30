namespace Weda.SubNode.Core.Transforms;

using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

/// <summary>
/// Unit conversion transform (e.g., Celsius to Fahrenheit)
/// </summary>
public class UnitConversionTransform : ITelemetryTransform
{
    private readonly string _targetResourceId;
    private readonly string _fromUnit;
    private readonly string _toUnit;

    public string Name => "UnitConversionTransform";

    /// <summary>
    /// Creates unit conversion transform
    /// </summary>
    /// <param name="targetResourceId">Target resource ID</param>
    /// <param name="fromUnit">Source unit</param>
    /// <param name="toUnit">Target unit</param>
    public UnitConversionTransform(string targetResourceId, string fromUnit, string toUnit)
    {
        _targetResourceId = targetResourceId ?? throw new ArgumentNullException(nameof(targetResourceId));
        _fromUnit = fromUnit ?? throw new ArgumentNullException(nameof(fromUnit));
        _toUnit = toUnit ?? throw new ArgumentNullException(nameof(toUnit));
    }

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        var transformed = measures.Select(measure =>
        {
            if (!measure.ResourceId.Contains(_targetResourceId))
                return measure;

            if (measure.Value is not double and not int and not float)
                return measure;

            var value = Convert.ToDouble(measure.Value);
            var converted = ConvertUnit(value, _fromUnit, _toUnit);

            return measure with { Value = converted };
        }).ToList();

        return Task.FromResult(transformed);
    }

    private double ConvertUnit(double value, string from, string to)
    {
        // Temperature conversions
        if (from.Equals("C", StringComparison.OrdinalIgnoreCase) &&
            to.Equals("F", StringComparison.OrdinalIgnoreCase))
        {
            return value * 9.0 / 5.0 + 32.0;
        }

        if (from.Equals("F", StringComparison.OrdinalIgnoreCase) &&
            to.Equals("C", StringComparison.OrdinalIgnoreCase))
        {
            return (value - 32.0) * 5.0 / 9.0;
        }

        if (from.Equals("C", StringComparison.OrdinalIgnoreCase) &&
            to.Equals("K", StringComparison.OrdinalIgnoreCase))
        {
            return value + 273.15;
        }

        if (from.Equals("K", StringComparison.OrdinalIgnoreCase) &&
            to.Equals("C", StringComparison.OrdinalIgnoreCase))
        {
            return value - 273.15;
        }

        // Add more conversions as needed
        return value;
    }
}
