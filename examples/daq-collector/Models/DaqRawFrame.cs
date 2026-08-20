namespace daq_data_collector.Models;

/// <summary>
/// Raw vibration samples collected from one DAQ acquisition frame.
/// Produced by DaqCollector and consumed by PhmFeatureTransform.
/// One frame = FrameSize samples at SamplingRate Hz (typically 2,500 samples @ 2,500 Hz = 1 second).
/// </summary>
public class DaqRawFrame
{
    /// <summary>Raw acceleration samples in mg units.</summary>
    public float[] Samples { get; init; } = [];

    /// <summary>Hardware timestamp of the first sample in this frame.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Axis identifier, e.g. "X".</summary>
    public string AxisName { get; init; } = string.Empty;

    /// <summary>Sampling rate in Hz, e.g. 25600.</summary>
    public double SamplingRate { get; init; }
}
