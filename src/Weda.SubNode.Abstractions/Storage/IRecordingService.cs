using ErrorOr;
using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Abstractions.Storage;

public interface IRecordingService
{
    bool ShouldRecord(string sensorId, int interval, long timestamp);

    Task RecordAsync(string sensorId, int interval, long timestamp, double value, CancellationToken cancellationToken = default);

    Task CleanupAsync(DateTimeOffset before, CancellationToken cancellationToken = default);

    Task<ErrorOr<IReadOnlyList<string>>> GetSensorIdsAsync(CancellationToken cancellationToken = default);

    Task<ErrorOr<RecordingResult>> GetRecordingsAsync(
        string sensorId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<Deleted>> DeleteSensorAsync(string sensorId, CancellationToken cancellationToken = default);

    Task<ErrorOr<Deleted>> DeleteAllAsync(CancellationToken cancellationToken = default);
}

public record RecordingResult(
    string SensorId,
    IReadOnlyList<RecordingMeasureResult> Measures);

public record RecordingMeasureResult(
    int Interval,
    long StartTimeStamp,
    IReadOnlyList<double> Values);
