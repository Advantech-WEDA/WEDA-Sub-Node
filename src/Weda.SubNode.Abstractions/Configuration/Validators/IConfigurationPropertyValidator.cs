using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Configuration.Validators;

/// <summary>
/// Interface for validating specific configuration properties during config updates.
/// Each property (e.g., Dtdl, Periods, Sensors) should have its own validator implementation.
/// </summary>
public interface IConfigurationPropertyValidator
{
    /// <summary>
    /// The property name this validator handles (e.g., "Dtdl", "Periods", "Sensors").
    /// Used by the registry to route validation to the correct validator.
    /// </summary>
    string PropertyName { get; }

    /// <summary>
    /// Validates the configuration property.
    /// </summary>
    /// <param name="context">The validation context containing current and desired configurations</param>
    /// <returns>Validation result indicating success or failure with error message</returns>
    ConfigurationValidationResult Validate(ConfigurationValidationContext context);
}

/// <summary>
/// Context for configuration property validation.
/// Contains all information needed to validate a configuration update.
/// </summary>
public class ConfigurationValidationContext
{
    /// <summary>
    /// The current device configuration before the update.
    /// </summary>
    public required DeviceConfiguration CurrentConfig { get; init; }

    /// <summary>
    /// The desired device configuration from cloud.
    /// </summary>
    public required SubNodeDeviceConfigDto DesiredConfig { get; init; }

    /// <summary>
    /// Validation options controlling which checks are enabled.
    /// </summary>
    public required ConfigUpdateOptions Options { get; init; }
}
