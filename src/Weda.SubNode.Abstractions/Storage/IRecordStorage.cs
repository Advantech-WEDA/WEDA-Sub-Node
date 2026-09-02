using Weda.SubNode.Abstractions.Common;
using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Abstractions.Storage;

public interface IRecordStorage
{
    /// <summary>
    /// Writes a single data point for a sensor.
    /// </summary>
    /// <param name="sensorId">The sensor ID.</param>
    /// <param name="interval">The recording interval in milliseconds.</param>
    /// <param name="schemaType">The data type of the value.</param>
    /// <param name="dataPoint">The data point to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteAsync(string sensorId, int interval, SchemaType schemaType, RecordingDataPoint dataPoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes multiple data points for a sensor.
    /// </summary>
    /// <param name="sensorId">The sensor ID.</param>
    /// <param name="interval">The recording interval in milliseconds.</param>
    /// <param name="schemaType">The data type of the values.</param>
    /// <param name="dataPoints">The data points to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteBatchAsync(string sensorId, int interval, SchemaType schemaType, IEnumerable<RecordingDataPoint> dataPoints, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads data points for a sensor within the specified time range.
    /// SchemaType is determined from the file header.
    /// </summary>
    /// <param name="sensorId">The sensor ID.</param>
    /// <param name="interval">The recording interval in milliseconds.</param>
    /// <param name="start">Start time of the range.</param>
    /// <param name="end">End time of the range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of data points.</returns>
    Task<IReadOnlyList<RecordingDataPoint>> ReadAsync(string sensorId, int interval, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads data points for a sensor across all intervals within the specified time range.
    /// </summary>
    /// <param name="sensorId">The sensor ID.</param>
    /// <param name="start">Start time of the range.</param>
    /// <param name="end">End time of the range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of data points sorted by timestamp.</returns>
    Task<IReadOnlyList<RecordingDataPoint>> ReadAllIntervalsAsync(string sensorId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sensor IDs (short IDs used for storage).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sensor IDs.</returns>
    Task<IReadOnlyList<string>> GetSensorIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sensor IDs with pagination support.
    /// </summary>
    /// <param name="pageIndex">The page index (0-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paged result containing sensor IDs (short IDs used for storage).</returns>
    Task<PagedResult<string>> GetSensorsAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the storage knows about a sensor, regardless of whether any
    /// recording data currently remains for it.
    /// </summary>
    /// <remarks>
    /// Distinguishes an unknown sensor from a known sensor whose files have all aged out of
    /// retention. Without that distinction an empty-but-known sensor is indistinguishable from
    /// a storage failure, and callers report a phantom error for a sensor that simply has no
    /// data. The default implementation derives the answer from <see cref="GetSensorIdsAsync"/>;
    /// implementations that can answer more cheaply should override it.
    /// </remarks>
    /// <param name="sensorId">The sensor ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the sensor is known to the storage; otherwise <c>false</c>.</returns>
    async Task<bool> SensorExistsAsync(string sensorId, CancellationToken cancellationToken = default)
    {
        var sensorIds = await GetSensorIdsAsync(cancellationToken);
        return sensorIds.Contains(sensorId, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets all recording intervals for a sensor.
    /// </summary>
    /// <param name="sensorId">The sensor ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of intervals in milliseconds.</returns>
    Task<IReadOnlyList<int>> GetIntervalsAsync(string sensorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all recordings older than the specified date.
    /// </summary>
    /// <param name="before">Delete recordings before this date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CleanupAsync(DateTimeOffset before, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all recordings for a sensor.
    /// </summary>
    /// <param name="sensorId">The sensor ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSensorAsync(string sensorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all recordings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}
