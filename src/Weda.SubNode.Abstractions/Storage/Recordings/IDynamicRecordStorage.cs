namespace Weda.SubNode.Abstractions.Storage.Recordings;

/// <summary>
/// Interface for dynamic record storage with variable-size payloads.
/// Uses two-file architecture: .idx (fix-size index) + .dat (append-only data)
/// </summary>
public interface IDynamicRecordStorage
{
    /// <summary>
    /// Appends a record to storage.
    /// Write order: data -> fsync -> index -> fsync (crash-safe)
    /// </summary>
    /// <param name="sensorId">Sensor short resource ID</param>
    /// <param name="timestamp">Record timestamp (Unix ms)</param>
    /// <param name="payload">Raw payload bytes</param>
    /// <param name="schemaType">Data Type Identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AppendAsync(string sensorId, long timestamp, ReadOnlyMemory<byte> payload, SchemaType schemaType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a record by exact timestamp match.
    /// </summary>
    /// <param name="sensorId">Sensor short resource ID</param>
    /// <param name="timestamp">Record timestamp (Unit ms)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Record payload or null if not found</returns>
    Task<StorageRecord?> ReadAsync(string sensorId, long timestamp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads records within a time range
    /// </summary>
    /// <param name="sensorId">Sensor short resource ID</param>
    /// <param name="startTime">Start timestamp (inclusive)</param>
    /// <param name="endTime">End timestamp (inclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of records in the range</returns>
    Task<IReadOnlyList<StorageRecord>> ReadRangeAsync(string sensorId, long startTime, long endTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes files older than the specified cutoff date.
    /// </summary>
    /// <param name="cutoffDate">Files with dates before this will be deleted</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of file pairs (.idx + .dat) deleted</returns>
    Task<int> CleanupAsync(DateTimeOffset cutoffDate, CancellationToken cancellationToken = default);
}