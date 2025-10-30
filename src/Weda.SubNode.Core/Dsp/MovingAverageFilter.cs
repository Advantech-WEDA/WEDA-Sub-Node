using System.Runtime.CompilerServices;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Dsp;

/// <summary>
/// Moving average DSP filter with O(1) time complexity using circular buffer
/// </summary>
public class MovingAverageFilter : IDspFilter
{
    private readonly int _window;
    private readonly Dictionary<string, CircularBuffer> _buffers = new();

    public MovingAverageFilter(int window)
    {
        if (window <= 0)
            throw new ArgumentException("Window size must be greater than 0", nameof(window));
        _window = window;
    }

    public async IAsyncEnumerable<TelemetryMeasure> ApplyAsync(
        IAsyncEnumerable<TelemetryMeasure> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var measure in input.WithCancellation(cancellationToken))
        {
            // Get or create buffer for this sensor
            if (!_buffers.ContainsKey(measure.ResourceId))
            {
                _buffers[measure.ResourceId] = new CircularBuffer(_window);
            }

            var buffer = _buffers[measure.ResourceId];

            // Convert Value to double
            if (measure.Value is not double and not int and not float and not long)
            {
                yield return measure; // Pass through non-numeric values
                continue;
            }

            var value = Convert.ToDouble(measure.Value);

            // Update buffer and get average in O(1)
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
            // If buffer is full, subtract the value being overwritten
            if (_count == _buffer.Length)
            {
                _sum -= _buffer[_index];
            }
            else
            {
                _count++;
            }

            // Add new value
            _buffer[_index] = value;
            _sum += value;

            // Move to next position
            _index = (_index + 1) % _buffer.Length;

            // Return average
            return _sum / _count;
        }
    }
}
