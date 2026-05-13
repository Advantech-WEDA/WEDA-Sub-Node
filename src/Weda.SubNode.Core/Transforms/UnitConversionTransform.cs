namespace Weda.SubNode.Core.Transforms;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

/// <summary>
/// Configuration parameters for <see cref="UnitConversionTransform"/>.
/// Supported units (case-insensitive): C / Celsius, F / Fahrenheit, K / Kelvin.
/// </summary>
public class UnitConversionParameters
{
    [Required]
    [Description("Source unit. Accepted values: C, F, K, celsius, fahrenheit, kelvin.")]
    [JsonPropertyName("fromUnit")]
    public string FromUnit { get; init; } = string.Empty;

    [Required]
    [Description("Target unit. Accepted values: C, F, K, celsius, fahrenheit, kelvin.")]
    [JsonPropertyName("toUnit")]
    public string ToUnit { get; init; } = string.Empty;
}

/// <summary>
/// Unit conversion transform (e.g., Celsius to Fahrenheit). Converts all
/// numeric values in the measure list; non-numeric measures pass through.
/// </summary>
public class UnitConversionTransform
    : ITelemetryTransform,
      IConfigurableTransform<UnitConversionTransform, UnitConversionParameters>
{
    private string _fromUnit;
    private string _toUnit;

    public static string TypeName => "unitconversion";

    public static string? Description =>
        "Convert numeric measurements between temperature units (C/F/K).";

    public static UnitConversionTransform Create(UnitConversionParameters parameters)
    {
        return new UnitConversionTransform(parameters.FromUnit, parameters.ToUnit);
    }

    public string Name => nameof(UnitConversionTransform);

    public bool Enabled { get; set; } = true;

    public UnitConversionTransform(string fromUnit, string toUnit)
    {
        _fromUnit = NormalizeUnit(fromUnit ?? throw new ArgumentNullException(nameof(fromUnit)));
        _toUnit = NormalizeUnit(toUnit ?? throw new ArgumentNullException(nameof(toUnit)));
    }

    public void UpdateParameters(UnitConversionParameters parameters)
    {
        _fromUnit = NormalizeUnit(parameters.FromUnit);
        _toUnit = NormalizeUnit(parameters.ToUnit);
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
            if (measure.Value is null or not double and not int and not float)
            {
                return measure;
            }

            var value = Convert.ToDouble(measure.Value);
            var converted = ConvertUnit(value, _fromUnit, _toUnit);
            return measure with { Value = converted };
        }).ToList();

        return Task.FromResult(transformed);
    }

    private static string NormalizeUnit(string unit)
    {
        var normalized = unit.Trim().ToLowerInvariant();
        return normalized switch
        {
            "c" or "celsius" => "C",
            "f" or "fahrenheit" => "F",
            "k" or "kelvin" => "K",
            _ => unit,
        };
    }

    private static double ConvertUnit(double value, string from, string to)
    {
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return (from, to) switch
        {
            ("C", "F") => value * 9.0 / 5.0 + 32.0,
            ("F", "C") => (value - 32.0) * 5.0 / 9.0,
            ("C", "K") => value + 273.15,
            ("K", "C") => value - 273.15,
            ("F", "K") => (value - 32.0) * 5.0 / 9.0 + 273.15,
            ("K", "F") => (value - 273.15) * 9.0 / 5.0 + 32.0,
            _ => value,
        };
    }
}
