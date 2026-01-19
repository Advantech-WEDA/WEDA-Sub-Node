using ErrorOr;
using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Abstractions.Storage;

public interface IRecordingService
{
    bool ShouldRecord(string sensorId, int interval, long timestamp);

    Task RecordAsync(string sensorId, int interval, long timestamp, double value, CancellationToken cancellationToken = default);

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

    Task<ErrorOr<IReadOnlyList<string>>> GetSensorIdsAsync(CancellationToken cancellationToken = default);

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
    IReadOnlyList<double> Values);
