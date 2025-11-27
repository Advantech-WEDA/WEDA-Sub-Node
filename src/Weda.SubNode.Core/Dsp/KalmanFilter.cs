using System.Runtime.CompilerServices;
using ErrorOr;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Dsp;

/// <summary>
/// Simple 1D Kalman filter for telemetry data
/// </summary>
public class KalmanFilter : IDspFilter
{
    private double _processNoise;
    private double _measurementNoise;
    private readonly Dictionary<string, KalmanState> _states = new();

    public KalmanFilter(double processNoise = 0.01, double measurementNoise = 0.1)
    {
        _processNoise = processNoise;
        _measurementNoise = measurementNoise;
    }

    /// <inheritdoc/>
    public bool Enabled { get; set; } = true;

    /// <inheritdoc/>
    public ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("ProcessNoise", out var pn))
        {
            var processNoise = Convert.ToDouble(pn);
            if (processNoise < 0)
                return Error.Validation("KalmanFilter.ProcessNoise", "ProcessNoise must be >= 0");
        }

        if (parameters.TryGetValue("MeasurementNoise", out var mn))
        {
            var measurementNoise = Convert.ToDouble(mn);
            if (measurementNoise <= 0)
                return Error.Validation("KalmanFilter.MeasurementNoise", "MeasurementNoise must be > 0");
        }

        return Result.Success;
    }

    /// <inheritdoc/>
    public void UpdateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("ProcessNoise", out var pn))
            _processNoise = Convert.ToDouble(pn);

        if (parameters.TryGetValue("MeasurementNoise", out var mn))
            _measurementNoise = Convert.ToDouble(mn);

        // Note: _states is preserved, no warm-up needed
    }

    public async IAsyncEnumerable<TelemetryMeasure> ApplyAsync(
        IAsyncEnumerable<TelemetryMeasure> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var measure in input.WithCancellation(cancellationToken))
        {
            // Pass through if disabled
            if (!Enabled)
            {
                yield return measure;
                continue;
            }

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
