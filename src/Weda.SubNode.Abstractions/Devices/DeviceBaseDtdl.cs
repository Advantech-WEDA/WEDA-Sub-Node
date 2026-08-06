using System.Text.Json.Nodes;

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
/// <para>Ships once per upload: <c>DeviceTypeRegistry</c> adds it to
/// <c>DeviceConfigurationDto.RefModels</c> the first time a typed device type is
/// registered; dedup-by-@id in <c>DeviceConfigurationMappingExtensions</c> keeps
/// it singular.</para>
/// </summary>
public static class DeviceBaseDtdl
{
    public const string Dtmi = "dtmi:advantech:weda:device:base;1";

    private const string SensorRefDtmi  = "dtmi:advantech:weda:device:base:SensorRef;1";
    private const string SensorListDtmi = "dtmi:advantech:weda:device:base:SensorList;1";

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
            // SensorRef: a sensor contributed by this device config. Mirrors
            // sensor:base:PipelineStep — the referenced sensor-type Interface in
            // refModels[] describes the Parameters / Report shape.
            new JsonObject
            {
                ["@id"] = SensorRefDtmi,
                ["@type"] = "Object",
                ["fields"] = new JsonArray
                {
                    Field("name", "string", required: true, description: "Sensor name, unique within the device config."),
                    Field("dtmi", "string", required: true, description: "DTMI of the chosen sensor-type Interface; resolve in refModels[] for Parameters/Report shape."),
                },
            },

            // SensorList: Array<SensorRef>.
            new JsonObject
            {
                ["@id"] = SensorListDtmi,
                ["@type"] = "Array",
                ["elementSchema"] = SensorRefDtmi,
            },
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
