namespace Weda.SubNode.Abstractions.Telemetry;

public class SensorRecordingConfig
{
    /// <summary>
    /// Whether the recording is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Recording Interval in Milliseconds.
    /// 0 means equals to sensor reporting interval.
    /// default is 0
    /// </summary>
    public int Interval { get; set; } = 0;
}