using Xunit;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Core.Transforms;

namespace Weda.SubNode.Core.Tests.Transforms;

public class UnitConversionTransformTests
{
    private static TelemetryMeasure CreateMeasure(double value, string resourceId = "temperature")
        => new() { ResourceId = resourceId, Value = value };

    private static TelemetryTransformContext CreateContext()
        => new() { DeviceId = "device1", Timestamp = DateTimeOffset.UtcNow };

    private static IConfigurableTransform<UnitConversionTransform, UnitConversionParameters> AsConfigurable(UnitConversionTransform t) => t;

    [Fact]
    public void ValidateParameters_ValidUnits_ReturnsSuccess()
    {
        var transform = new UnitConversionTransform("C", "F");
        var result = AsConfigurable(transform).ValidateParameters(new UnitConversionParameters
        {
            FromUnit = "C",
            ToUnit = "K",
        });
        Assert.False(result.IsError);
    }

    [Fact]
    public void ValidateParameters_EmptyFromUnit_ReturnsFailure()
    {
        var transform = new UnitConversionTransform("C", "F");
        var result = AsConfigurable(transform).ValidateParameters(new UnitConversionParameters
        {
            FromUnit = "",
            ToUnit = "F",
        });
        Assert.True(result.IsError);
        Assert.Contains("FromUnit", result.FirstError.Code);
    }

    [Fact]
    public void ValidateParameters_EmptyToUnit_ReturnsFailure()
    {
        var transform = new UnitConversionTransform("C", "F");
        var result = AsConfigurable(transform).ValidateParameters(new UnitConversionParameters
        {
            FromUnit = "C",
            ToUnit = "",
        });
        Assert.True(result.IsError);
        Assert.Contains("ToUnit", result.FirstError.Code);
    }

    [Fact]
    public async Task UpdateParameters_UnitsChanged_Applied()
    {
        var transform = new UnitConversionTransform("C", "F");

        transform.UpdateParameters(new UnitConversionParameters
        {
            FromUnit = "C",
            ToUnit = "K",
        });

        var measures = new List<TelemetryMeasure> { CreateMeasure(0.0) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(273.15, (double)result[0].Value!, precision: 2);
    }

    [Fact]
    public async Task Enabled_False_PassThrough()
    {
        var transform = new UnitConversionTransform("C", "F");
        transform.Enabled = false;

        var measures = new List<TelemetryMeasure> { CreateMeasure(0.0) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(0.0, result[0].Value);
    }

    [Fact]
    public async Task Enabled_True_ConvertsUnits()
    {
        var transform = new UnitConversionTransform("C", "F");

        var measures = new List<TelemetryMeasure>
        {
            CreateMeasure(0.0),
            CreateMeasure(100.0),
        };

        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Equal(2, result.Count);
        Assert.Equal(32.0, (double)result[0].Value!, precision: 2);
        Assert.Equal(212.0, (double)result[1].Value!, precision: 2);
    }

    [Fact]
    public async Task KelvinToCelsius_ConvertsCorrectly()
    {
        var transform = new UnitConversionTransform("K", "C");

        var measures = new List<TelemetryMeasure> { CreateMeasure(273.15) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(0.0, (double)result[0].Value!, precision: 2);
    }

    [Fact]
    public async Task FahrenheitToCelsius_ConvertsCorrectly()
    {
        var transform = new UnitConversionTransform("F", "C");

        var measures = new List<TelemetryMeasure> { CreateMeasure(32.0) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(0.0, (double)result[0].Value!, precision: 2);
    }

    #region Full Unit Name Support Tests

    [Theory]
    [InlineData("celsius", "fahrenheit", 0.0, 32.0)]
    [InlineData("Celsius", "Fahrenheit", 100.0, 212.0)]
    [InlineData("CELSIUS", "FAHRENHEIT", 25.0, 77.0)]
    public async Task FullUnitNames_ConvertsCorrectly(string from, string to, double input, double expected)
    {
        var transform = new UnitConversionTransform(from, to);

        var measures = new List<TelemetryMeasure> { CreateMeasure(input) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(expected, (double)result[0].Value!, precision: 2);
    }

    [Theory]
    [InlineData("celsius", "kelvin", 0.0, 273.15)]
    [InlineData("kelvin", "celsius", 273.15, 0.0)]
    public async Task FullUnitNames_KelvinConversion(string from, string to, double input, double expected)
    {
        var transform = new UnitConversionTransform(from, to);

        var measures = new List<TelemetryMeasure> { CreateMeasure(input) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(expected, (double)result[0].Value!, precision: 2);
    }

    [Theory]
    [InlineData("fahrenheit", "kelvin", 32.0, 273.15)]
    [InlineData("kelvin", "fahrenheit", 273.15, 32.0)]
    public async Task FahrenheitKelvin_ConvertsCorrectly(string from, string to, double input, double expected)
    {
        var transform = new UnitConversionTransform(from, to);

        var measures = new List<TelemetryMeasure> { CreateMeasure(input) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(expected, (double)result[0].Value!, precision: 2);
    }

    [Fact]
    public async Task MixedNotation_ConvertsCorrectly()
    {
        var transform = new UnitConversionTransform("C", "fahrenheit");

        var measures = new List<TelemetryMeasure> { CreateMeasure(0.0) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(32.0, (double)result[0].Value!, precision: 2);
    }

    [Fact]
    public void UpdateParameters_FullUnitNames_Applied()
    {
        var transform = new UnitConversionTransform("C", "F");

        transform.UpdateParameters(new UnitConversionParameters
        {
            FromUnit = "celsius",
            ToUnit = "kelvin",
        });

        var validationResult = AsConfigurable(transform).ValidateParameters(new UnitConversionParameters
        {
            FromUnit = "celsius",
            ToUnit = "kelvin",
        });
        Assert.False(validationResult.IsError);
    }

    #endregion

    #region Same Unit (Pass-through) Tests

    [Theory]
    [InlineData("C", "C")]
    [InlineData("celsius", "celsius")]
    [InlineData("C", "celsius")]
    public async Task SameUnit_ReturnsOriginalValue(string from, string to)
    {
        var transform = new UnitConversionTransform(from, to);

        var measures = new List<TelemetryMeasure> { CreateMeasure(25.0) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(25.0, (double)result[0].Value!, precision: 2);
    }

    #endregion
}
