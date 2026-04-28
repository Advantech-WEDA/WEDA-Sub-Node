using daq_data_collector.Models;
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
    /// Extracts time-domain features from the provided raw frame.
    /// Writes results into the supplied <paramref name="features"/> builder object.
    /// </summary>
    public TimeDomainResults Extract(DaqRawFrame frame)
    {
        throw new NotImplementedException(
            "Time-domain feature extraction (Deviation, Skewness, Kurtosis, CrestFactor) not yet implemented.");
    }
}

/// <summary>Intermediate result holder for time-domain features.</summary>
public record TimeDomainResults(
    double Deviation,
    double Skewness,
    double Kurtosis,
    double CrestFactor);
