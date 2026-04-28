using daq_data_collector.Models;

using Microsoft.Extensions.Logging;

namespace daq_data_collector.Communication.Pipeline;

/// <summary>
/// Computes frequency-domain PHM features from a raw vibration frame via FFT.
/// Features: RMSmg, Peakmg, Peak-to-Peak Displacement, OAVelocity.
/// FFT size is configured from devicecfg.json Properties.FftSize.
/// </summary>
public class FrequencyDomainExtractor
{
    /// <summary>Number of samples for FFT analysis (configured from devicecfg.json).</summary>
    private readonly int _fftSize;

    private readonly ILogger<FrequencyDomainExtractor> _logger;

    public FrequencyDomainExtractor(
        int fftSize,
        ILogger<FrequencyDomainExtractor> logger)
    {
        if (fftSize <= 0)
            throw new ArgumentException("FFT size must be a positive integer", nameof(fftSize));
        _fftSize = fftSize;
        _logger = logger;
    }

    /// <summary>Gets the configured FFT size.</summary>
    public int FftSize => _fftSize;

    /// <summary>
    /// Extracts frequency-domain features from the provided raw frame.
    /// </summary>
    public FrequencyDomainResults Extract(DaqRawFrame frame)
    {
        throw new NotImplementedException(
            "Frequency-domain feature extraction (RMSmg, Peakmg, PeakToPeakDisplacement, OAVelocity) via FFT not yet implemented.");
    }
}

/// <summary>Intermediate result holder for frequency-domain features.</summary>
public record FrequencyDomainResults(
    double RMSmg,
    double Peakmg,
    double PeakToPeakDisplacement,
    double OAVelocity);
