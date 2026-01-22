namespace Weda.SubNode.Abstractions.Commands;

/// <summary>
/// Specifies the device command name for routing.
/// Apply this attribute to the Command class (not the Handler).
/// The command name is matched against the "deviceCmd" field in incoming request payload.
/// </summary>
/// <example>
/// <code>
/// [DeviceCmd("report")]
/// public record BatchReportCommand(...) : ICommand;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class DeviceCmdAttribute : Attribute
{
    /// <summary>
    /// Gets the device command name used for routing.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DeviceCmdAttribute"/>.
    /// </summary>
    /// <param name="name">The device command name for routing (e.g., "report").</param>
    public DeviceCmdAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }
}
