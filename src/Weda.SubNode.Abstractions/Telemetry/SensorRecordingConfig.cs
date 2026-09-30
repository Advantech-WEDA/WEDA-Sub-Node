namespace Weda.SubNode.Abstractions.Telemetry;

public class SensorRecordingConfig
{
    /// <summary>
    /// Whether the recording is enabled. Defaults to false: a sensor that declares no record
    /// config does not record.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Recording Interval in Milliseconds. Optional: required only when <see cref="Enabled"/> is
    /// true. When recording is disabled the apply path fills it from the report interval, so the
    /// value round-trips to the cloud without failing the interval range check.
    /// </summary>
    public int? Interval { get; set; }
}