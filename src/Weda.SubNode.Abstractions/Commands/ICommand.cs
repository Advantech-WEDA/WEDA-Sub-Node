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
}