namespace daq_data_collector.Models;

/// <summary>
/// Computed PHM features for one acquisition frame.
/// Produced by PhmFeatureTransform and consumed by TelemetryPipeline.
/// Corresponds to WISE-2410 equivalent parameters for a single axis.
/// </summary>
public class DaqPhMFeatures
{
    // ── Metadata ─────────────────────────────────────────────────────────────

    /// <summary>Hardware sample timestamp (mapped from DaqRawFrame.Timestamp).</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Edge device system clock at the time of feature computation.</summary>
    public DateTimeOffset DeviceTime { get; init; } = DateTimeOffset.UtcNow;

    // ── Frequency-domain features (computed via FFT, full-frame = 2,500 samples) ─

    /// <summary>X-Axis RMS in mg — energy-equivalent RMS from FFT spectrum (Parseval).</summary>
    public double XAxisRMSmg { get; init; }

    /// <summary>X-Axis Peak in mg — maximum spectral magnitude in FFT spectrum.</summary>
    public double XAxisPeakmg { get; init; }

    /// <summary>X-Axis Peak-to-Peak Displacement — derived via double spectral integration.</summary>
    public double XAxisPeakToPeakDisplacement { get; init; }

    /// <summary>X-Axis Overall Velocity RMS — derived from acceleration spectrum.</summary>
    public double XAxisOAVelocity { get; init; }

    // ── Time-domain features (computed directly from raw samples) ─────────────

    /// <summary>X-Axis Standard Deviation of raw samples.</summary>
    public double XAxisDeviation { get; init; }

    /// <summary>X-Axis Skewness — third standardized central moment.</summary>
    public double XAxisSkewness { get; init; }

    /// <summary>X-Axis Kurtosis — fourth standardized central moment.</summary>
    public double XAxisKurtosis { get; init; }

    /// <summary>X-Axis Crest Factor — ratio of peak to RMS.</summary>
    public double XAxisCrestFactor { get; init; }
}
