namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// Data type identifier for index records.
/// When schemaType >= 0x20, the MIME flag in IndexFlags MUST also be set.
/// </summary>
public enum SchemaType : byte
{
    // Primitive Numeric types (0x00 - 0x02)
    Double = 0x00,
    Integer = 0x01,
    Long = 0x02,

    // Primitive Non-numeric types (0x03 - 0x1F)
    Boolean = 0x03,
    String = 0x10,

    // MIME types (0x20+)
    ImageJpeg = 0x20,
    ImagePng = 0x21,
    ApplicationJson = 0x30,
    ApplicationOctetStream = 0x31,
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
    public static SchemaType? ParseSchema(string? schema)
    {
        if (string.IsNullOrEmpty(schema))
            return null;

        var mime = ParseMimeSchema(schema);
        if (mime != null)
            return mime.Value;

        return schema.ToLowerInvariant() switch
        {
            "double" => SchemaType.Double,
            "integer" => SchemaType.Integer,
            "long" => SchemaType.Long,
            "boolean" => SchemaType.Boolean,
            "string" => SchemaType.String,
            _ => null
        };
    }
    public static bool IsMimeType(this SchemaType schemaType)
        => (byte)schemaType >= 0x20;

    public static bool IsPrimitiveNumericSchema(this SchemaType schemaType)
        => (byte)schemaType <= 0x02;

    public static bool IsPrimitiveNonNumericSchema(this SchemaType schemaType)
        => (byte)schemaType > 0x02
        && (byte)schemaType < 0x20;
}

public static class SchemaStringExtensions
{
    public static bool IsPrimitiveNumericSchema(this string? schema)
        => SchemaTypeExtensions.ParseSchema(schema)?.IsPrimitiveNumericSchema() == true;

    public static bool IsPrimitiveNonNumericSchema(this string? schema)
        => SchemaTypeExtensions.ParseSchema(schema)?.IsPrimitiveNonNumericSchema() == true;
}