namespace Weda.SubNode.Abstractions.Storage;

public interface IRecordingService
{   
    bool ShouldRecord(string sensorId, int interval, long timestamp);

    Task RecordAsync(string sensorId, int interval, long timestamp, double value, CancellationToken cancellationToken = default);

    Task CleanupAsync(DateTimeOffset before, CancellationToken cancellationToken = default);
}