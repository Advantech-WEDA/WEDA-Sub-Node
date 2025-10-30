namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Event raised when a retry attempt is about to occur.
/// </summary>
public sealed record RetryAttemptEvent(
    string DeviceId,
    string OperationName,
    int AttemptNumber,
    int MaxAttempts,
    TimeSpan Delay,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Gets the exception that caused the retry.
    /// </summary>
    public Exception? LastException { get; init; }

    /// <summary>
    /// Gets the reason for the retry.
    /// </summary>
    public string? Reason { get; init; }
}
