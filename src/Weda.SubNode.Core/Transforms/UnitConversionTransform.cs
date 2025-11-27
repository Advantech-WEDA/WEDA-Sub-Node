namespace Weda.SubNode.Core.Transforms;

using ErrorOr;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

/// <summary>
/// Unit conversion transform (e.g., Celsius to Fahrenheit)
/// Converts all numeric values in the measure list.
/// </summary>
public class UnitConversionTransform : ITelemetryTransform
{
    private string _fromUnit;
    private string _toUnit;

    public string Name => "UnitConversionTransform";

    /// <inheritdoc/>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Creates unit conversion transform
    /// </summary>
    /// <param name="fromUnit">Source unit (C/celsius, F/fahrenheit, K/kelvin)</param>
    /// <param name="toUnit">Target unit (C/celsius, F/fahrenheit, K/kelvin)</param>
    public UnitConversionTransform(string fromUnit, string toUnit)
    {
        _fromUnit = NormalizeUnit(fromUnit ?? throw new ArgumentNullException(nameof(fromUnit)));
        _toUnit = NormalizeUnit(toUnit ?? throw new ArgumentNullException(nameof(toUnit)));
    }

    /// <inheritdoc/>
    public ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("FromUnit", out var from))
        {
            var fromUnit = from?.ToString();
            if (string.IsNullOrWhiteSpace(fromUnit))
                return Error.Validation("UnitConversionTransform.FromUnit", "FromUnit cannot be empty");
        }

        if (parameters.TryGetValue("ToUnit", out var to))
        {
            var toUnit = to?.ToString();
            if (string.IsNullOrWhiteSpace(toUnit))
                return Error.Validation("UnitConversionTransform.ToUnit", "ToUnit cannot be empty");
        }

        return Result.Success;
    }

    /// <inheritdoc/>
    public void UpdateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("FromUnit", out var from))
            _fromUnit = NormalizeUnit(from?.ToString() ?? _fromUnit);

        if (parameters.TryGetValue("ToUnit", out var to))
            _toUnit = NormalizeUnit(to?.ToString() ?? _toUnit);
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
            if (measure.Value is not double and not int and not float)
                return measure;

            var value = Convert.ToDouble(measure.Value);
            var converted = ConvertUnit(value, _fromUnit, _toUnit);

            return measure with { Value = converted };
        }).ToList();

        return Task.FromResult(transformed);
    }

    /// <summary>
    /// Normalizes unit string to standard abbreviation (C, F, K)
    /// Supports: celsius/C, fahrenheit/F, kelvin/K (case-insensitive)
    /// </summary>
    private static string NormalizeUnit(string unit)
    {
        var normalized = unit.Trim().ToLowerInvariant();
        return normalized switch
        {
            "c" or "celsius" => "C",
            "f" or "fahrenheit" => "F",
            "k" or "kelvin" => "K",
            _ => unit // Return as-is if not recognized
        };
    }

    private static double ConvertUnit(double value, string from, string to)
    {
        // Same unit, no conversion needed
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
            return value;

        // Temperature conversions (units are normalized to C, F, K)
        return (from, to) switch
        {
            ("C", "F") => value * 9.0 / 5.0 + 32.0,
            ("F", "C") => (value - 32.0) * 5.0 / 9.0,
            ("C", "K") => value + 273.15,
            ("K", "C") => value - 273.15,
            ("F", "K") => (value - 32.0) * 5.0 / 9.0 + 273.15,
            ("K", "F") => (value - 273.15) * 9.0 / 5.0 + 32.0,
            _ => value // Unsupported conversion, return as-is
        };
    }
}
