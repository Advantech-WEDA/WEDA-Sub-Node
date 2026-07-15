using daq_data_collector.Models;

using MathNet.Numerics.Statistics;

using Microsoft.Extensions.Logging;

namespace daq_data_collector.Communication.Pipeline;

/// <summary>
/// Computes time-domain PHM features from a raw vibration frame.
/// Features: Deviation, Skewness, Kurtosis, CrestFactor.
/// Sampling rate is configured from devicecfg.json Properties.SamplingRate.
/// </summary>
public class TimeDomainExtractor
{
    /// <summary>Sampling rate in Hz (configured from devicecfg.json).</summary>
    private readonly int _samplingRate;

    private readonly ILogger<TimeDomainExtractor> _logger;

    public TimeDomainExtractor(
        int samplingRate,
        ILogger<TimeDomainExtractor> logger)
    {
        if (samplingRate <= 0)
            throw new ArgumentException("Sampling rate must be a positive integer", nameof(samplingRate));
        _samplingRate = samplingRate;
        _logger = logger;
    }

    /// <summary>Gets the configured sampling rate.</summary>
    public int SamplingRate => _samplingRate;

    /// <summary>
    /// Extracts time-domain features from the provided raw frame using MathNet.Numerics.
    /// Features: Deviation (StdDev), Skewness, Kurtosis, CrestFactor.
    /// </summary>
    public TimeDomainResults Extract(DaqRawFrame frame)
    {
        if (frame?.Samples == null || frame.Samples.Length == 0)
            throw new ArgumentException("Frame must contain at least one sample", nameof(frame));

        // Convert float samples to double for MathNet
        var samples = frame.Samples.Select(s => (double)s).ToArray();

        // Use MathNet.Numerics for robust statistics
        double deviation = samples.StandardDeviation();
        double skewness = samples.Skewness();
        double kurtosis = samples.Kurtosis();

        // Compute RMS and Crest Factor
        double rms = Math.Sqrt(samples.Sum(s => s * s) / samples.Length);
        double max = samples.Maximum();
        double min = samples.Minimum();
        double peakAmplitude = Math.Max(Math.Abs(max), Math.Abs(min));
        double crestFactor = rms > 0 ? peakAmplitude / rms : 0.0;

        return new TimeDomainResults(
            Deviation: deviation,
            Skewness: skewness,
            Kurtosis: kurtosis,
            CrestFactor: crestFactor);
    }
}

/// <summary>Intermediate result holder for time-domain features.</summary>
public record TimeDomainResults(
    double Deviation,
    double Skewness,
    double Kurtosis,
    double CrestFactor);
