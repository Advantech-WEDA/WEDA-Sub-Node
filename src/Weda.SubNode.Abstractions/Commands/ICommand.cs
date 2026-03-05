namespace Weda.SubNode.Abstractions.Commands;

/// <summary>
/// Generic command interface with strongly-typed parameters.
/// </summary>
public interface ICommand<TParameter> : ICommand
{
    /// <summary>
    /// Command parameters.
    /// </summary>
    TParameter Parameters { get; set; }
}

/// <summary>
/// Non-generic marker interface for all commands.
/// Used as generic constraint in ICommandHandler and ICommandValidator
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Device request command composed of `cmdType` and `subCmd`. e.g., "report.historical", "report.data"
    /// </summary>
    string DeviceCmd { get; }

    /// <summary>
    /// Topic to response command execution result.
    /// </summary>
    string? RespTopic { get; set; }

    /// <summary>
    /// Command execution timeout in seconds.
    /// </summary>
    uint Timeout { get; set; }

}
