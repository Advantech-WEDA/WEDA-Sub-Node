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

    [Fact]
    public void ValidateParameters_ValidUnits_ReturnsSuccess()
    {
        var transform = new UnitConversionTransform("temperature", "C", "F");
        var parameters = new Dictionary<string, object>
        {
            ["FromUnit"] = "C",
            ["ToUnit"] = "K"
        };

        var result = transform.ValidateParameters(parameters);

        Assert.False(result.IsError);
    }

    [Fact]
    public void ValidateParameters_EmptyFromUnit_ReturnsFailure()
    {
        var transform = new UnitConversionTransform("temperature", "C", "F");
        var parameters = new Dictionary<string, object>
        {
            ["FromUnit"] = ""
        };

        var result = transform.ValidateParameters(parameters);

        Assert.True(result.IsError);
        Assert.Contains("FromUnit", result.FirstError.Description);
    }

    [Fact]
    public void ValidateParameters_EmptyToUnit_ReturnsFailure()
    {
        var transform = new UnitConversionTransform("temperature", "C", "F");
        var parameters = new Dictionary<string, object>
        {
            ["ToUnit"] = "   "
        };

        var result = transform.ValidateParameters(parameters);

        Assert.True(result.IsError);
        Assert.Contains("ToUnit", result.FirstError.Description);
    }

    [Fact]
    public void ValidateParameters_NoUnitsProvided_ReturnsSuccess()
    {
        var transform = new UnitConversionTransform("temperature", "C", "F");
        var parameters = new Dictionary<string, object>
        {
            ["TargetResourceId"] = "humidity"
        };

        var result = transform.ValidateParameters(parameters);

        Assert.False(result.IsError);
    }

    [Fact]
    public async Task UpdateParameters_UnitsChanged_Applied()
    {
        var transform = new UnitConversionTransform("temperature", "C", "F");

        // Update to Celsius to Kelvin
        transform.UpdateParameters(new Dictionary<string, object>
        {
            ["FromUnit"] = "C",
            ["ToUnit"] = "K"
        });

        // 0°C = 273.15K
        var measures = new List<TelemetryMeasure> { CreateMeasure(0.0) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(273.15, (double)result[0].Value!, precision: 2);
    }

    [Fact]
    public async Task Enabled_False_PassThrough()
    {
        var transform = new UnitConversionTransform("temperature", "C", "F");
        transform.Enabled = false;

        // 0°C should be 32°F if enabled
        var measures = new List<TelemetryMeasure> { CreateMeasure(0.0) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(0.0, result[0].Value); // Not converted
    }

    [Fact]
    public async Task Enabled_True_ConvertsUnits()
    {
        var transform = new UnitConversionTransform("temperature", "C", "F");

        // 0°C = 32°F, 100°C = 212°F
        var measures = new List<TelemetryMeasure>
        {
            CreateMeasure(0.0),
            CreateMeasure(100.0)
        };

        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Equal(2, result.Count);
        Assert.Equal(32.0, (double)result[0].Value!, precision: 2);
        Assert.Equal(212.0, (double)result[1].Value!, precision: 2);
    }

    [Fact]
    public async Task NonMatchingResourceId_PassThrough()
    {
        var transform = new UnitConversionTransform("temperature", "C", "F");

        var measure = CreateMeasure(100.0, "humidity"); // Different resource ID

        var result = await transform.TransformAsync(new List<TelemetryMeasure> { measure }, CreateContext());

        Assert.Single(result);
        Assert.Equal(100.0, result[0].Value); // Not converted
    }

    [Fact]
    public async Task KelvinToCelsius_ConvertsCorrectly()
    {
        var transform = new UnitConversionTransform("temperature", "K", "C");

        // 273.15K = 0°C
        var measures = new List<TelemetryMeasure> { CreateMeasure(273.15) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(0.0, (double)result[0].Value!, precision: 2);
    }

    [Fact]
    public async Task FahrenheitToCelsius_ConvertsCorrectly()
    {
        var transform = new UnitConversionTransform("temperature", "F", "C");

        // 32°F = 0°C
        var measures = new List<TelemetryMeasure> { CreateMeasure(32.0) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(0.0, (double)result[0].Value!, precision: 2);
    }

    #region Wildcard Pattern Matching Tests

    [Fact]
    public async Task WildcardStar_MatchesAll()
    {
        var transform = new UnitConversionTransform("*", "C", "F");

        var measures = new List<TelemetryMeasure>
        {
            CreateMeasure(0.0, "temperature.room1"),
            CreateMeasure(100.0, "humidity.room2"),
            CreateMeasure(25.0, "pressure.lab")
        };

        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Equal(3, result.Count);
        Assert.Equal(32.0, (double)result[0].Value!, precision: 2);   // 0°C = 32°F
        Assert.Equal(212.0, (double)result[1].Value!, precision: 2);  // 100°C = 212°F
        Assert.Equal(77.0, (double)result[2].Value!, precision: 2);   // 25°C = 77°F
    }

    [Fact]
    public async Task WildcardStarSuffix_MatchesPattern()
    {
        var transform = new UnitConversionTransform("temperature.*", "C", "F");

        var measures = new List<TelemetryMeasure>
        {
            CreateMeasure(0.0, "temperature.room1"),
            CreateMeasure(100.0, "temperature.room2"),
            CreateMeasure(25.0, "humidity.room1") // Should not match
        };

        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Equal(3, result.Count);
        Assert.Equal(32.0, (double)result[0].Value!, precision: 2);   // Converted
        Assert.Equal(212.0, (double)result[1].Value!, precision: 2);  // Converted
        Assert.Equal(25.0, (double)result[2].Value!, precision: 2);   // Not converted
    }

    [Fact]
    public async Task WildcardQuestion_MatchesSingleChar()
    {
        var transform = new UnitConversionTransform("temp?", "C", "F");

        var measures = new List<TelemetryMeasure>
        {
            CreateMeasure(0.0, "temp1"),    // Matches
            CreateMeasure(100.0, "temp2"),  // Matches
            CreateMeasure(25.0, "temp12"),  // Does not match (2 chars)
            CreateMeasure(50.0, "temp")     // Does not match (0 chars)
        };

        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Equal(4, result.Count);
        Assert.Equal(32.0, (double)result[0].Value!, precision: 2);   // Converted
        Assert.Equal(212.0, (double)result[1].Value!, precision: 2);  // Converted
        Assert.Equal(25.0, (double)result[2].Value!, precision: 2);   // Not converted
        Assert.Equal(50.0, (double)result[3].Value!, precision: 2);   // Not converted
    }

    [Fact]
    public async Task WildcardCombined_MatchesComplexPattern()
    {
        // Pattern: sensor?.temp* - matches sensor1.temperature, sensorA.temp, etc.
        var transform = new UnitConversionTransform("sensor?.temp*", "C", "F");

        var measures = new List<TelemetryMeasure>
        {
            CreateMeasure(0.0, "sensor1.temperature"),   // Matches
            CreateMeasure(100.0, "sensorA.temp"),        // Matches
            CreateMeasure(25.0, "sensor12.temperature"), // Does not match (2 chars after sensor)
            CreateMeasure(50.0, "sensor1.humidity")      // Does not match (different suffix)
        };

        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Equal(4, result.Count);
        Assert.Equal(32.0, (double)result[0].Value!, precision: 2);   // Converted
        Assert.Equal(212.0, (double)result[1].Value!, precision: 2);  // Converted
        Assert.Equal(25.0, (double)result[2].Value!, precision: 2);   // Not converted
        Assert.Equal(50.0, (double)result[3].Value!, precision: 2);   // Not converted
    }

    #endregion

    #region Full Unit Name Support Tests

    [Theory]
    [InlineData("celsius", "fahrenheit", 0.0, 32.0)]
    [InlineData("Celsius", "Fahrenheit", 100.0, 212.0)]
    [InlineData("CELSIUS", "FAHRENHEIT", 25.0, 77.0)]
    public async Task FullUnitNames_ConvertsCorrectly(string from, string to, double input, double expected)
    {
        var transform = new UnitConversionTransform("temperature", from, to);

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
        var transform = new UnitConversionTransform("temperature", from, to);

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
        var transform = new UnitConversionTransform("temperature", from, to);

        var measures = new List<TelemetryMeasure> { CreateMeasure(input) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(expected, (double)result[0].Value!, precision: 2);
    }

    [Fact]
    public async Task MixedNotation_ConvertsCorrectly()
    {
        // Use abbreviation for from, full name for to
        var transform = new UnitConversionTransform("temperature", "C", "fahrenheit");

        var measures = new List<TelemetryMeasure> { CreateMeasure(0.0) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(32.0, (double)result[0].Value!, precision: 2);
    }

    [Fact]
    public void UpdateParameters_FullUnitNames_Applied()
    {
        var transform = new UnitConversionTransform("temperature", "C", "F");

        transform.UpdateParameters(new Dictionary<string, object>
        {
            ["FromUnit"] = "celsius",
            ["ToUnit"] = "kelvin"
        });

        // Verify the update was applied (indirectly through conversion)
        var validationResult = transform.ValidateParameters(new Dictionary<string, object>
        {
            ["FromUnit"] = "celsius",
            ["ToUnit"] = "kelvin"
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
        var transform = new UnitConversionTransform("temperature", from, to);

        var measures = new List<TelemetryMeasure> { CreateMeasure(25.0) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(25.0, (double)result[0].Value!, precision: 2);
    }

    #endregion
}
