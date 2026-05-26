using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Core.Commands.Handlers.System.Models;

/// <summary>
/// Shared parameters for system commands (reboot, shutdown).
/// </summary>
public class SystemCommandParameters
{
    /// <summary>
    /// Delay in seconds before initiating the operation.
    /// Allows time for the result response to be published before the system goes offline.
    /// Default is 5 seconds.
    /// </summary>
    [JsonPropertyName("delaySeconds")]
    [Display(Name = "Delay (seconds)")]
    public int DelaySeconds { get; init; } = 5;
}
