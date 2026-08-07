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
/// <para>The definition is still emitted for local parsing/validation, but is no
/// longer uploaded: <c>DeviceConfigurationDto.RefModels</c> is obsolete and now
/// rides the wire empty, with the cloud resolving the base Interface from the
/// shared <c>Weda.Dtdl</c> catalog via <c>refModelsMap</c>.</para>
/// </summary>
public static class DeviceBaseDtdl
{
    public const string Dtmi = "dtmi:advantech:weda:device:base;1";

    /// <summary>
    /// Builds a fresh <see cref="JsonObject"/> for the base Interface. Returns a
    /// new instance each call so callers can mutate freely without aliasing.
    /// </summary>
    public static JsonObject GetInterface() => new()
    {
        ["@context"]    = new JsonArray("dtmi:dtdl:context;3", "dtmi:advantech:edgesync:validation;1"),
        ["@id"]         = Dtmi,
        ["@type"]       = "Interface",
        ["displayName"] = "Device config (base)",
        ["description"] = "Universal device-config envelope inherited by every typed device Interface.",

        ["contents"] = new JsonArray
        {
            Property("Enabled", "boolean", required: false, writable: true,
                description: "Whether this device configuration is active."),
        },
    };

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
