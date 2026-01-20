namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// Binary file header for recording storage.
/// </summary>
/// <remarks>
/// Header Layout (24 bytes):
/// <code>
/// ┌────────┬──────┬─────────┬─────────────────────────────────┐
/// │ Offset │ Size │ Type    │ Field                           │
/// ├────────┼──────┼─────────┼─────────────────────────────────┤
/// │ 0      │ 4    │ uint32  │ Prefix (0x57454441 "WEDA")      │
/// │ 4      │ 2    │ uint16  │ Version                         │
/// │ 6      │ 1    │ byte    │ Flags (bit 0: Endianness)       │
/// │ 7      │ 1    │ byte    │ CheckSumType                    │
/// │ 8      │ 4    │ uint32  │ Interval (ms)                   │
/// │ 12     │ 8    │ ulong   │ StartTimestamp (Unix ms)        │
/// │ 20     │ 4    │ uint32  │ SlotCount                       │
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
    /// Total header size in bytes.
    /// </summary>
    public const int HeaderSize = 24;

    /// <summary>
    /// File format version.
    /// </summary>
    public ushort Version { get; set; } = 1;

    /// <summary>
    /// Flags byte. Bit 0: Endianness (0 = Little Endian, 1 = Big Endian).
    /// </summary>
    public byte Flags { get; set; }

    /// <summary>
    /// Checksum type. 0 = None, 1 = CRC32 (reserved for future use).
    /// </summary>
    public byte CheckSumType { get; set; }

    /// <summary>
    /// Recording interval in milliseconds.
    /// </summary>
    public uint Interval { get; set; }

    /// <summary>
    /// Start timestamp of the day (Unix milliseconds, UTC midnight).
    /// </summary>
    public ulong StartTimestamp { get; set; }

    /// <summary>
    /// Total number of slots in the file (86400000 / Interval).
    /// </summary>
    public uint SlotCount { get; set; }
}