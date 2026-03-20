namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// Data type identifier for index records.
/// When schemaType >= 0x10, the MIME flag in IndexFlags MUST also be set.
/// </summary>
public enum SchemaType : byte
{
    // Primitives types (0x00 - 0x0F)
    Double = 0x00,
    Integer = 0x01,
    Long = 0x02,
    String = 0x03,
    Boolean = 0x04,

    // MIME types (0x10+)
    ImageJpeg = 0x10,
    ImagePng = 0x11,
    ApplicationJson = 0x20,
    ApplicationOctetStream = 0x21,
}

public static class SchemaTypeExtensions
{
    public static SchemaType? ParseMimeSchema(string? schema)
    {
        if (string.IsNullOrEmpty(schema))
            return null;
        
        return schema.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => SchemaType.ImageJpeg,
            "image/png" => SchemaType.ImagePng,
            "application/json" => SchemaType.ApplicationJson,
            "application/octet-stream" => SchemaType.ApplicationOctetStream,
            _ => null
        };
    }

    public static bool IsMimeType(this SchemaType schemaType)
        => (byte)schemaType >= 0x10;

    public static bool IsNumericSchema(string? schema)
    {
        if (string.IsNullOrEmpty(schema))
            return false;

        return schema.Equals("double", StringComparison.OrdinalIgnoreCase)
            || schema.Equals("integer", StringComparison.OrdinalIgnoreCase)
            || schema.Equals("long", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPrimitiveNonNumericSchema(string? schema)
    {
        if (string.IsNullOrEmpty(schema))
            return false;

        return schema.Equals("string", StringComparison.OrdinalIgnoreCase)
            || schema.Equals("boolean", StringComparison.OrdinalIgnoreCase);
    }
}