namespace Weda.SubNode.Abstractions.Commands.Attributes;

/// <summary>
/// Controls the automatic "Received" acknowledgment sent by CommandDispatcher.
/// By default, CommandDispatcher sends an auto ack when a command is received.
/// Set Enabled to false to suppress auto ack when the handler sends its own custom acknowledgment.
/// </summary>
/// <example>
/// <code>
/// [AutoAck(false)]
/// public class BatchReportCommandHandler : ICommandHandler&lt;BatchReportCommand, BatchReportResult&gt;
/// {
///     // Handler sends custom ack with estimated metrics
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class AutoAckAttribute(bool enabled = true) : Attribute
{
    /// <summary>
    /// Gets whether auto ack is enabled. Default is true.
    /// </summary>
    public bool Enabled { get; } = enabled;
}
