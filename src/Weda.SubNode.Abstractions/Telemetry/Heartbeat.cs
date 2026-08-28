using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;

namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// The reserved SubNode liveness heartbeat, per SD-Heartbeat-Committed-Scope
/// §API Contract — "The heartbeat sensor".
/// </summary>
/// <remarks>
/// <para>
/// The heartbeat is declared as a reserved sensor in <c>devicecfg.json</c>, identified by
/// its reserved <see cref="SensorName"/>. Declaring it is what turns liveness on — there is
/// no separate switch, and a SubNode with no such sensor emits nothing. That makes the
/// feature opt-in per application while still being available to every SubNode.
/// </para>
/// <para>
/// Declaring it as a sensor rather than inventing a side channel buys three things: the
/// beat gets a real <c>ResourceId</c>, it appears in the generated DTDL and the capability
/// upload, and it registers in the device-management sensor registry — so enriched
/// telemetry resolves it instead of falling back to <c>"unknown"</c>.
/// </para>
/// <para>
/// <strong>The DTMI and schema are SDK-owned, not configuration.</strong>
/// <see cref="ApplyReservedContract"/> stamps <see cref="Dtmi"/> and <see cref="Schema"/>
/// onto the declared sensor before DTDL generation, so <c>devicecfg.json</c> declares only
/// a name and a report interval and the model is generated automatically like any other
/// sensor. Identity is the name because that is the one field the author must write:
/// deriving identity from a value the SDK itself supplies would be circular, and requiring
/// the author to hand-write a platform DTMI made deleting one line silently disable
/// liveness with no error anywhere.
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
/// pinned to a literal. The beat is instead identified by <see cref="Dtmi"/>, which the
/// enrichment stage already resolves for every measure. Note this is necessarily a
/// <em>post</em>-enrichment match, where the SD asked for pre-enrichment; a marker cannot
/// travel on the raw message because a measure's <c>metadata</c> is the framework's
/// chunked-transfer descriptor and anything placed there without a <c>transferId</c> causes
/// the WedaNode telemetry proxy to reject the entire message.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // devicecfg.json — DeviceConfigs.&lt;device&gt;.Sensors[]
/// // Dtmi and SensorInfo.Schema are deliberately absent: the SDK supplies both.
/// {
///   "Name": "hb",
///   "SensorGroup": "SYS",
///   "Report": { "Enabled": true, "Interval": 60000 },
///   "Record": { "Enabled": false }
/// }
/// </code>
/// </example>
public static class Heartbeat
{
    /// <summary>
    /// Reserved name for the liveness sensor. This is the heartbeat's identity: the SDK
    /// recognises the beat by this name and nothing else.
    /// </summary>
    /// <remarks>
    /// The name is reserved platform-wide. A customer sensor called <c>hb</c> is treated as
    /// the heartbeat — it is excluded from polling and has its DTMI and schema overwritten.
    /// That is the cost of making the name the identity; it is accepted because the name is
    /// the only field the configuration author necessarily writes, and because <c>hb</c> is
    /// the identifier the SD already reserves for this purpose.
    /// </remarks>
    public const string SensorName = "hb";

    /// <summary>
    /// Reserved DTMI, stamped onto the declared sensor by
    /// <see cref="ApplyReservedContract"/>. Configuration does not supply it.
    /// </summary>
    /// <remarks>
    /// A fixed constant rather than a device-scoped auto-generated DTMI, because this is
    /// what the platform matches on to recognise a liveness measure — an identifier that
    /// varied per device or per SubNode could not serve that purpose.
    /// </remarks>
    public const string Dtmi = "dtmi:com:advantech:weda:Heartbeat;1";

