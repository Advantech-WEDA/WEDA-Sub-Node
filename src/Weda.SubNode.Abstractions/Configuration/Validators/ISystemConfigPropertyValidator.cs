using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

namespace Weda.SubNode.Abstractions.Configuration.Validators;

/// <summary>
/// Interface for validating specific system configuration properties during config updates.
/// Each property (e.g., WedaNode, Record, Serilog) should have its own validator implementation.
/// </summary>
public interface ISystemConfigPropertyValidator
{
    /// <summary>
    /// The property name this validator handles (e.g., "WedaNode", "Record", "Serilog").
    /// Used by the registry to route validation to the correct validator.
    /// </summary>
    string PropertyName { get; }

    /// <summary>
    /// Validates the system configuration property.
    /// </summary>
    /// <param name="context">The validation context containing desired system configuration</param>
    /// <returns>Validation result indicating success or failure with error message</returns>
    ConfigurationValidationResult Validate(SystemConfigValidationContext context);
}

/// <summary>
/// Context for system configuration property validation.
/// Contains all information needed to validate a system configuration update.
/// </summary>
public class SystemConfigValidationContext
{
    /// <summary>
    /// The desired system configuration from cloud.
    /// </summary>
    public required SubNodeSystemCfgDto DesiredConfig { get; init; }
}
