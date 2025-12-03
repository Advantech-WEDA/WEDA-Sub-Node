using Xunit;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Core.Transforms;

namespace Weda.SubNode.Core.Tests.Transforms;

public class CalibrationTransformTests
{
    private static TelemetryMeasure CreateMeasure(double value, string resourceId = "sensor1")
        => new() { ResourceId = resourceId, Value = value };

    private static TelemetryTransformContext CreateContext()
        => new() { DeviceId = "device1", Timestamp = DateTimeOffset.UtcNow };

    [Fact]
    public void ValidateParameters_ValidScale_ReturnsSuccess()
    {
        var transform = new CalibrationTransform();
        var parameters = new Dictionary<string, object>
        {
            ["Scale"] = 1.5,
            ["Offset"] = 10.0
        };

        var result = transform.ValidateParameters(parameters);

        Assert.False(result.IsError);
    }

    [Fact]
    public void ValidateParameters_ScaleZero_ReturnsFailure()
    {
        var transform = new CalibrationTransform();
        var parameters = new Dictionary<string, object>
        {
            ["Scale"] = 0.0
        };

        var result = transform.ValidateParameters(parameters);

        Assert.True(result.IsError);
        Assert.Contains("Scale", result.FirstError.Description);
    }

    [Fact]
    public void ValidateParameters_NoScaleProvided_ReturnsSuccess()
    {
        var transform = new CalibrationTransform();
        var parameters = new Dictionary<string, object>
        {
            ["Offset"] = 5.0
        };

        var result = transform.ValidateParameters(parameters);

        Assert.False(result.IsError);
    }

    [Fact]
    public async Task UpdateParameters_ScaleOffset_Applied()
    {
        var transform = new CalibrationTransform(scale: 1.0, offset: 0.0);

        // Update parameters
        transform.UpdateParameters(new Dictionary<string, object>
        {
            ["Scale"] = 2.0,
            ["Offset"] = 10.0
        });

        // Apply transform: value * 2.0 + 10.0
        var measures = new List<TelemetryMeasure> { CreateMeasure(5.0) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(20.0, (double)result[0].Value!); // 5 * 2 + 10 = 20
    }

    [Fact]
    public async Task Enabled_False_PassThrough()
    {
        var transform = new CalibrationTransform(scale: 2.0, offset: 10.0);
        transform.Enabled = false;

        var measures = new List<TelemetryMeasure>
        {
            CreateMeasure(5.0),
            CreateMeasure(10.0)
        };

        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Equal(2, result.Count);
        Assert.Equal(5.0, result[0].Value);  // Not transformed
        Assert.Equal(10.0, result[1].Value); // Not transformed
    }

    [Fact]
    public async Task Enabled_True_AppliesCalibration()
    {
        var transform = new CalibrationTransform(scale: 2.0, offset: 5.0);

        var measures = new List<TelemetryMeasure>
        {
            CreateMeasure(10.0),
            CreateMeasure(20.0)
        };

        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Equal(2, result.Count);
        Assert.Equal(25.0, (double)result[0].Value!); // 10 * 2 + 5 = 25
        Assert.Equal(45.0, (double)result[1].Value!); // 20 * 2 + 5 = 45
    }

    [Fact]
    public async Task NonNumericValues_PassThrough()
    {
        var transform = new CalibrationTransform(scale: 2.0, offset: 5.0);

        var measure = new TelemetryMeasure
        {
            ResourceId = "sensor1",
            Value = "text"
        };

        var result = await transform.TransformAsync(new List<TelemetryMeasure> { measure }, CreateContext());

        Assert.Single(result);
        Assert.Equal("text", result[0].Value);
    }
}
