using ErrorOr;
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

    Task CleanupAsync(DateTimeOffset before, CancellationToken cancellationToken = default);

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
}

public record RecordingResult(
    string SensorId,
    IReadOnlyList<RecordingMeasureResult> Measures);

public record RecordingMeasureResult(
    int Interval,
    long StartTimeStamp,
    SchemaType SchemaType,
    IReadOnlyList<object> Values);
