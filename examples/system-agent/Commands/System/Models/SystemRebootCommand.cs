using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.System.Models;

/// <summary>
/// Command to reboot the system.
/// Maps to payload: data.deviceCmd = "system.reboot"
/// </summary>
/// <remarks>
/// Cloud → SubNode command structure:
/// <code>
/// {
///   "deviceCmd": "system.reboot",
///   "timeout": 30,
///   "respTopic": "...",
///   "parameters": {
///     "delaySeconds": 5
///   }
/// }
/// </code>
/// </remarks>
[DeviceCmd("system.reboot")]
public class SystemRebootCommand : CommandData<SystemCommandParameters>
{
}
