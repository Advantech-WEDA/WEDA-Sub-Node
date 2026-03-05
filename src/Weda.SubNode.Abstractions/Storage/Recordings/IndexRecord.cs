using System.Runtime.InteropServices;

namespace Weda.SubNode.Abstractions.Storage.Recordings;

/*
    | Offset | Field       | Type   | Size |
    |--------|-------------|--------|------|
    |    0   | Timestamp   | int64  |   8  |
    |    8   | Offset      | int64  |   8  |
    |   16   | Size        | int32  |   4  |
    |   20   | PayloadCrc  | uint32 |   4  |
    |   24   | Flags       | uint8  |   1  |
    |   25   | SchemaType  | uint8  |   1  |
    |   26   | SeqNum      | uint16 |   2  |
    |   28   | IndexCrc    | uint32 |   4  |
    | Total  |             |        |  32  |
 */


/// <summary>
/// Fixed-size 32-byte index record for dynamic record storage.
/// Enables logarithmic time complexity via binarch search by timestamp.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct IndexRecord(
    long timestamp,
    long offset,
    int payloadSize,
    uint payloadCrc,
    IndexFlags flags,
    SchemaType schemaType,
    ushort seqNum,
    uint indexCrc)
{
    public const int Size = 32;

    /// <summary>Primary sort key (Unix ms). Enables O(log n) binary search.</summary>
    public readonly long Timestamp = timestamp;

    /// <summary>Byte address in the .dat file where the data frame starts.</summary>
    public readonly long Offset = offset;

    /// <summary>Length of the payload in bytes (excluding frame header). Max ~2GB.</summary>
    public readonly int PayloadSize = payloadSize;

    /// <summary>CRC32 of the raw payload bytes. Used for corruption detection.</summary>
    public readonly uint PayloadCrc = payloadCrc;

    /// <summary>Bitmask flags (MIME, Compressed, Encrypted, Deleted).</summary>
    public readonly IndexFlags Flags = flags;

    /// <summary>Data type enum. Enables type-based filtering without reading payload.</summary>
    public readonly SchemaType SchemaType = schemaType;

    /// <summary>Monotonic counter within same timestamp. Disambiguates multiple records at same ms.</summary>
    public readonly ushort SeqNum = seqNum;

    /// <summary>CRC32 of bytes [0..27]. Self-integrity check for the index entry.</summary>
    public readonly uint IndexCrc = indexCrc;
}