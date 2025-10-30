namespace Weda.SubNode.Abstractions.Cloud.Nats;

/// <summary>
/// Performance metrics for JetStream message processing
/// </summary>
public class PerformanceMetrics
{
    private long _totalMessagesProcessed = 0;
    private long _totalBatchesProcessed = 0;
    private long _totalProcessingErrors = 0;
    private long _totalAckSuccesses = 0;
    private long _totalNakSent = 0;
    private long _totalProcessingTimeMs = 0;

    public long TotalMessagesProcessed => Interlocked.Read(ref _totalMessagesProcessed);
    public long TotalBatchesProcessed => Interlocked.Read(ref _totalBatchesProcessed);
    public long TotalProcessingErrors => Interlocked.Read(ref _totalProcessingErrors);
    public long TotalAckSuccesses => Interlocked.Read(ref _totalAckSuccesses);
    public long TotalNakSent => Interlocked.Read(ref _totalNakSent);
    public long TotalProcessingTimeMs => Interlocked.Read(ref _totalProcessingTimeMs);

    public void AddMessagesProcessed(long count) => Interlocked.Add(ref _totalMessagesProcessed, count);
    public void IncrementBatchesProcessed() => Interlocked.Increment(ref _totalBatchesProcessed);
    public void IncrementProcessingErrors() => Interlocked.Increment(ref _totalProcessingErrors);
    public void IncrementAckSuccesses() => Interlocked.Increment(ref _totalAckSuccesses);
    public void IncrementNakSent() => Interlocked.Increment(ref _totalNakSent);
    public void AddProcessingTime(long milliseconds) => Interlocked.Add(ref _totalProcessingTimeMs, milliseconds);

    public void Reset()
    {
        Interlocked.Exchange(ref _totalMessagesProcessed, 0);
        Interlocked.Exchange(ref _totalBatchesProcessed, 0);
        Interlocked.Exchange(ref _totalProcessingErrors, 0);
        Interlocked.Exchange(ref _totalAckSuccesses, 0);
        Interlocked.Exchange(ref _totalNakSent, 0);
        Interlocked.Exchange(ref _totalProcessingTimeMs, 0);
    }

    public MetricsSnapshot GetSnapshot() => new()
    {
        TotalMessagesProcessed = TotalMessagesProcessed,
        TotalBatchesProcessed = TotalBatchesProcessed,
        TotalProcessingErrors = TotalProcessingErrors,
        TotalAckSuccesses = TotalAckSuccesses,
        TotalNakSent = TotalNakSent,
        TotalProcessingTimeMs = TotalProcessingTimeMs,
        Timestamp = DateTime.UtcNow
    };
}
