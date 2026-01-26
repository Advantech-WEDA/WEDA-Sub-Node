namespace Weda.SubNode.Abstractions.Commands;

/// <summary>
/// Marker interface for all commands that can be handled by the SubNode.
/// Each command must specify a unique DeviceCmd identifier for routing.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Gets the command identifier used for routing (e.g. "report").
    /// This value is matched against the "deviceCmd" field in incoming request payload.
    /// </summary>
    string DeviceCmd { get; }

    /// <summary>
    /// Gets or sets the sequence ID from the original command envelope.
    /// This is set by the CommandDispatcher after deserialization and used for response correlation.
    /// All responses (initial ack, progress, final) should use this same SeqId.
    /// </summary>
    ulong SeqId { get; set; }

    /// <summary>
    /// Gets or sets the request sequence ID from the original command envelope.
    /// This is set by the CommandDispatcher after deserialization and used for response correlation.
    /// </summary>
    string? ReqSeqId { get; set; }
}