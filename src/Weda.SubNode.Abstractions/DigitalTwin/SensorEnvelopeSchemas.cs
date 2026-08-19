using System.Text.Json.Nodes;

namespace Weda.SubNode.Abstractions.DigitalTwin;

/// <summary>
/// Shared builders for the generic sensor-envelope DTDL schemas (report / record /
/// sensorInfo and the transform/dsp pipeline shapes report references).
///
/// <para>Both <c>sensor:base</c> and <c>device:base</c> carry these shapes —
/// sensor:base as the envelope every typed sensor Interface inherits, device:base
/// inside <c>SensorRef</c> so a cloud-written <c>Sensors</c> value validates. The
/// shadow validator parses each Interface with only its <c>extends</c> closure, so
/// an Interface must define every schema it references under its own @id namespace
/// (cross-Interface schema references fail validator construction). These builders
/// keep the shape single-sourced while letting each Interface own its ids.</para>
/// </summary>
internal static class SensorEnvelopeSchemas
{
    /// <summary>A chosen transform/dsp catalog reference (no inline params).</summary>
    internal static JsonObject PipelineStep(string id) => new()
    {
        ["@id"] = id,
        ["@type"] = "Object",
        ["fields"] = new JsonArray
        {
            Field("type",    "string",  required: true,  description: "Catalog name of the chosen transform/dsp (e.g. unitconversion)."),
            Field("dtmi",    "string",  required: true,  description: "DTMI of the chosen capability Interface; resolve in dtdl[] for parameter shape."),
            Field("enabled", "boolean", required: false),
        },
    };

    /// <summary>Array of <see cref="PipelineStep"/>.</summary>
    internal static JsonObject PipelineList(string id, string stepId) => new()
    {
        ["@id"] = id,
        ["@type"] = "Array",
        ["elementSchema"] = stepId,
    };

    /// <summary>Sampling cadence + transform/dsp pipelines.</summary>
    internal static JsonObject Report(string id, string pipelineListId) => new()
    {
        ["@id"] = id,
        ["@type"] = "Object",
        ["fields"] = new JsonArray
        {
            Field("enabled",           "boolean", required: false),
            Field("interval",          "integer", required: false, minimum: 1000, maximum: int.MaxValue, description: "Sampling interval (ms)."),
            Field("unit",              "string",  required: false),
            Field("transformPipeline", pipelineListId, required: false),
            Field("dspPipeline",       pipelineListId, required: false),
        },
    };

    /// <summary>Local recording cadence.</summary>
    internal static JsonObject Record(string id) => new()
    {
        ["@id"] = id,
        ["@type"] = "Object",
        ["fields"] = new JsonArray
        {
            Field("enabled",  "boolean", required: false),
            Field("interval", "integer", required: false, minimum: 1000, maximum: int.MaxValue, description: "Recording interval (ms)."),
        },
    };

    /// <summary>Wire schema + UI metadata.</summary>
    internal static JsonObject SensorInfo(string id) => new()
    {
        ["@id"] = id,
        ["@type"] = "Object",
        ["fields"] = new JsonArray
        {
            Field("schema",      "string", required: true,  description: "Wire schema: double / long / integer / boolean / string / MIME type."),
            Field("displayName", "string", required: false),
            Field("description", "string", required: false),
        },
    };

    internal static JsonObject Field(
        string name, string schema, bool required,
        int? minimum = null,
        int? maximum = null,
        string? description = null)
    {
        var f = new JsonObject
        {
            ["@type"] = new JsonArray("Field", "ConfigConstraint"),
            ["name"] = name,
        };
        if (description is not null) f["description"] = description;
        if (minimum.HasValue) f["minimum"] = minimum.Value;
        if (maximum.HasValue) f["maximum"] = maximum.Value;
        f["schema"]   = schema;
        f["required"] = required;
        f["writable"] = true;
        return f;
    }
}
