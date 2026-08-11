using System.Text.Json.Nodes;

using Weda.SubNode.Abstractions.DigitalTwin;

namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Universal device-config envelope. Defines the DTDL Interface that every typed
/// device Interface (<see cref="IConfigurableDevice{TCommunication,TProperties}"/>)
/// extends, so each device-type Interface contributes only its type-specific
/// <c>DeviceCommunication</c> and <c>Properties</c> Properties and inherits
/// <c>Enabled</c> from here.
///
/// <para>Mirrors the pattern of <c>SensorBase</c>. Hand-authored because
/// <c>WedaDtdlEmitter</c> currently only produces Object-typed Property bindings;
/// the primitive <c>Enabled</c> boolean cannot be expressed via a
/// <see cref="WedaDtdlEmitter.PropertyBinding"/> yet (FR-E: primitive property
/// binding).</para>
///
/// <para>The definition is still emitted for local parsing/validation, but is no
/// longer uploaded: <c>DeviceConfigurationDto.RefModels</c> is obsolete and now
/// rides the wire empty, with the cloud resolving the base Interface from the
/// shared <c>Weda.Dtdl</c> catalog via <c>refModelsMap</c>.</para>
/// </summary>
public static class DeviceBaseDtdl
{
    public const string Dtmi = "dtmi:advantech:weda:device:base;1";

    private const string SensorRefDtmi  = "dtmi:advantech:weda:device:base:SensorRef;1";
    private const string SensorListDtmi = "dtmi:advantech:weda:device:base:SensorList;1";

    // Device-scoped copies of the sensor envelope schemas (built by
    // SensorEnvelopeSchemas, so they stay shape-identical to sensor:base's).
    // They must live under this Interface's own @id namespace: the shadow
    // validator parses each Interface with only its extends closure, so a
    // reference into sensor:base's schemas would not resolve.
    private const string ReportDtmi       = "dtmi:advantech:weda:device:base:Report;1";
    private const string RecordDtmi       = "dtmi:advantech:weda:device:base:Record;1";
    private const string SensorInfoDtmi   = "dtmi:advantech:weda:device:base:SensorInfo;1";
    private const string PipelineStepDtmi = "dtmi:advantech:weda:device:base:PipelineStep;1";
    private const string PipelineListDtmi = "dtmi:advantech:weda:device:base:PipelineList;1";

    /// <summary>
    /// Builds a fresh <see cref="JsonObject"/> for the base Interface. Returns a
    /// new instance each call so callers can mutate freely without aliasing.
    /// </summary>
    /// <remarks>
    /// The <c>Sensors</c> Property makes device-to-sensor containment self-describing
    /// from the schema alone — mirroring how <c>sensor:base:Report.transformPipeline</c>
    /// declares that a sensor carries a transform pipeline. Each <c>SensorRef</c>
    /// references a sensor-type Interface by DTMI (resolve in <c>refModels[]</c>),
    /// exactly as <c>sensor:base:PipelineStep</c> references a transform/dsp capability.
    /// This describes placement only; sensor-type↔device-type compatibility remains a
    /// catalog fact (<c>deviceCapabilities.sensorTypes[].deviceType</c>).
    /// </remarks>
    public static JsonObject GetInterface() => new()
    {
        ["@context"]    = new JsonArray("dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"),
        ["@id"]         = Dtmi,
        ["@type"]       = "Interface",
        ["displayName"] = "Device config (base)",
        ["description"] = "Universal device-config envelope inherited by every typed device Interface.",

        ["schemas"] = new JsonArray
        {
            // SensorRef: a sensor contributed by this device config. Carries the
            // generic sensor envelope (group/report/record/sensorInfo) via the
            // device-scoped envelope schemas below. Parameters are deliberately
            // absent: their shape is protocol-specific and comes from the
            // sensor-type Interface the dtmi field resolves to (mirroring how
            // sensor:base:PipelineStep defers parameter shape to the referenced
            // transform/dsp capability Interface).
            new JsonObject
            {
                ["@id"] = SensorRefDtmi,
                ["@type"] = "Object",
                ["fields"] = new JsonArray
                {
                    Field("name", "string", required: true, description: "Sensor name, unique within the device config."),
                    // Not required: the SubNode owns DTMI assignment, so sensor entries
                    // written from the cloud arrive without a dtmi and must validate.
                    Field("dtmi", "string", required: false, description: "DTMI of the chosen sensor-type Interface; resolve in refModels[] for the Parameters shape."),
                    Field("sensorGroup", "string", required: false, description: "AI / AO / DI / DO / SYS / TEMP / PWR."),
                    Field("report",      ReportDtmi,     required: false, description: "Sampling cadence + transform/dsp pipelines."),
                    Field("record",      RecordDtmi,     required: false, description: "Local recording cadence."),
                    Field("sensorInfo",  SensorInfoDtmi, required: false, description: "Wire schema + UI metadata."),
                },
            },

            // SensorList: Array<SensorRef>.
            new JsonObject
            {
                ["@id"] = SensorListDtmi,
                ["@type"] = "Array",
                ["elementSchema"] = SensorRefDtmi,
            },

            // Sensor envelope schemas referenced by SensorRef.
            SensorEnvelopeSchemas.PipelineStep(PipelineStepDtmi),
            SensorEnvelopeSchemas.PipelineList(PipelineListDtmi, PipelineStepDtmi),
            SensorEnvelopeSchemas.Report(ReportDtmi, PipelineListDtmi),
            SensorEnvelopeSchemas.Record(RecordDtmi),
            SensorEnvelopeSchemas.SensorInfo(SensorInfoDtmi),
        },

        ["contents"] = new JsonArray
        {
            Property("Enabled", "boolean", required: false, writable: true,
                description: "Whether this device configuration is active."),
            Property("Sensors", SensorListDtmi, required: false, writable: true,
                description: "Sensors contributed by this device config; each entry references a sensor-type Interface by DTMI."),
        },
    };

    private static JsonObject Field(
        string name, string schema, bool required,
        string? description = null)
    {
        var f = new JsonObject
        {
            ["@type"] = new JsonArray("Field", "ConfigConstraint"),
            ["name"] = name,
        };
        if (description is not null) f["description"] = description;
        f["schema"]   = schema;
        f["required"] = required;
        f["writable"] = true;
        return f;
    }

    private static JsonObject Property(
        string name, string schema, bool required, bool writable,
        string? description = null)
    {
        var p = new JsonObject
        {
            ["@type"] = new JsonArray("Property", "ConfigConstraint"),
            ["name"]  = name,
        };
        if (description is not null) p["description"] = description;
        p["schema"]   = schema;
        p["required"] = required;
        p["writable"] = writable;
        return p;
    }
}
