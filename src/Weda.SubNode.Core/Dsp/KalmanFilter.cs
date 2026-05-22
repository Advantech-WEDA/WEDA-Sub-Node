using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Dsp;

/// <summary>
/// Configuration parameters for <see cref="KalmanFilter"/>.
/// </summary>
public class KalmanParameters : IValidatableObject
{
    [Description("Process noise (Q). Must be >= 0.")]
    [JsonPropertyName("processNoise")]
    [Range(0.0, double.MaxValue)]
    [DefaultValue(0.01)]
    public double ProcessNoise { get; init; } = 0.01;

    [Description("Measurement noise (R). Must be > 0.")]
    [JsonPropertyName("measurementNoise")]
    [DefaultValue(0.1)]
    public double MeasurementNoise { get; init; } = 0.1;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MeasurementNoise <= 0)
        {
            yield return new ValidationResult(
                "MeasurementNoise must be greater than 0.",
                [nameof(MeasurementNoise)]);
        }
    }
}

/// <summary>
/// Simple 1D Kalman filter for telemetry data.
/// </summary>
public class KalmanFilter
    : IDspFilter,
      IConfigurableDspFilter<KalmanFilter, KalmanParameters>
{
    private double _processNoise;
    private double _measurementNoise;
    private readonly Dictionary<string, KalmanState> _states = new();

    public static string TypeName => "kalman";

    public static string? Description =>
        "1D Kalman filter that fuses noisy measurements with a process model.";

    public static KalmanFilter Create(KalmanParameters parameters) =>
        new(parameters.ProcessNoise, parameters.MeasurementNoise);

    public KalmanFilter(double processNoise = 0.01, double measurementNoise = 0.1)
    {
        _processNoise = processNoise;
        _measurementNoise = measurementNoise;
    }

    public bool Enabled { get; set; } = true;

    public void UpdateParameters(KalmanParameters parameters)
    {
        _processNoise = parameters.ProcessNoise;
        _measurementNoise = parameters.MeasurementNoise;
        // Note: _states preserved across parameter updates; no warm-up required.
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

            if (measure.Value is not double and not int and not float and not long)
            {
                yield return measure;
                continue;
            }

            var value = Convert.ToDouble(measure.Value);

            if (!_states.ContainsKey(measure.ResourceId))
            {
                _states[measure.ResourceId] = new KalmanState
                {
                    Estimate = value,
                    ErrorCovariance = 1.0,
                };
            }

            var state = _states[measure.ResourceId];

            var predictedEstimate = state.Estimate;
            var predictedErrorCovariance = state.ErrorCovariance + _processNoise;

            var kalmanGain = predictedErrorCovariance / (predictedErrorCovariance + _measurementNoise);
            var estimate = predictedEstimate + kalmanGain * (value - predictedEstimate);
            var errorCovariance = (1 - kalmanGain) * predictedErrorCovariance;

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
