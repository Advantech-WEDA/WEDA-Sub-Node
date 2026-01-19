using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Abstractions.Storage;

public interface IRecordStorage
{
    Task WriteAsync(string sensorId, int interval, RecordingDataPoint dataPoint, CancellationToken cancellationToken = default);

    Task WriteBatchAsync(string sensorId, int interval, IEnumerable<RecordingDataPoint> dataPoints, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecordingDataPoint>> ReadAsync(string sensorId, int interval, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecordingDataPoint>> ReadAllIntervalsAsync(string sensorId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSensorIdsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetIntervalsAsync(string sensorId, CancellationToken cancellationToken = default);

    Task CleanupAsync(DateTimeOffset before, CancellationToken cancellationToken = default);

    Task DeleteSensorAsync(string sensorId, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(CancellationToken cancellationToken = default);
}