    /// <summary>
    /// Reserved telemetry schema. The beat is a constant <c>true</c>, so the DTDL primitive
    /// is <c>boolean</c>; it is stamped rather than configured so the declared schema can
    /// never disagree with the value the SDK actually sends.
    /// </summary>
    public const string Schema = "boolean";

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
    /// <returns><see langword="true"/> when the sensor carries the reserved name.</returns>
    /// <remarks>
    /// Also matches a sensor that carries the reserved <see cref="Dtmi"/> under a different
    /// name, so configurations written against the earlier DTMI-keyed contract keep working.
    /// </remarks>
    public static bool IsHeartbeat(Sensor? sensor) =>
        sensor is not null
        && (string.Equals(sensor.Name, SensorName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sensor.Dtmi, Dtmi, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Stamps the SDK-owned parts of the reserved contract onto a device's declared
    /// heartbeat sensor: its <see cref="Dtmi"/> and its <see cref="Schema"/>.
    /// </summary>
    /// <param name="sensors">The device's configured sensors. May be <see langword="null"/>.</param>
    /// <param name="logger">Optional logger, used to report overwritten configuration.</param>
    /// <remarks>
    /// <para>
    /// Must run before DTDL generation. Once stamped, the heartbeat flows through the normal
    /// auto-generation path — <c>DtdlGenerator.PopulateSensorDtmis</c> leaves it alone
    /// because it already has a DTMI, and <c>GenerateInterface</c> emits it as an ordinary
    /// <c>boolean</c> Telemetry content.
    /// </para>
    /// <para>
    /// Overwrites rather than merges. Both values are part of a platform contract the
    /// application does not get to vary, and honouring a configured schema of, say,
    /// <c>double</c> would produce a model that disagrees with the <c>true</c> the SDK
    /// sends. Anything overwritten is logged so the divergence is visible.
    /// </para>
    /// </remarks>
    public static void ApplyReservedContract(IEnumerable<Sensor>? sensors, ILogger? logger = null)
    {
        if (sensors is null)
            return;

        foreach (var sensor in sensors)
        {
            if (!IsHeartbeat(sensor))
                continue;

            if (!string.IsNullOrEmpty(sensor.Dtmi)
                && !string.Equals(sensor.Dtmi, Dtmi, StringComparison.OrdinalIgnoreCase))
            {
                logger?.LogWarning(
                    "Sensor '{Sensor}' is the reserved heartbeat; its configured dtmi " +
                    "'{Configured}' is replaced by the platform dtmi '{Reserved}'",
                    sensor.Name, sensor.Dtmi, Dtmi);
            }

            if (!string.Equals(sensor.SensorInfo.Schema, Schema, StringComparison.OrdinalIgnoreCase))
            {
                logger?.LogDebug(
                    "Sensor '{Sensor}' is the reserved heartbeat; schema '{Configured}' " +
                    "is replaced by '{Reserved}'",
                    sensor.Name, sensor.SensorInfo.Schema, Schema);
            }

            sensor.Dtmi = Dtmi;
            sensor.SensorInfo.Schema = Schema;
        }
    }

    /// <summary>
    /// The DTDL Interface that defines <see cref="Dtmi"/>, for the capability upload's
    /// <c>refModels</c>.
    /// </summary>
    /// <returns>An Interface extending the universal sensor envelope.</returns>
    /// <remarks>
    /// <para>
    /// Without this the heartbeat's <c>SensorDto.Dtmi</c> is a dangling reference: appearing
    /// as a Telemetry <c>@id</c> inside the SubNode wrapper Interface is <em>not</em> the
    /// same as being defined in <c>refModels</c>, which is where a sensor's dtmi is
    /// resolved. Ordinary sensors reach <c>refModels</c> through
    /// <c>SensorTypeRegistry</c>, and the heartbeat deliberately never enters typed
    /// dispatch — so it has to supply its own definition.
    /// </para>
    /// <para>
    /// Carries no <c>Parameters</c> property. Typed sensor Interfaces bind one, but the
    /// heartbeat takes no configuration and the cloud rejects a SensorRef carrying unknown
    /// fields — so the Interface stays an envelope and nothing more.
    /// </para>
    /// </remarks>
    public static JsonObject GetInterface() => new()
    {
        ["@context"] = new JsonArray("dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"),
        ["@id"] = Dtmi,
        ["@type"] = "Interface",
        ["extends"] = SensorBase.Dtmi,
        ["displayName"] = "Heartbeat",
        ["description"] =
            "Reserved SubNode liveness beat. Declared as a sensor so it carries a ResourceId "
            + "and registers in the sensor registry; it is never polled and its boolean value "
            + "is synthesised by the SDK.",
    };

    /// <summary>
    /// Whether any of the given sensors is the reserved heartbeat, i.e. whether
    /// <see cref="GetInterface"/> has to travel in the capability upload.
    /// </summary>
    /// <param name="sensors">The sensors to inspect. May be <see langword="null"/>.</param>
    public static bool IsDeclaredBy(IEnumerable<Sensor>? sensors) =>
        sensors?.Any(IsHeartbeat) == true;

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
