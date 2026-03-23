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
    // Slot sizes lookup table: index by (byte)SchemaType, -1 means not slot-based
    // [0]=Double:8, [1]=Integer:4, [2]=Long:8, [3]=Boolean:1
    private static readonly int[] SlotSizes = [8, 4, 8, 1];

    // Pre-computed empty slot bytes for each slot-based SchemaType
    private static readonly byte[][] EmptySlotBytes =
    [
        BitConverter.GetBytes(double.NaN),      // Double
        BitConverter.GetBytes(int.MinValue),    // Integer
        BitConverter.GetBytes(long.MinValue),   // Long
        [0xFF]                                  // Boolean
    ];

    public static SchemaType? ParseSchema(string? schema)
    {
        if (string.IsNullOrEmpty(schema))
            return null;

        return schema.ToLowerInvariant() switch
        {
            // MIME schemas
            "image/jpeg" or "image/jpg" => SchemaType.ImageJpeg,
            "image/png" => SchemaType.ImagePng,
            "application/json" => SchemaType.ApplicationJson,
            "application/octet-stream" => SchemaType.ApplicationOctetStream,

            // Primitive schemas
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

    /// <summary>
    /// Checks if this SchemaType is supported by slot-based (fixed-size) storage.
    /// Slot-based types are: Double (0x00), Integer (0x01), Long (0x02), Boolean (0x03).
    /// </summary>
    public static bool IsSlotBasedSchema(this SchemaType schemaType)
        => (byte)schemaType <= 0x03;

    /// <summary>
    /// Gets the slot size in bytes for this SchemaType.
    /// Only valid for slot-based schemas (Double, Long, Integer, Boolean).
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown if SchemaType is not slot-based.</exception>
    public static int GetSlotSize(this SchemaType schemaType)
        => schemaType.IsSlotBasedSchema()
            ? SlotSizes[(byte)schemaType]
            : throw new NotSupportedException($"SchemaType {schemaType} is not supported for slot-based storage");

    /// <summary>
    /// Gets the empty slot bytes for this SchemaType (used to initialize slots as "no data").
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown if SchemaType is not slot-based.</exception>
    public static byte[] GetEmptySlotBytes(this SchemaType schemaType)
        => schemaType.IsSlotBasedSchema()
            ? EmptySlotBytes[(byte)schemaType]
            : throw new NotSupportedException($"SchemaType {schemaType} is not supported for slot-based storage");

    /// <summary>
    /// Converts a value to bytes for this SchemaType.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown if SchemaType is not slot-based.</exception>
    public static byte[] ToBytes(this SchemaType schemaType, object value) => schemaType switch
    {
        SchemaType.Double => BitConverter.GetBytes(Convert.ToDouble(value)),
        SchemaType.Long => BitConverter.GetBytes(Convert.ToInt64(value)),
        SchemaType.Integer => BitConverter.GetBytes(Convert.ToInt32(value)),
        SchemaType.Boolean => [(byte)(Convert.ToBoolean(value) ? 1 : 0)],
        _ => throw new NotSupportedException($"SchemaType {schemaType} is not supported for slot-based storage")
    };

    /// <summary>
    /// Converts bytes to a value for this SchemaType.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown if SchemaType is not slot-based.</exception>
    public static object FromBytes(this SchemaType schemaType, ReadOnlySpan<byte> bytes) => schemaType switch
    {
        SchemaType.Double => BitConverter.ToDouble(bytes),
        SchemaType.Long => BitConverter.ToInt64(bytes),
        SchemaType.Integer => BitConverter.ToInt32(bytes),
        SchemaType.Boolean => bytes[0] == 1,
        _ => throw new NotSupportedException($"SchemaType {schemaType} is not supported for slot-based storage")
    };

    /// <summary>
    /// Checks if the given bytes represent an empty slot for this SchemaType.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown if SchemaType is not slot-based.</exception>
    public static bool IsEmptySlot(this SchemaType schemaType, ReadOnlySpan<byte> bytes) => schemaType switch
    {
        SchemaType.Double => double.IsNaN(BitConverter.ToDouble(bytes)),
        SchemaType.Long => BitConverter.ToInt64(bytes) == long.MinValue,
        SchemaType.Integer => BitConverter.ToInt32(bytes) == int.MinValue,
        SchemaType.Boolean => bytes[0] == 0xFF,
        _ => throw new NotSupportedException($"SchemaType {schemaType} is not supported for slot-based storage")
    };
}

public static class SchemaStringExtensions
{
    public static bool IsPrimitiveNumericSchema(this string? schema)
        => SchemaTypeExtensions.ParseSchema(schema)?.IsPrimitiveNumericSchema() == true;

    public static bool IsPrimitiveNonNumericSchema(this string? schema)
        => SchemaTypeExtensions.ParseSchema(schema)?.IsPrimitiveNonNumericSchema() == true;
}