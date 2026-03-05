using System.Buffers.Binary;

namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// Unified file header for both fixed-size (BinaryRecordStorage) and dynamic-size (DynamicRecordStorage) files.
/// Total size: 32 bytes.
/// </summary>
/// <remarks>
/// Layout:
/// | Offset | Field       | Type   | Size | Description                    |
/// |--------|-------------|--------|------|--------------------------------|
/// |    0   | Magic       | uint32 |   4  | 0x49445831 ("IDX1")            |
/// |    4   | Version     | uint16 |   2  | File format version            |
/// |    6   | Flags       | uint8  |   1  | File-level flags               |
/// |    7   | SchemaType  | uint8  |   1  | Data type for all records      |
/// |    8   | SlotSize    | uint16 |   2  | Bytes per slot (0=dynamic)     |
/// |   10   | Reserved    | byte[] |   6  | Future use                     |
/// |   16   | Interval    | uint32 |   4  | Reporting interval (ms)        |
/// |   20   | StartTime   | int64  |   8  | Start of day timestamp (ms)    |
/// |   28   | RecordCount | uint32 |   4  | Number of records/slots        |
/// </remarks>
public readonly record struct IndexFileHeader(
    uint Magic,
    ushort Version,
    byte Flags,
    SchemaType SchemaType,
    ushort SlotSize,
    uint Interval,
    long StartTime,
    uint RecordCount
)
{
    public const int Size = 32;
    public const uint MagicValue = 0x49445831; // "IDX1" in little-endian
    public const ushort CurrentVersion = 2;

    // SlotSize constants for fixed-size types
    public const ushort SlotSizeDynamic = 0;
    public const ushort SlotSizeBoolean = 1;
    public const ushort SlotSizeInt32 = 4;
    public const ushort SlotSizeInt64 = 8;
    public const ushort SlotSizeDouble = 8;

    public bool IsDynamic => SlotSize == SlotSizeDynamic;
    public bool IsFixedSize => SlotSize > 0;

    public static IndexFileHeader CreateForDynamic(SchemaType schemaType)
        => new(MagicValue, CurrentVersion, 0, schemaType, SlotSizeDynamic, 0, 0, 0);

    public static IndexFileHeader CreateForFixedSize(SchemaType schemaType, ushort slotSize, uint interval, long startTime, uint recordCount)
        => new(MagicValue, CurrentVersion, 0, schemaType, slotSize, interval, startTime, recordCount);

    public static ushort GetSlotSizeForType(SchemaType schemaType) => schemaType switch
    {
        SchemaType.Boolean => SlotSizeBoolean,
        SchemaType.Integer => SlotSizeInt32,
        SchemaType.Long => SlotSizeInt64,
        SchemaType.Double => SlotSizeDouble,
        _ => SlotSizeDynamic
    };

    public void WriteTo(Span<byte> buffer)
    {
        if (buffer.Length < Size)
            throw new ArgumentException($"Buffer must be at least {Size} bytes", nameof(buffer));

        BinaryPrimitives.WriteUInt32LittleEndian(buffer[0..], Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[4..], Version);
        buffer[6] = Flags;
        buffer[7] = (byte)SchemaType;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer[8..], SlotSize);
        buffer[10..16].Clear(); // Reserved
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[16..], Interval);
        BinaryPrimitives.WriteInt64LittleEndian(buffer[20..], StartTime);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer[28..], RecordCount);
    }

    public static IndexFileHeader ReadFrom(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < Size)
            throw new ArgumentException($"Buffer must be at least {Size} bytes", nameof(buffer));

        return new IndexFileHeader(
            Magic: BinaryPrimitives.ReadUInt32LittleEndian(buffer[0..]),
            Version: BinaryPrimitives.ReadUInt16LittleEndian(buffer[4..]),
            Flags: buffer[6],
            SchemaType: (SchemaType)buffer[7],
            SlotSize: BinaryPrimitives.ReadUInt16LittleEndian(buffer[8..]),
            Interval: BinaryPrimitives.ReadUInt32LittleEndian(buffer[16..]),
            StartTime: BinaryPrimitives.ReadInt64LittleEndian(buffer[20..]),
            RecordCount: BinaryPrimitives.ReadUInt32LittleEndian(buffer[28..])
        );
    }

    public bool IsValid() => Magic == MagicValue && Version <= CurrentVersion;
}
