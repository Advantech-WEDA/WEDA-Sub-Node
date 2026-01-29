namespace Weda.SubNode.Abstractions.Commands;

/// <summary>
/// Encasulates the raw command data received from cloud.
/// </summary>
public class CommandEnvelope
{
    /// <summary>
    /// The name of the command.
    /// </summary>
    public required string CommandName { get; init;}

    /// <summary>
    /// The Sequence ID to track the command.
    /// </summary>
    public ulong SeqId { get; init; }

    /// <summary>
    /// The Request Sequence ID to track the command.
    /// </summary>
    public string? ReqSeqId { get; init; }

    /// <summary>
    /// The timestamp when the command was created.
    /// </summary>
    public ulong Timestamp { get; init; }

    /// <summary>
    /// The raw JSON payload of the command.
    /// </summary>
    public object? Data { get; init; }
}