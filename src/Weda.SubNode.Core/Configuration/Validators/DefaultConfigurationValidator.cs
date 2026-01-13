using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;

namespace Weda.SubNode.Core.Configuration.Validators;

/// <summary>
/// Default validator for unknown/unregistered properties.
/// Rejects updates to properties that don't have explicit validators.
/// This implements a blacklist approach - only explicitly allowed properties can be updated.
/// </summary>
public class DefaultConfigurationValidator(Func<IEnumerable<string>> getAllowedProperties)
    : IConfigurationPropertyValidator
{
    public string PropertyName => "*";

    /// <summary>
    /// This validator is called when a property is not registered.
    /// It always fails validation to enforce the whitelist approach.
    /// </summary>
    public ConfigurationValidationResult Validate(ConfigurationValidationContext context)
    {
        // This should not be called directly via ValidateAll()
        // It's used as a fallback when GetValidator() is called with an unknown property
        var allowedProperties = getAllowedProperties();
        return ConfigurationValidationResult.Failure(
            $"Unknown property in configuration update. " +
            $"Allowed properties: {string.Join(", ", allowedProperties)}");
    }

    /// <summary>
    /// Validates that a specific property is in the allowed list.
    /// </summary>
    public ConfigurationValidationResult ValidateProperty(string propertyName)
    {
        var allowedProperties = getAllowedProperties().ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!allowedProperties.Contains(propertyName))
        {
            return ConfigurationValidationResult.Failure(
                $"Property '{propertyName}' is not allowed to be updated. " +
                $"Allowed properties: {string.Join(", ", allowedProperties)}");
        }

        return ConfigurationValidationResult.Success;
    }
}
