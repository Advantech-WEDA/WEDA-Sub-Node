using System.Numerics;

using daq_data_collector.Models;

using MathNet.Numerics.IntegralTransforms;

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
    /// Extracts frequency-domain features from the provided raw frame using MathNet.Numerics FFT.
    /// Features: RMSmg, Peakmg, Peak-to-Peak Displacement, OAVelocity.
    /// Uses MathNet's optimized Fourier transform for improved performance and accuracy.
    /// </summary>
    public FrequencyDomainResults Extract(DaqRawFrame frame)
    {
        if (frame?.Samples == null || frame.Samples.Length == 0)
            throw new ArgumentException("Frame must contain at least one sample", nameof(frame));

        var samples = frame.Samples;
        int n = samples.Length;
        double fs = frame.SamplingRate;  // Sampling frequency in Hz

        // Calculate mean and center the signal
        double mean = samples.Average();
        var centeredSamples = samples.Select(s => s - mean).ToArray();

        // Apply Hann window to reduce spectral leakage
        var windowedSamples = ApplyHannWindow(centeredSamples);

        // Convert to Complex and apply MathNet FFT
        var fft = windowedSamples.Select(s => new Complex(s, 0)).ToArray();
        Fourier.Forward(fft, FourierOptions.Matlab);

        // Compute frequency resolution and normalization factor
        double freqResolution = fs / n;
        double normFactor = 2.0 / n;  // Two-sided to one-sided conversion

        // Compute RMS from FFT spectrum (Parseval's theorem)
        double rmsmg = ComputeRmsFromSpectrum(fft, normFactor);

        // Find peak magnitude
        double peakmg = ComputePeakMagnitude(fft, normFactor);

        // Compute peak-to-peak displacement via double integration
        double peakToPeakDisplacement = ComputePeakToPeakDisplacement(fft, fs, normFactor);

        // Compute overall velocity RMS
        double oavelocity = ComputeOverallVelocity(fft, fs, normFactor);

        return new FrequencyDomainResults(
            RMSmg: rmsmg,
            Peakmg: peakmg,
            PeakToPeakDisplacement: peakToPeakDisplacement,
            OAVelocity: oavelocity);
    }

    private double[] ApplyHannWindow(double[] samples)
    {
        int n = samples.Length;
        var windowed = new double[n];
        for (int i = 0; i < n; i++)
        {
            double window = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (n - 1)));
            windowed[i] = samples[i] * window;
        }
        return windowed;
    }

    private double ComputeRmsFromSpectrum(Complex[] fft, double normFactor)
    {
        int n = fft.Length;
        double energy = 0.0;

        // DC component
        energy += Math.Pow(fft[0].Magnitude, 2);

        // Positive frequencies (one-sided spectrum)
        for (int i = 1; i < n / 2; i++)
            energy += 2.0 * Math.Pow(fft[i].Magnitude, 2);

        // Nyquist component (if n is even)
        if (n % 2 == 0)
            energy += Math.Pow(fft[n / 2].Magnitude, 2);

        // RMS from Parseval's theorem: RMS = sqrt(energy / n)
        double rms = Math.Sqrt(energy / (n * n));
        return rms;
    }

    private double ComputePeakMagnitude(Complex[] fft, double normFactor)
    {
        double peak = 0.0;
        for (int i = 0; i < fft.Length / 2; i++)
            peak = Math.Max(peak, fft[i].Magnitude * normFactor);
        return peak;
    }

    private double ComputePeakToPeakDisplacement(Complex[] fft, double fs, double normFactor)
    {
        // Displacement = double integration of acceleration
        // In frequency domain: X(f) = A(f) / (-4 * pi^2 * f^2)
        int n = fft.Length;
        double peakDisplacement = 0.0;
        double freqResolution = fs / n;

        for (int i = 1; i < n / 2; i++)
        {
            double freq = i * freqResolution;
            if (freq > 0)
            {
                double denominator = 4.0 * Math.PI * Math.PI * freq * freq;
                double magnitude = fft[i].Magnitude * normFactor / denominator;
                peakDisplacement = Math.Max(peakDisplacement, magnitude);
            }
        }

        return peakDisplacement * 2.0;  // Peak-to-peak
    }

    private double ComputeOverallVelocity(Complex[] fft, double fs, double normFactor)
    {
        // Velocity RMS = integration of acceleration
        // In frequency domain: V(f) = A(f) / (2 * pi * f)
        int n = fft.Length;
        double velocityEnergy = 0.0;
        double freqResolution = fs / n;

        // DC component (ignored, velocity requires differentiation)
        // Positive frequencies
        for (int i = 1; i < n / 2; i++)
        {
            double freq = i * freqResolution;
            if (freq > 0)
            {
                double denominator = 2.0 * Math.PI * freq;
                double magnitude = fft[i].Magnitude * normFactor / denominator;
                velocityEnergy += 2.0 * magnitude * magnitude;
            }
        }

        double velocityRms = Math.Sqrt(velocityEnergy / (n * n));
        return velocityRms;
    }
}

/// <summary>Intermediate result holder for frequency-domain features.</summary>
public record FrequencyDomainResults(
    double RMSmg,
    double Peakmg,
    double PeakToPeakDisplacement,
    double OAVelocity);
