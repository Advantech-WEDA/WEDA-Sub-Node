using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Core.Storage;

/// <summary>
/// Reads recording data from binary files with header validation and version support.
/// </summary>
/// <remarks>
/// Supported file format versions:
/// - Version 1: 24-byte header (current)
///
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
public static class RecordingBinFileReader
{
    private const ushort CurrentVersion = 1;

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
        // Check minimum file size for header
        if (fs.Length < RecordingFileHeader.HeaderSize)
            return null;

        var headerBytes = new byte[RecordingFileHeader.HeaderSize];
        await fs.ReadExactlyAsync(headerBytes, cancellationToken);

        return ParseHeader(headerBytes);
    }

    /// <summary>
    /// Parses header bytes into a RecordingFileHeader.
    /// Returns null if validation fails.
    /// </summary>
    private static RecordingFileHeader? ParseHeader(byte[] headerBytes)
    {
        // Validate magic prefix "WEDA" (0x57454441)
        var prefix = BitConverter.ToUInt32(headerBytes, 0);
        if (prefix != RecordingFileHeader.Prefix)
            return null;

        var version = BitConverter.ToUInt16(headerBytes, 4);

        // Version-specific parsing
        // Future versions can be handled here
        return version switch
        {
            1 => ParseHeaderV1(headerBytes),
            _ => null // Unsupported version
        };
    }

    /// <summary>
    /// Parses Version 1 header format.
    /// </summary>
    private static RecordingFileHeader ParseHeaderV1(byte[] headerBytes)
    {
        return new RecordingFileHeader
        {
            Version = BitConverter.ToUInt16(headerBytes, 4),
            Flags = headerBytes[6],
            CheckSumType = headerBytes[7],
            Interval = BitConverter.ToUInt32(headerBytes, 8),
            StartTimestamp = BitConverter.ToUInt64(headerBytes, 12),
            SlotCount = BitConverter.ToUInt32(headerBytes, 20)
        };
    }

    /// <summary>
    /// Reads data points from a recording file within the specified time range.
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

        // Calculate slot range to read
        var startSlot = Math.Max(0, (int)((startMs - startOfDay) / interval));
        var endSlot = Math.Min(slotCount - 1, (int)((endMs - startOfDay) / interval));

        if (startSlot > endSlot || startSlot >= slotCount)
            return result;

        // Seek to start slot (header size + slot offset)
        var startPosition = RecordingFileHeader.HeaderSize + startSlot * sizeof(double);
        fs.Seek(startPosition, SeekOrigin.Begin);

        // Read slots
        var buffer = new byte[sizeof(double)];
        for (var slot = startSlot; slot <= endSlot; slot++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await fs.ReadExactlyAsync(buffer, cancellationToken);
            var value = BitConverter.ToDouble(buffer);
            var timestamp = startOfDay + (long)slot * interval;
            result.Add(new RecordingDataPoint(timestamp, value));
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
