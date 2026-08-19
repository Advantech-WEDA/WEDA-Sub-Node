using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Weda.SubNode.Cloud.Serialization;

/// <summary>
/// Recursively rewrites every JSON object property key to camelCase using
/// <see cref="JsonNamingPolicy.CamelCase"/>.<c>ConvertName</c>, applied through
/// nested objects and arrays. Scalar values and array element order are preserved
/// verbatim; only object keys are transformed.
/// </summary>
/// <remarks>
/// <para>
/// Rationale: the cloud-side validator for schemaVersion=2 enforces STRICT camelCase
/// on the device's reported configuration payload. Two things in the outbound payload
/// are NOT covered by <see cref="JsonNamingPolicy"/> alone:
/// </para>
/// <list type="number">
///   <item>the raw-echoed devicecfg (a <see cref="JsonElement"/> written verbatim,
///   which preserves the author's PascalCase);</item>
///   <item>dictionary KEYS (e.g. a sensor's <c>Parameters</c>, <c>Metadata</c>,
///   <c>DeviceInfo</c>) — dictionary keys are only touched when a
///   <c>DictionaryKeyPolicy</c> is set, and never at all for raw <see cref="JsonElement"/>s.</item>
/// </list>
/// <para>
/// This normalizer runs over the fully serialized bytes as a final pass, so the DTO
/// envelope, the raw echo, and all dictionary keys come out camelCase in one shot.
/// </para>
/// <para>
/// IMPORTANT — map-entry keys: a raw <see cref="JsonElement"/> carries no schema, so
/// there is no generic way to tell a <em>structural</em> property key (e.g.
/// <c>deviceConfigs</c>, <c>sensors</c>) apart from a <em>data</em> key whose value is
/// itself a dictionary entry (e.g. a device-config entry name such as
/// <c>SystemAgentDeviceConfig</c>, or an arbitrary <c>Metadata</c> key). This normalizer
/// therefore transforms ALL object keys. In practice that is the desired behavior here:
/// the cloud validator wants camelCase everywhere, device names are resolved
/// case-insensitively downstream, and <c>ConvertName</c> only lowercases the leading
/// upper-case run (<c>SystemAgentDeviceConfig</c> -> <c>systemAgentDeviceConfig</c>),
/// leaving embedded casing/underscores intact. This trade-off is exercised explicitly
/// in the unit tests.
/// </para>
/// <para>
/// The transform is idempotent: keys that are already camelCase (including the injected
/// <c>sdkVersion</c> / <c>schemaVersion</c> keys) are returned unchanged.
/// </para>
/// </remarks>
public static class CamelCaseJsonNormalizer
{
    /// <summary>
    /// Serializes <paramref name="value"/> with <paramref name="options"/>, then returns
    /// the equivalent UTF-8 JSON bytes with every object key converted to camelCase.
    /// </summary>
    public static byte[] SerializeAndNormalize<T>(T value, JsonSerializerOptions? options = null)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, options);
        return NormalizeToUtf8Bytes(bytes);
    }

    /// <summary>
    /// Returns UTF-8 JSON bytes equivalent to <paramref name="utf8Json"/> with every
    /// object property key converted to camelCase (recursively).
    /// </summary>
    public static byte[] NormalizeToUtf8Bytes(ReadOnlySpan<byte> utf8Json)
    {
        using var doc = JsonDocument.Parse(utf8Json.ToArray());
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteNormalized(doc.RootElement, writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Returns a JSON string equivalent to <paramref name="json"/> with every object
    /// property key converted to camelCase (recursively). Convenience overload for tests
    /// and diagnostics.
    /// </summary>
    public static string Normalize(string json)
    {
        var bytes = NormalizeToUtf8Bytes(Encoding.UTF8.GetBytes(json));
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Returns a new <see cref="JsonElement"/> equivalent to <paramref name="element"/>
    /// with every object property key converted to camelCase (recursively).
    /// </summary>
    public static JsonElement Normalize(JsonElement element)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteNormalized(element, writer);
        }

        using var doc = JsonDocument.Parse(buffer.WrittenMemory);
        return doc.RootElement.Clone();
    }

    private static void WriteNormalized(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(JsonNamingPolicy.CamelCase.ConvertName(property.Name));
                    WriteNormalized(property.Value, writer);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteNormalized(item, writer);
                }

                writer.WriteEndArray();
                break;

            default:
                // Scalars (string/number/bool/null) are written verbatim.
                element.WriteTo(writer);
                break;
        }
    }
}