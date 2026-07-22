namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// The reserved SubNode liveness heartbeat, per SD-Heartbeat-Committed-Scope
/// §API Contract — "The heartbeat sensor".
/// </summary>
/// <remarks>
/// <para>
/// The heartbeat is declared as a reserved sensor in <c>devicecfg.json</c>, identified by
/// its platform-owned <see cref="Dtmi"/>. Declaring it is what turns liveness on — there
/// is no separate switch, and a SubNode with no such sensor emits nothing. That makes the
/// feature opt-in per application while still being available to every SubNode.
/// </para>
/// <para>
/// Declaring it as a sensor rather than inventing a side channel buys three things: the
/// beat gets a real <c>ResourceId</c>, it appears in the generated DTDL and the capability
/// upload, and it registers in the device-management sensor registry — so enriched
/// telemetry resolves it instead of falling back to <c>"unknown"</c>.
/// </para>
/// <para>
/// <strong>It is not polled.</strong> No protocol parser is ever asked to read it —
/// <c>DeviceBase.GroupSensorsByInterval</c> excludes it from the polling path. The value is
/// synthesised by the SDK, so the heartbeat works identically on Modbus, OPC UA, MQTT or
/// any custom device, none of which could produce a liveness reading themselves.
/// </para>
/// <para>
/// <strong>Detection contract.</strong> The SD specifies detection on a well-known
/// <c>sensorId == "hb"</c>. That is not achievable in this SDK: the wire <c>sensorId</c> is
/// <c>ResourceId[^5..]</c>, and <c>ResourceId</c> is a UUID5 over
/// (subNodeDeviceId, deviceName, sensorName), so it differs per SubNode and cannot be
/// pinned to a literal. Liveness is therefore marked with <see cref="MarkerKey"/> in the
/// measure's <c>metadata</c>. Metadata is present on the raw message, which preserves the
/// SD's requirement that detection happen <em>pre-enrichment</em>, with no sensor-registry
/// or IAM lookup on the hot path.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // devicecfg.json — DeviceConfigs.&lt;device&gt;.Sensors[]
/// {
///   "Name": "hb",
///   "Dtmi": "dtmi:com:advantech:weda:Heartbeat;1",
///   "SensorGroup": "SYS",
///   "SensorInfo": { "Schema": "boolean", "DisplayName": "Heartbeat" },
///   "Report": { "Enabled": true, "Interval": 60000 },
///   "Record": { "Enabled": false }
/// }
/// </code>
/// </example>
public static class Heartbeat
{
    /// <summary>Conventional name for the reserved sensor.</summary>
    /// <remarks>
    /// Identity is <see cref="Dtmi"/>, not this name — a customer sensor that happens to
    /// be called <c>hb</c> is not the platform heartbeat and gets none of its handling.
    /// </remarks>
    public const string SensorName = "hb";

    /// <summary>
    /// Reserved DTMI. This is the heartbeat's identity: the SDK recognises a sensor as the
    /// liveness beat by this value and nothing else.
    /// </summary>
    public const string Dtmi = "dtmi:com:advantech:weda:Heartbeat;1";

    /// <summary>
    /// Reserved key set on the measure's <c>metadata</c>. This is what the platform matches
    /// on to identify a liveness measure on the raw telemetry message.
    /// </summary>
    public const string MarkerKey = "hb";

    /// <summary>
    /// Lower bound on the beat interval. Guards against a mis-set configuration value
    /// flooding the telemetry path — the heartbeat is a liveness ping, not a data source.
    /// </summary>
    public const int MinIntervalMilliseconds = 1_000;

    /// <summary>
    /// Recommended beat interval <c>T</c>. 60 s per User Stories v2.11 §Feature 3, the
    /// authoritative source for this release's hardcoded thresholds
    /// (T = 60 s, N = 3, M = 10 min). SD-Heartbeat-Committed-Scope §Thresholds still states
    /// 30 s; that divergence is raised against the SD and is not resolved here.
    /// </summary>
    public const int DefaultIntervalMilliseconds = 60_000;

    /// <summary>
    /// Determines whether a configured sensor is the reserved platform heartbeat.
    /// </summary>
    /// <param name="sensor">The sensor to test. May be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the sensor carries the reserved DTMI.</returns>
    public static bool IsHeartbeat(Sensor? sensor) =>
        sensor is not null
        && string.Equals(sensor.Dtmi, Dtmi, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Finds the effectively-enabled heartbeat sensor in a device's sensor list.
    /// </summary>
    /// <param name="sensors">The device's configured sensors.</param>
    /// <returns>The heartbeat sensor, or <see langword="null"/> when the device declares none.</returns>
    /// <remarks>
    /// Honours <c>IsEffectivelyEnabled</c>, so disabling the sensor — at device level or
    /// sensor level — stops the beat. That is a deliberate consequence of putting liveness
    /// on the configurable sensor surface; see the remarks on the heartbeat hosted service
    /// for what that means operationally.
    /// </remarks>
    public static Sensor? FindEnabled(IEnumerable<Sensor>? sensors) =>
        sensors?.FirstOrDefault(s => IsHeartbeat(s) && s.IsEffectivelyEnabled);

    /// <summary>
    /// Resolves the beat interval for a heartbeat sensor, clamped to
    /// <see cref="MinIntervalMilliseconds"/>.
    /// </summary>
    /// <param name="sensor">The reserved heartbeat sensor.</param>
    /// <returns>The interval to beat at, in milliseconds.</returns>
    /// <remarks>
    /// Clamps rather than throws. This value can arrive from a cloud configuration update,
    /// and refusing to run because someone typed a bad interval would take liveness down
    /// entirely — the opposite of what a liveness signal is for.
    /// </remarks>
    public static int ResolveInterval(Sensor sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        // Report.Interval is a double for sub-millisecond sampling rates; the beat cadence
        // is whole milliseconds, matching how DeviceBase groups polling intervals.
        var interval = sensor.Report.Interval;
        return interval < MinIntervalMilliseconds ? MinIntervalMilliseconds : (int)interval;
    }
}
