using Xunit;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Dsp;

namespace Weda.SubNode.Core.Tests.Dsp;

public class KalmanFilterTests
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

    [Fact]
    public void ValidateParameters_ValidParams_ReturnsSuccess()
    {
        var filter = new KalmanFilter();
        var parameters = new Dictionary<string, object>
        {
            ["ProcessNoise"] = 0.05,
            ["MeasurementNoise"] = 0.2
        };

        var result = filter.ValidateParameters(parameters);

        Assert.False(result.IsError);
    }

    [Fact]
    public void ValidateParameters_ZeroProcessNoise_ReturnsSuccess()
    {
        var filter = new KalmanFilter();
        var parameters = new Dictionary<string, object>
        {
            ["ProcessNoise"] = 0.0
        };

        var result = filter.ValidateParameters(parameters);

        Assert.False(result.IsError);
    }

    [Fact]
    public void ValidateParameters_NegativeProcessNoise_ReturnsFailure()
    {
        var filter = new KalmanFilter();
        var parameters = new Dictionary<string, object>
        {
            ["ProcessNoise"] = -0.1
        };

        var result = filter.ValidateParameters(parameters);

        Assert.True(result.IsError);
        Assert.Contains("ProcessNoise", result.FirstError.Description);
    }

    [Fact]
    public void ValidateParameters_ZeroMeasurementNoise_ReturnsFailure()
    {
        var filter = new KalmanFilter();
        var parameters = new Dictionary<string, object>
        {
            ["MeasurementNoise"] = 0.0
        };

        var result = filter.ValidateParameters(parameters);

        Assert.True(result.IsError);
        Assert.Contains("MeasurementNoise", result.FirstError.Description);
    }

    [Fact]
    public void ValidateParameters_NegativeMeasurementNoise_ReturnsFailure()
    {
        var filter = new KalmanFilter();
        var parameters = new Dictionary<string, object>
        {
            ["MeasurementNoise"] = -0.5
        };

        var result = filter.ValidateParameters(parameters);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task UpdateParameters_ChangesApplied_StatePreserved()
    {
        var filter = new KalmanFilter(processNoise: 0.01, measurementNoise: 0.1);

        // Process some values to build state
        var input1 = ToAsyncEnumerable(
            CreateMeasure(10.0),
            CreateMeasure(10.5),
            CreateMeasure(9.8));
        var result1 = await ToListAsync(filter.ApplyAsync(input1));

        // Get state estimate after processing
        var lastEstimate = (double)result1[^1].Value!;

        // Update parameters
        filter.UpdateParameters(new Dictionary<string, object>
        {
            ["ProcessNoise"] = 0.05,
            ["MeasurementNoise"] = 0.2
        });

        // Process another value - state should be preserved
        var input2 = ToAsyncEnumerable(CreateMeasure(10.2));
        var result2 = await ToListAsync(filter.ApplyAsync(input2));

        // The new estimate should be based on previous state, not starting fresh
        var newEstimate = (double)result2[0].Value!;

        // If state was reset, estimate would be exactly 10.2 (first value)
        // Since state is preserved, it should be different
        Assert.NotEqual(10.2, newEstimate, precision: 5);
    }

    [Fact]
    public async Task Enabled_False_PassThrough()
    {
        var filter = new KalmanFilter();
        filter.Enabled = false;

        var input = ToAsyncEnumerable(
            CreateMeasure(10.0),
            CreateMeasure(-5.0),
            CreateMeasure(100.0));

        var result = await ToListAsync(filter.ApplyAsync(input));

        Assert.Equal(3, result.Count);
        Assert.Equal(10.0, result[0].Value);
        Assert.Equal(-5.0, result[1].Value);
        Assert.Equal(100.0, result[2].Value);
    }

    [Fact]
    public async Task Enabled_True_FiltersValues()
    {
        var filter = new KalmanFilter(processNoise: 0.01, measurementNoise: 0.1);

        var input = ToAsyncEnumerable(
            CreateMeasure(10.0),
            CreateMeasure(10.0),
            CreateMeasure(10.0));

        var result = await ToListAsync(filter.ApplyAsync(input));

        Assert.Equal(3, result.Count);
        // Kalman filter should smooth values
        foreach (var r in result)
            Assert.NotNull(r.Value);
    }
}
