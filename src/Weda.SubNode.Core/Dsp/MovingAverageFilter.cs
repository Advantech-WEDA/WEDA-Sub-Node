using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Dsp;

/// <summary>
/// Configuration parameters for <see cref="MovingAverageFilter"/>.
/// </summary>
public class MovingAverageParameters
{
    [Description("Sliding window size (number of samples). Must be greater than 0.")]
    [JsonPropertyName("window")]
    [Range(1, int.MaxValue)]
    [DefaultValue(5)]
    public int Window { get; init; } = 5;
}

/// <summary>
/// Moving average DSP filter with O(1) time complexity using a circular buffer.
/// </summary>
public class MovingAverageFilter
    : IDspFilter,
      IConfigurableDspFilter<MovingAverageFilter, MovingAverageParameters>
{
    private int _window;
    private Dictionary<string, CircularBuffer> _buffers = new();

    public static string TypeName => "movingaverage";

    public static string? Description =>
        "Smooth numeric measurements with a sliding-window arithmetic mean.";

    public static MovingAverageFilter Create(MovingAverageParameters parameters) =>
        new(parameters.Window);

    public MovingAverageFilter(int window)
    {
        if (window <= 0)
        {
            throw new ArgumentException("Window size must be greater than 0", nameof(window));
        }
        _window = window;
    }

    public bool Enabled { get; set; } = true;

    public void UpdateParameters(MovingAverageParameters parameters)
    {
        if (parameters.Window != _window)
        {
            _window = parameters.Window;
            // Reset buffers when window size changes (warm-up required)
            _buffers = new Dictionary<string, CircularBuffer>();
        }
    }

    public async IAsyncEnumerable<TelemetryMeasure> ApplyAsync(
        IAsyncEnumerable<TelemetryMeasure> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var measure in input.WithCancellation(cancellationToken))
        {
            if (!Enabled)
            {
                yield return measure;
                continue;
            }

            if (!_buffers.ContainsKey(measure.ResourceId))
            {
                _buffers[measure.ResourceId] = new CircularBuffer(_window);
            }

            var buffer = _buffers[measure.ResourceId];

            if (measure.Value is not double and not int and not float and not long)
            {
                yield return measure;
                continue;
            }

            var value = Convert.ToDouble(measure.Value);
            var avg = buffer.Update(value);

            yield return measure with { Value = avg };
        }
    }

    private class CircularBuffer
    {
        private readonly double[] _buffer;
        private int _index;
        private int _count;
        private double _sum;

        public CircularBuffer(int size)
        {
            _buffer = new double[size];
        }

        public double Update(double value)
        {
            if (_count == _buffer.Length)
            {
                _sum -= _buffer[_index];
            }
            else
            {
                _count++;
            }

            _buffer[_index] = value;
            _sum += value;
            _index = (_index + 1) % _buffer.Length;

            return _sum / _count;
        }
    }
}
