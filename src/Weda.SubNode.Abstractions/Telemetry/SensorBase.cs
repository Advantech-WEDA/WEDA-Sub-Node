using System.Text.Json.Nodes;

using Weda.SubNode.Abstractions.DigitalTwin;

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

        // Envelope schemas are built by SensorEnvelopeSchemas so sensor:base and
        // device:base (SensorRef) stay shape-identical under their own @id namespaces.
        ["schemas"] = new JsonArray
        {
            SensorEnvelopeSchemas.PipelineStep(PipelineStepDtmi),
            SensorEnvelopeSchemas.PipelineList(PipelineListDtmi, PipelineStepDtmi),
            SensorEnvelopeSchemas.Report(ReportDtmi, PipelineListDtmi),
            SensorEnvelopeSchemas.Record(RecordDtmi),
            SensorEnvelopeSchemas.SensorInfo(SensorInfoDtmi),
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
