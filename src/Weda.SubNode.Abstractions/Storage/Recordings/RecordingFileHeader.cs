namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// Binary file header for recording storage.
/// Supports both V1 (legacy, double-only) and V2 (multi-type) formats.
/// </summary>
/// <remarks>
/// V1 Header Layout (24 bytes):
/// <code>
/// ┌────────┬──────┬─────────┬─────────────────────────────────┐
/// │ Offset │ Size │ Type    │ Field                           │
/// ├────────┼──────┼─────────┼─────────────────────────────────┤
/// │ 0      │ 4    │ uint32  │ Prefix (0x57454441 "WEDA")      │
/// │ 4      │ 2    │ uint16  │ Version (= 1)                   │
/// │ 6      │ 1    │ byte    │ Flags (bit 0: Endianness)       │
/// │ 7      │ 1    │ byte    │ CheckSumType                    │
/// │ 8      │ 4    │ uint32  │ Interval (ms)                   │
/// │ 12     │ 8    │ ulong   │ StartTimestamp (Unix ms)        │
/// │ 20     │ 4    │ uint32  │ SlotCount                       │
/// └────────┴──────┴─────────┴─────────────────────────────────┘
/// </code>
///
/// V2 Header Layout (32 bytes):
/// <code>
/// ┌────────┬──────┬─────────┬─────────────────────────────────┐
/// │ Offset │ Size │ Type    │ Field                           │
/// ├────────┼──────┼─────────┼─────────────────────────────────┤
/// │ 0      │ 4    │ uint32  │ Prefix (0x57454441 "WEDA")      │
/// │ 4      │ 2    │ uint16  │ Version (= 2)                   │
/// │ 6      │ 1    │ byte    │ Flags (bit 0: Endianness)       │
/// │ 7      │ 1    │ byte    │ SchemaType                      │
/// │ 8      │ 4    │ uint32  │ Interval (ms)                   │
/// │ 12     │ 4    │ uint32  │ SlotSize (bytes)                │
/// │ 16     │ 8    │ ulong   │ StartTimestamp (Unix ms)        │
/// │ 24     │ 4    │ uint32  │ SlotCount                       │
/// │ 28     │ 4    │ uint32  │ HeaderChecksum (CRC32)          │
/// └────────┴──────┴─────────┴─────────────────────────────────┘
/// </code>
/// </remarks>
public class RecordingFileHeader
{
    /// <summary>
    /// Magic number "WEDA" (0x57454441) for file identification.
    /// </summary>
    public const uint Prefix = 0x57454441;

    /// <summary>
    /// V1 header size in bytes.
    /// </summary>
    public const int HeaderSizeV1 = 24;

    /// <summary>
    /// V2 header size in bytes.
    /// </summary>
    public const int HeaderSizeV2 = 32;

    /// <summary>
    /// Default slot size for V1 format (double = 8 bytes).
    /// </summary>
    public const int DefaultSlotSizeV1 = 8;

    /// <summary>
    /// File format version.
    /// </summary>
    public ushort Version { get; set; } = 2;

    /// <summary>
    /// Flags byte. Bit 0: Endianness (0 = Little Endian, 1 = Big Endian).
    /// </summary>
    public byte Flags { get; set; }

    /// <summary>
    /// Data type for V2 format. Defaults to Double for V1 compatibility.
    /// </summary>
    public SchemaType SchemaType { get; set; } = SchemaType.Double;

    /// <summary>
    /// Recording interval in milliseconds.
    /// </summary>
    public uint Interval { get; set; }

    /// <summary>
    /// Size of each slot in bytes. Derived from SchemaType for V2, fixed at 8 for V1.
    /// </summary>
    public uint SlotSize { get; set; } = DefaultSlotSizeV1;

    /// <summary>
    /// Start timestamp of the day (Unix milliseconds, UTC midnight).
    /// </summary>
    public ulong StartTimestamp { get; set; }

    /// <summary>
    /// Total number of slots in the file (86400000 / Interval).
    /// </summary>
    public uint SlotCount { get; set; }

    /// <summary>
    /// CRC32 checksum of the header (bytes 0-27) for V2 format.
    /// Used to verify header integrity.
    /// </summary>
    public uint HeaderChecksum { get; set; }

    /// <summary>
    /// Gets the header size based on the version.
    /// </summary>
    public int HeaderSize => Version == 1 ? HeaderSizeV1 : HeaderSizeV2;
}
