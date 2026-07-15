using System.Text.Json.Nodes;

namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Universal sensor envelope. Defines the DTDL Interface that every typed
/// <see cref="IConfigurableSensor{TParameter}"/> Interface <c>extends</c>, so each
/// sensor-type Interface only contributes its protocol-specific <c>Parameters</c>
/// Property and inherits Name / SensorGroup / Report / Record / SensorInfo from here.
///
/// <para>The Interface is hand-authored (not emitted via
/// <c>WedaDtdlEmitter.Emit</c>) because the base envelope needs primitive-typed
/// top-level Properties (<c>Name</c>, <c>SensorGroup</c>) which the current
/// emitter only produces for Object-typed PropertyBindings. Migrating to an
/// emitter-driven base is a future Weda.Dtdl enhancement (FR-E: primitive
/// property binding).</para>
///
/// <para>The Interface ships exactly once per upload: <c>SensorTypeRegistry</c>
/// adds it to <c>DeviceConfigurationDto.Dtdl[]</c> the first time a typed sensor
/// type is registered; dedup-by-@id in
/// <c>DeviceConfigurationMappingExtensions.BuildDtdlList</c> keeps it singular.</para>
/// </summary>
public static class SensorBase
{
    public const string Dtmi = "dtmi:advantech:weda:sensor:base;1";

    private const string ReportDtmi       = "dtmi:advantech:weda:sensor:base:Report;1";
    private const string RecordDtmi       = "dtmi:advantech:weda:sensor:base:Record;1";
    private const string SensorInfoDtmi   = "dtmi:advantech:weda:sensor:base:SensorInfo;1";
    private const string PipelineStepDtmi = "dtmi:advantech:weda:sensor:base:PipelineStep;1";
    private const string PipelineListDtmi = "dtmi:advantech:weda:sensor:base:PipelineList;1";

    /// <summary>
    /// Builds a fresh <see cref="JsonObject"/> for the base Interface. Returns a
    /// new instance each call so callers can mutate freely (e.g. inserting into
    /// <c>dtdl[]</c>) without aliasing.
    /// </summary>
    public static JsonObject GetInterface() => new()
    {
        ["@context"] = new JsonArray("dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"),
        ["@id"]      = Dtmi,
        ["@type"]    = "Interface",
        ["displayName"] = "Sensor (base)",
        ["description"] = "Universal sensor envelope inherited by every typed sensor Interface.",

        ["schemas"] = new JsonArray
        {
            // PipelineStep: a chosen transform/dsp catalog reference (no inline params;
            // the referenced capability Interface in dtdl[] describes the param shape).
            new JsonObject
            {
                ["@id"] = PipelineStepDtmi,
                ["@type"] = "Object",
                ["fields"] = new JsonArray
                {
                    Field("type",    "string",  required: true,  description: "Catalog name of the chosen transform/dsp (e.g. unitconversion)."),
                    Field("dtmi",    "string",  required: true,  description: "DTMI of the chosen capability Interface; resolve in dtdl[] for parameter shape."),
                    Field("enabled", "boolean", required: false),
                },
            },

            // PipelineList: Array<PipelineStep>.
            new JsonObject
            {
                ["@id"] = PipelineListDtmi,
                ["@type"] = "Array",
                ["elementSchema"] = PipelineStepDtmi,
            },

            // Report: sampling cadence + transform/dsp pipelines.
            new JsonObject
            {
                ["@id"] = ReportDtmi,
                ["@type"] = "Object",
                ["fields"] = new JsonArray
                {
                    Field("enabled",           "boolean", required: false),
                    Field("interval",          "integer", required: false, minimum: 1, description: "Sampling interval (ms)."),
                    Field("unit",              "string",  required: false),
                    Field("transformPipeline", PipelineListDtmi, required: false),
                    Field("dspPipeline",       PipelineListDtmi, required: false),
                },
            },

            // Record: local recording cadence.
            new JsonObject
            {
                ["@id"] = RecordDtmi,
                ["@type"] = "Object",
                ["fields"] = new JsonArray
                {
                    Field("enabled",  "boolean", required: false),
                    Field("interval", "integer", required: false, minimum: 1, description: "Recording interval (ms)."),
                },
            },

            // SensorInfo: wire schema + UI metadata.
            new JsonObject
            {
                ["@id"] = SensorInfoDtmi,
                ["@type"] = "Object",
                ["fields"] = new JsonArray
                {
                    Field("schema",      "string", required: true,  description: "Wire schema: double / long / integer / boolean / string / MIME type."),
                    Field("displayName", "string", required: false),
                    Field("description", "string", required: false),
                },
            },
        },

        ["contents"] = new JsonArray
        {
            Property("Name",        "string", required: true,  writable: false),
            Property("SensorGroup", "string", required: true,  writable: false, description: "AI / AO / DI / DO / SYS / TEMP / PWR."),
            Property("Report",      ReportDtmi,     required: false, writable: true),
            Property("Record",      RecordDtmi,     required: false, writable: true),
            Property("SensorInfo",  SensorInfoDtmi, required: false, writable: true),
        },
    };

    private static JsonObject Field(
        string name, string schema, bool required,
        int? minimum = null,
        string? description = null)
    {
        var f = new JsonObject
        {
            ["@type"] = new JsonArray("Field", "ConfigConstraint"),
            ["name"] = name,
        };
        if (description is not null) f["description"] = description;
        if (minimum.HasValue) f["minimum"] = minimum.Value;
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
            ["name"] = name,
        };
        if (description is not null) p["description"] = description;
        p["schema"]   = schema;
        p["required"] = required;
        p["writable"] = writable;
        return p;
    }
}
