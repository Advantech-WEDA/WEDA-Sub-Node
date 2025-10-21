using System.Runtime.CompilerServices;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Dsp;

/// <summary>
/// Simple 1D Kalman filter for telemetry data
/// </summary>
public class KalmanFilter : IDspFilter
{
    private readonly double _processNoise;
    private readonly double _measurementNoise;
    private readonly Dictionary<string, KalmanState> _states = new();

    public KalmanFilter(double processNoise = 0.01, double measurementNoise = 0.1)
    {
        _processNoise = processNoise;
        _measurementNoise = measurementNoise;
    }

    public async IAsyncEnumerable<TelemetryMeasure> ApplyAsync(
        IAsyncEnumerable<TelemetryMeasure> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var measure in input.WithCancellation(cancellationToken))
        {
            // Convert Value to double
            if (measure.Value is not double and not int and not float and not long)
            {
                yield return measure; // Pass through non-numeric values
                continue;
            }

            var value = Convert.ToDouble(measure.Value);

            // Get or create state for this sensor
            if (!_states.ContainsKey(measure.ResourceId))
            {
                _states[measure.ResourceId] = new KalmanState
                {
                    Estimate = value,
                    ErrorCovariance = 1.0
                };
            }

            var state = _states[measure.ResourceId];

            // Prediction
            var predictedEstimate = state.Estimate;
            var predictedErrorCovariance = state.ErrorCovariance + _processNoise;

            // Update
            var kalmanGain = predictedErrorCovariance / (predictedErrorCovariance + _measurementNoise);
            var estimate = predictedEstimate + kalmanGain * (value - predictedEstimate);
            var errorCovariance = (1 - kalmanGain) * predictedErrorCovariance;

            // Save state
            state.Estimate = estimate;
            state.ErrorCovariance = errorCovariance;

            yield return measure with { Value = estimate };
        }
    }

    private class KalmanState
    {
        public double Estimate { get; set; }
        public double ErrorCovariance { get; set; }
    }
}
