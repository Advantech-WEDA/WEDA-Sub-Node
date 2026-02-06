namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Configuration options for telemetry processing.
/// </summary>
public class TelemetryOptions
{
    /// <summary>
    /// Maximum allowed for binary/image data in bytes.
    /// Data exceeding this size will be rejected with an error.
    /// Default is 10MB (10,485,760 bytes).
    /// </summary>
    public int MaxBinarySize { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Maximum NATS message payload size (1MB - overhead margin)
    /// </summary>
    public int MaxMessageSize { get; set; } = 950 * 1024;

    /// <summary>
    /// Chunk size threshold for automatic chunking in bytes.
    /// Base64 strings larger than this will be automatically split into chunks.
    /// Default is 750KB (768,000 bytes) to stay under NATS 1MB payload limit.
    /// </summary>
    public int ChunkSize { get; set; } = 750 * 1024;

    /// <summary>
    /// Default telemetry options instance.
    /// </summary>
    public static TelemetryOptions Default => new();
}