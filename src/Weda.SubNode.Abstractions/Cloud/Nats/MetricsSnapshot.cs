namespace Weda.SubNode.Abstractions.Cloud.Nats;

/// <summary>
/// A snapshot of performance metrics at a point in time
/// </summary>
public class MetricsSnapshot
{
    public long TotalMessagesProcessed { get; init; }
    public long TotalBatchesProcessed { get; init; }
    public long TotalProcessingErrors { get; init; }
    public long TotalAckSuccesses { get; init; }
    public long TotalNakSent { get; init; }
    public long TotalProcessingTimeMs { get; init; }
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Average processing time per message
    /// </summary>
    public double AverageProcessingTimePerMessage =>
        TotalMessagesProcessed > 0 ? (double)TotalProcessingTimeMs / TotalMessagesProcessed : 0;

    /// <summary>
    /// Average messages per batch
    /// </summary>
    public double AverageMessagesPerBatch =>
        TotalBatchesProcessed > 0 ? (double)TotalMessagesProcessed / TotalBatchesProcessed : 0;

    /// <summary>
    /// Error rate as percentage
    /// </summary>
    public double ErrorRate =>
        TotalMessagesProcessed > 0 ? (double)TotalProcessingErrors / TotalMessagesProcessed * 100 : 0;

    /// <summary>
    /// Success rate as percentage
    /// </summary>
    public double SuccessRate => 100 - ErrorRate;
}
