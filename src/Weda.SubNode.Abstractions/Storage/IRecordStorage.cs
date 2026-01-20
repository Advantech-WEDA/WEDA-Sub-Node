using Weda.SubNode.Abstractions.Common;
using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Abstractions.Storage;

public interface IRecordStorage
{
    Task WriteAsync(string sensorId, int interval, RecordingDataPoint dataPoint, CancellationToken cancellationToken = default);

    Task WriteBatchAsync(string sensorId, int interval, IEnumerable<RecordingDataPoint> dataPoints, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecordingDataPoint>> ReadAsync(string sensorId, int interval, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);

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

    Task<IReadOnlyList<int>> GetIntervalsAsync(string sensorId, CancellationToken cancellationToken = default);

    Task CleanupAsync(DateTimeOffset before, CancellationToken cancellationToken = default);

    Task DeleteSensorAsync(string sensorId, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}