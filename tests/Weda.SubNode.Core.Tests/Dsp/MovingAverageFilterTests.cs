using Xunit;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Dsp;

namespace Weda.SubNode.Core.Tests.Dsp;

public class MovingAverageFilterTests
{
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

    private static IConfigurableDspFilter<MovingAverageFilter, MovingAverageParameters> AsConfigurable(MovingAverageFilter f) => f;

    [Fact]
    public void ValidateParameters_ValidWindow_ReturnsSuccess()
    {
        var filter = new MovingAverageFilter(5);
        var result = AsConfigurable(filter).ValidateParameters(new MovingAverageParameters { Window = 10 });
        Assert.False(result.IsError);
    }

    [Fact]
    public void ValidateParameters_WindowZero_ReturnsFailure()
    {
        var filter = new MovingAverageFilter(5);
        var result = AsConfigurable(filter).ValidateParameters(new MovingAverageParameters { Window = 0 });
        Assert.True(result.IsError);
        Assert.Contains("Window", result.FirstError.Code);
    }

    [Fact]
    public void ValidateParameters_WindowNegative_ReturnsFailure()
    {
        var filter = new MovingAverageFilter(5);
        var result = AsConfigurable(filter).ValidateParameters(new MovingAverageParameters { Window = -5 });
        Assert.True(result.IsError);
        Assert.Contains("Window", result.FirstError.Code);
    }

    [Fact]
    public async Task UpdateParameters_WindowChanged_BufferReset()
    {
        var filter = new MovingAverageFilter(3);

        var input1 = ToAsyncEnumerable(
            CreateMeasure(10.0),
            CreateMeasure(20.0),
            CreateMeasure(30.0));
        var result1 = await ToListAsync(filter.ApplyAsync(input1));
        Assert.Equal(20.0, (double)result1[^1].Value!, precision: 5);

        filter.UpdateParameters(new MovingAverageParameters { Window = 2 });

        var input2 = ToAsyncEnumerable(
            CreateMeasure(100.0),
            CreateMeasure(200.0));
        var result2 = await ToListAsync(filter.ApplyAsync(input2));
        Assert.Equal(150.0, (double)result2[^1].Value!, precision: 5);
    }

    [Fact]
    public async Task Enabled_False_PassThrough()
    {
        var filter = new MovingAverageFilter(5);
        filter.Enabled = false;

        var input = ToAsyncEnumerable(
            CreateMeasure(10.0),
            CreateMeasure(20.0),
            CreateMeasure(30.0));

        var result = await ToListAsync(filter.ApplyAsync(input));

        Assert.Equal(3, result.Count);
        Assert.Equal(10.0, result[0].Value);
        Assert.Equal(20.0, result[1].Value);
        Assert.Equal(30.0, result[2].Value);
    }

    [Fact]
    public async Task Enabled_True_AveragesValues()
    {
        var filter = new MovingAverageFilter(3);

        var input = ToAsyncEnumerable(
            CreateMeasure(10.0),
            CreateMeasure(20.0),
            CreateMeasure(30.0));

        var result = await ToListAsync(filter.ApplyAsync(input));

        Assert.Equal(3, result.Count);
        Assert.Equal(10.0, (double)result[0].Value!, precision: 5);
        Assert.Equal(15.0, (double)result[1].Value!, precision: 5);
        Assert.Equal(20.0, (double)result[2].Value!, precision: 5);
    }

    [Fact]
    public void Constructor_WindowZero_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new MovingAverageFilter(0));
    }

    [Fact]
    public void Constructor_WindowNegative_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => new MovingAverageFilter(-1));
    }
}
