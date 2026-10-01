namespace Weda.SubNode.Abstractions.Telemetry;

public class SensorRecordingConfig
{
    /// <summary>
    /// Minimum effective record interval (ms), and the single source of truth for that floor. The
    /// report interval floor is lower (50 ms), so a record interval derived from the report interval
    /// is clamped up to this bound. The clamp lives in the apply path and the reported-JSON patch;
    /// the sensor validator enforces this floor for an enabled record. All reference this constant.
    /// </summary>
    public const int MinimumIntervalMs = 1000;

    /// <summary>
    /// Whether the recording is enabled. Defaults to false: a sensor that declares no record
    /// config does not record.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Recording interval in milliseconds. When <see cref="Enabled"/> is true this is required and
    /// must already be at least <see cref="MinimumIntervalMs"/> — null, 0 and sub-floor values are
    /// rejected by the sensor validator. When recording is disabled it is optional: the apply path
    /// fills it from the report interval and clamps it up to <see cref="MinimumIntervalMs"/>. The
    /// resolved value is reported back to the cloud, whose validator range-checks this field whether
    /// or not recording is enabled.
    /// </summary>
    public int? Interval { get; set; }
}
