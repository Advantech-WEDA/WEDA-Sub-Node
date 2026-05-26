using System.ComponentModel.DataAnnotations;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.System.Models;

/// <summary>
/// Command to shut down the system.
/// Maps to payload: data.deviceCmd = "system.shutdown"
/// </summary>
/// <remarks>
/// Cloud → SubNode command structure:
/// <code>
/// {
///   "deviceCmd": "system.shutdown",
///   "timeout": 30,
///   "respTopic": "...",
///   "parameters": {
///     "delaySeconds": 5
///   }
/// }
/// </code>
/// </remarks>
[DeviceCmd("system.shutdown")]
[Display(Name = "System Shutdown")]
public class SystemShutdownCommand : CommandData<SystemCommandParameters>
{
}
