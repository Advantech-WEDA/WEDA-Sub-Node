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

    private static IConfigurableTransform<CalibrationTransform, CalibrationParameters> AsConfigurable(CalibrationTransform t) => t;

    [Fact]
    public void ValidateParameters_ValidScale_ReturnsSuccess()
    {
        var transform = new CalibrationTransform();
        var result = AsConfigurable(transform).ValidateParameters(new CalibrationParameters
        {
            Scale = 1.5,
            Offset = 10.0,
        });
        Assert.False(result.IsError);
    }

    [Fact]
    public void ValidateParameters_ScaleZero_ReturnsFailure()
    {
        var transform = new CalibrationTransform();
        var result = AsConfigurable(transform).ValidateParameters(new CalibrationParameters
        {
            Scale = 0.0,
        });
        Assert.True(result.IsError);
        Assert.Contains("Scale", result.FirstError.Code);
    }

    [Fact]
    public void ValidateParameters_DefaultScaleAndOffsetOnly_ReturnsSuccess()
    {
        var transform = new CalibrationTransform();
        var result = AsConfigurable(transform).ValidateParameters(new CalibrationParameters
        {
            Offset = 5.0,
        });
        Assert.False(result.IsError);
    }

    [Fact]
    public async Task UpdateParameters_ScaleOffset_Applied()
    {
        var transform = new CalibrationTransform(scale: 1.0, offset: 0.0);

        transform.UpdateParameters(new CalibrationParameters
        {
            Scale = 2.0,
            Offset = 10.0,
        });

        var measures = new List<TelemetryMeasure> { CreateMeasure(5.0) };
        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Single(result);
        Assert.Equal(20.0, (double)result[0].Value!);
    }

    [Fact]
    public async Task Enabled_False_PassThrough()
    {
        var transform = new CalibrationTransform(scale: 2.0, offset: 10.0);
        transform.Enabled = false;

        var measures = new List<TelemetryMeasure>
        {
            CreateMeasure(5.0),
            CreateMeasure(10.0),
        };

        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Equal(2, result.Count);
        Assert.Equal(5.0, result[0].Value);
        Assert.Equal(10.0, result[1].Value);
    }

    [Fact]
    public async Task Enabled_True_AppliesCalibration()
    {
        var transform = new CalibrationTransform(scale: 2.0, offset: 5.0);

        var measures = new List<TelemetryMeasure>
        {
            CreateMeasure(10.0),
            CreateMeasure(20.0),
        };

        var result = await transform.TransformAsync(measures, CreateContext());

        Assert.Equal(2, result.Count);
        Assert.Equal(25.0, (double)result[0].Value!);
        Assert.Equal(45.0, (double)result[1].Value!);
    }

    [Fact]
    public async Task NonNumericValues_PassThrough()
    {
        var transform = new CalibrationTransform(scale: 2.0, offset: 5.0);

        var measure = new TelemetryMeasure
        {
            ResourceId = "sensor1",
            Value = "text",
        };

        var result = await transform.TransformAsync(new List<TelemetryMeasure> { measure }, CreateContext());

        Assert.Single(result);
        Assert.Equal("text", result[0].Value);
    }
}
