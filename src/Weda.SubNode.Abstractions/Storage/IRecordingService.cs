using ErrorOr;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Common;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Storage;

public interface IRecordingService
{
    bool ShouldRecord(string sensorId, int interval, long timestamp);

    /// <summary>
    /// Records a data point for a sensor.
    /// </summary>
    /// <param name="sensorId">The sensor ID.</param>
    /// <param name="interval">The recording interval in milliseconds.</param>
    /// <param name="schemaType">The data type of the value.</param>
    /// <param name="timestamp">The timestamp in Unix milliseconds.</param>
    /// <param name="value">The value to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordAsync(string sensorId, int interval, SchemaType schemaType, long timestamp, object value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any buffered recording data to storage.
    /// Should be called periodically or before shutdown to ensure data is persisted.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes buffered recording data for a specific sensor to storage.
    /// Should be called when sensor recording interval is changed to avoid mixing data from different intervals.
    /// </summary>
    /// <param name="sensorId">The sensor ID to flush</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task FlushSensorAsync(string sensorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every recording dated before <paramref name="before"/>.
    /// </summary>
    /// <param name="before">The cutoff; recordings older than this are removed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What the pass scanned, deleted and freed.</returns>
    Task<RecordingCleanupResult> CleanupAsync(DateTimeOffset before, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the ring-buffer FIFO limits, deleting the oldest recordings until the configured
    /// maximum storage size and minimum free disk space are both satisfied.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>What the pass scanned, deleted and freed.</returns>
    Task<RecordingCleanupResult> EnforceStorageLimitsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sensor short IDs that have recording data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sensor short IDs.</returns>
    Task<ErrorOr<IReadOnlyList<string>>> GetSensorIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sensors with pagination support.
    /// </summary>
    /// <param name="pageIndex">The page index (0-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paged result containing sensor DTOs.</returns>
    Task<ErrorOr<PagedResult<RecordingSensorDto>>> GetSensorsAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    Task<ErrorOr<RecordingResult>> GetRecordingsAsync(
        string sensorId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<Deleted>> DeleteSensorAsync(string sensorId, CancellationToken cancellationToken = default);

    Task<ErrorOr<Deleted>> DeleteAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the batch configuration settings at runtime.
    /// </summary>
    /// <param name="batchEnabled">Whether batch buffering is enabled</param>
    /// <param name="batchMaxSamples">Maximum samples per batch</param>
    void UpdateBatchSettings(bool batchEnabled, int batchMaxSamples);

    /// <summary>
    /// Enables or disables recording at runtime.
    /// When disabled, RecordAsync calls will be ignored.
    /// </summary>
    /// <param name="enabled">Whether recording is enabled</param>
    void SetEnabled(bool enabled);

    /// <summary>
    /// The retention and ring-buffer limits currently in force.
    /// </summary>
    RecordingStorageSettings StorageSettings { get; }

    /// <summary>
    /// Applies new retention and ring-buffer limits at runtime.
    /// </summary>
    /// <remarks>
    /// Takes effect on the next write and the next cleanup sweep - no restart. Callers that
    /// accept these values from outside the device must validate them first; an out-of-range
    /// value is rejected here rather than silently clamped.
    /// </remarks>
    /// <param name="settings">The limits to apply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settings"/> holds a value outside its range.</exception>
    void UpdateStorageSettings(RecordingStorageSettings settings);

    /// <summary>
    /// Captures the full recording state as a rollback snapshot, so the DeviceConfig transaction
    /// can restore it if any participant fails. Covers every field the service holds, including
    /// the ones outside <see cref="RecordingStorageSettings"/> (enabled, batch).
    /// </summary>
    /// <returns>A snapshot to hand back to <see cref="RestoreFromRollback"/>.</returns>
    RecordingOptions CaptureForRollback();

    /// <summary>
    /// Applies a cloud-pushed recording configuration at runtime, validating it first. This is the
    /// recording participant's apply step in the DeviceConfig transaction.
    /// </summary>
    /// <param name="record">The desired recording configuration from the cloud.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="record"/> holds a value outside its range.</exception>
    void ApplyRecordConfig(SubNodeRecordConfigDto record);

    /// <summary>
    /// Restores a snapshot captured by <see cref="CaptureForRollback"/>. This is the recording
    /// participant's rollback step in the DeviceConfig transaction.
    /// </summary>
    /// <param name="snapshot">The snapshot to restore.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    void RestoreFromRollback(RecordingOptions snapshot);
}

public record RecordingResult(
    string SensorId,
    IReadOnlyList<RecordingMeasureResult> Measures);

/// <summary>
/// A run of recorded values on one slot grid.
/// </summary>
/// <param name="Interval">Recording interval in milliseconds.</param>
/// <param name="StartTimeStamp">Unix timestamp in milliseconds of the first value.</param>
/// <param name="SchemaType">Data type of the values.</param>
/// <param name="Values">
/// One entry per slot: the value at index <c>i</c> was recorded at
/// <c>StartTimeStamp + i * Interval</c>. <c>null</c> marks a slot with no recording, such as a
/// failed read. The first and last entries are never <c>null</c>.
/// </param>
public record RecordingMeasureResult(
    int Interval,
    long StartTimeStamp,
    SchemaType SchemaType,
    IReadOnlyList<object?> Values);
