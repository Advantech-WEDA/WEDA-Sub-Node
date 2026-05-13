using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Schema;

/// <summary>
/// Wire-format DTO for a JSON Schema describing the shape of a configurable
/// transform / DSP filter / command parameter (or command response).
/// </summary>
/// <remarks>
/// The internal representation is a <see cref="JsonObject"/> so the schema
/// can be built imperatively by the emitter and serialized transparently as
/// a JSON object — the wrapping type is invisible on the wire.
/// <para>
/// Sample serialized form:
/// </para>
/// <code>
/// {
///   "type": "object",
///   "required": ["fromUnit", "toUnit"],
///   "properties": {
///     "fromUnit": { "type": "string", "enum": ["C", "F", "K"] },
///     "toUnit":   { "type": "string", "enum": ["C", "F", "K"] }
///   }
/// }
/// </code>
/// </remarks>
[JsonConverter(typeof(JsonSchemaDtoConverter))]
public sealed record JsonSchemaDto(JsonObject Root)
{
    /// <summary>
    /// Returns a schema for an object type with no properties — used when a
    /// transform / DSP filter / command takes no parameters.
    /// </summary>
    public static JsonSchemaDto EmptyObject() => new(
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(),
        });
}

/// <summary>
/// Serializes <see cref="JsonSchemaDto"/> as its inner JSON object directly,
/// without exposing the wrapper type on the wire.
/// </summary>
internal sealed class JsonSchemaDtoConverter : JsonConverter<JsonSchemaDto>
{
    public override JsonSchemaDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var node = JsonNode.Parse(ref reader)
            ?? throw new JsonException($"{nameof(JsonSchemaDto)} cannot be null");
        if (node is not JsonObject root)
        {
            throw new JsonException($"{nameof(JsonSchemaDto)} expects a JSON object root");
        }
        return new JsonSchemaDto(root);
    }

    public override void Write(Utf8JsonWriter writer, JsonSchemaDto value, JsonSerializerOptions options)
    {
        value.Root.WriteTo(writer, options);
    }
}
