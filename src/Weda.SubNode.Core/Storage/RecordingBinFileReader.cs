using System.IO.Hashing;
using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Core.Storage;

/// <summary>
/// Reads recording data from binary files with header validation and version support.
/// </summary>
/// <remarks>
/// Supported file format versions:
/// - Version 1: 24-byte header, double-only (legacy)
/// - Version 2: 32-byte header, multi-type support
///
/// V1 Header Layout (24 bytes):
/// <code>
/// ┌────────┬──────┬─────────┬─────────────────────────────────┐
/// │ Offset │ Size │ Type    │ Field                           │
/// ├────────┼──────┼─────────┼─────────────────────────────────┤
/// │ 0      │ 4    │ uint32  │ Prefix (0x57454441 "WEDA")      │
/// │ 4      │ 2    │ uint16  │ Version (= 1)                   │
/// │ 6      │ 1    │ byte    │ Flags                           │
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
/// │ 6      │ 1    │ byte    │ Flags                           │
/// │ 7      │ 1    │ byte    │ SchemaType                      │
/// │ 8      │ 4    │ uint32  │ Interval (ms)                   │
/// │ 12     │ 4    │ uint32  │ SlotSize (bytes)                │
/// │ 16     │ 8    │ ulong   │ StartTimestamp (Unix ms)        │
/// │ 24     │ 4    │ uint32  │ SlotCount                       │
/// │ 28     │ 4    │ uint32  │ HeaderChecksum (CRC32)          │
/// └────────┴──────┴─────────┴─────────────────────────────────┘
/// </code>
/// </remarks>
public static class RecordingBinFileReader
{
    /// <summary>
    /// Reads the header from a recording file.
    /// Returns null if the file format is invalid or unsupported.
    /// </summary>
    public static async Task<RecordingFileHeader?> ReadHeaderAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return null;

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await ReadHeaderAsync(fs, cancellationToken);
    }

    /// <summary>
    /// Reads the header from a file stream.
    /// Returns null if the file format is invalid or unsupported.
    /// </summary>
    public static async Task<RecordingFileHeader?> ReadHeaderAsync(FileStream fs, CancellationToken cancellationToken = default)
    {
        // Read minimum bytes to determine version (first 6 bytes: prefix + version)
        if (fs.Length < 6)
            return null;

        var prefixAndVersion = new byte[6];
        await fs.ReadExactlyAsync(prefixAndVersion, cancellationToken);

        // Validate magic prefix
        var prefix = BitConverter.ToUInt32(prefixAndVersion, 0);
        if (prefix != RecordingFileHeader.Prefix)
            return null;

        var version = BitConverter.ToUInt16(prefixAndVersion, 4);

        // Determine header size based on version
        var headerSize = version switch
        {
            1 => RecordingFileHeader.HeaderSizeV1,
            2 => RecordingFileHeader.HeaderSizeV2,
            _ => 0
        };

        if (headerSize == 0 || fs.Length < headerSize)
            return null;

        // Read remaining header bytes
        fs.Seek(0, SeekOrigin.Begin);
        var headerBytes = new byte[headerSize];
        await fs.ReadExactlyAsync(headerBytes, cancellationToken);

        return ParseHeader(headerBytes, version);
    }

    /// <summary>
    /// Parses header bytes into a RecordingFileHeader.
    /// Returns null if validation fails.
    /// </summary>
    private static RecordingFileHeader? ParseHeader(byte[] headerBytes, ushort version)
    {
        return version switch
        {
            1 => ParseHeaderV1(headerBytes),
            2 => ParseHeaderV2(headerBytes),
            _ => null
        };
    }

    /// <summary>
    /// Parses Version 1 header format (legacy, double-only).
    /// </summary>
    private static RecordingFileHeader ParseHeaderV1(byte[] bytes) => new()
    {
        Version = 1,
        Flags = bytes[6],
        SchemaType = SchemaType.Double, // V1 is always double
        Interval = BitConverter.ToUInt32(bytes, 8),
        SlotSize = RecordingFileHeader.DefaultSlotSizeV1, // V1 is always 8 bytes
        StartTimestamp = BitConverter.ToUInt64(bytes, 12),
        SlotCount = BitConverter.ToUInt32(bytes, 20)
    };

    /// <summary>
    /// Parses Version 2 header format (multi-type support).
    /// Returns null if checksum validation fails.
    /// </summary>
    private static RecordingFileHeader? ParseHeaderV2(byte[] bytes)
    {
        // Validate checksum (CRC32 of bytes 0-27)
        var storedChecksum = BitConverter.ToUInt32(bytes, 28);
        var calculatedChecksum = Crc32.HashToUInt32(bytes.AsSpan(0, 28));

        if (storedChecksum != calculatedChecksum)
            return null; // Checksum mismatch, header corrupted

        return new RecordingFileHeader
        {
            Version = 2,
            Flags = bytes[6],
            SchemaType = (SchemaType)bytes[7],
            Interval = BitConverter.ToUInt32(bytes, 8),
            SlotSize = BitConverter.ToUInt32(bytes, 12),
            StartTimestamp = BitConverter.ToUInt64(bytes, 16),
            SlotCount = BitConverter.ToUInt32(bytes, 24),
            HeaderChecksum = storedChecksum
        };
    }

    /// <summary>
    /// Reads data points from a recording file within the specified time range.
    /// Supports both V1 and V2 file formats.
    /// </summary>
    /// <param name="filePath">Path to the recording file</param>
    /// <param name="interval">Expected recording interval in milliseconds</param>
    /// <param name="startMs">Start timestamp (Unix milliseconds)</param>
    /// <param name="endMs">End timestamp (Unix milliseconds)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of data points, or empty list if file is invalid</returns>
    public static async Task<IReadOnlyList<RecordingDataPoint>> ReadDataPointsAsync(
        string filePath,
        int interval,
        long startMs,
        long endMs,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return [];

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Read and validate header
        var header = await ReadHeaderAsync(fs, cancellationToken);
        if (header == null)
            return []; // Invalid or unsupported file format

        return await ReadDataPointsFromStreamAsync(fs, header, interval, startMs, endMs, cancellationToken);
    }

    /// <summary>
    /// Reads data points from an already opened file stream with a validated header.
    /// </summary>
    private static async Task<IReadOnlyList<RecordingDataPoint>> ReadDataPointsFromStreamAsync(
        FileStream fs,
        RecordingFileHeader header,
        int interval,
        long startMs,
        long endMs,
        CancellationToken cancellationToken)
    {
        var result = new List<RecordingDataPoint>();
        var startOfDay = (long)header.StartTimestamp;
        var slotCount = (int)header.SlotCount;
        var slotSize = (int)header.SlotSize;
        var schemaType = header.SchemaType;

        // Calculate slot range to read
        var startSlot = Math.Max(0, (int)((startMs - startOfDay) / interval));
        var endSlot = Math.Min(slotCount - 1, (int)((endMs - startOfDay) / interval));

        if (startSlot > endSlot || startSlot >= slotCount)
            return result;

        // Seek to start slot (header size + slot offset)
        var startPosition = header.HeaderSize + startSlot * slotSize;
        fs.Seek(startPosition, SeekOrigin.Begin);

        // Read slots
        var buffer = new byte[slotSize];
        for (var slot = startSlot; slot <= endSlot; slot++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await fs.ReadExactlyAsync(buffer, cancellationToken);

            // Skip empty slots
            if (schemaType.IsEmptySlot(buffer))
                continue;

            var value = schemaType.FromBytes(buffer);
            var timestamp = startOfDay + (long)slot * interval;
            result.Add(new RecordingDataPoint(timestamp, value, schemaType));
        }

        return result;
    }

    /// <summary>
    /// Validates if a file is a valid recording file.
    /// </summary>
    public static async Task<bool> IsValidFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var header = await ReadHeaderAsync(filePath, cancellationToken);
        return header != null;
    }
}
