using Xunit;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Dsp;

namespace Weda.SubNode.Core.Tests.Dsp;

public class ReluFilterTests
{
    private static IConfigurableDspFilter<ReluFilter, ReluParameters> AsConfigurable(ReluFilter f) => f;

    private static TelemetryMeasure CreateMeasure(double value, string resourceId = "sensor1")
        => new() { ResourceId = resourceId, Value = value };

    private static async IAsyncEnumerable<TelemetryMeasure> ToAsyncEnumerable(params TelemetryMeasure[] measures)
    {
        foreach (var m in measures)
            yield return m;
        await Task.CompletedTask;
    }

    private static async Task<List<TelemetryMeasure>> ToListAsync(IAsyncEnumerable<TelemetryMeasure> source)
    {
        var result = new List<TelemetryMeasure>();
        await foreach (var item in source)
            result.Add(item);
        return result;
    }

    [Fact]
    public void ValidateParameters_EmptyParams_ReturnsSuccess()
    {
        var filter = new ReluFilter();
        var result = AsConfigurable(filter).ValidateParameters(new ReluParameters());
        Assert.False(result.IsError);
    }

    [Fact]
    public void UpdateParameters_NoEffect()
    {
        var filter = new ReluFilter();
        // Should not throw
        filter.UpdateParameters(new ReluParameters());
    }

    [Fact]
    public async Task Enabled_False_PassThrough()
    {
        var filter = new ReluFilter();
        filter.Enabled = false;

        var input = ToAsyncEnumerable(
            CreateMeasure(10.0),
            CreateMeasure(-5.0),
            CreateMeasure(-100.0));

        var result = await ToListAsync(filter.ApplyAsync(input));

        Assert.Equal(3, result.Count);
        Assert.Equal(10.0, result[0].Value);
        Assert.Equal(-5.0, result[1].Value);   // Negative values pass through when disabled
        Assert.Equal(-100.0, result[2].Value);
    }

    [Fact]
    public async Task Enabled_True_ClampsNegativeToZero()
    {
        var filter = new ReluFilter();

        var input = ToAsyncEnumerable(
            CreateMeasure(10.0),
            CreateMeasure(-5.0),
            CreateMeasure(0.0),
            CreateMeasure(-100.0));

        var result = await ToListAsync(filter.ApplyAsync(input));

        Assert.Equal(4, result.Count);
        Assert.Equal(10.0, result[0].Value);
        Assert.Equal(0.0, result[1].Value);   // Clamped from -5.0
        Assert.Equal(0.0, result[2].Value);
        Assert.Equal(0.0, result[3].Value);   // Clamped from -100.0
    }

    [Fact]
    public async Task NonNumericValues_PassThrough()
    {
        var filter = new ReluFilter();

        var measure = new TelemetryMeasure
        {
            ResourceId = "sensor1",
            Value = "text"
        };

        var input = ToAsyncEnumerable(measure);
        var result = await ToListAsync(filter.ApplyAsync(input));

        Assert.Single(result);
        Assert.Equal("text", result[0].Value);
    }
}
