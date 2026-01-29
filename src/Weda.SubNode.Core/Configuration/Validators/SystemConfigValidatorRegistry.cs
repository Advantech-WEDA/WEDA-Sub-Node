using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;
using Weda.SubNode.Core.Configuration.Validators.System;

namespace Weda.SubNode.Core.Configuration.Validators;

/// <summary>
/// Registry for system configuration property validators.
/// Validates system-level settings like WedaNode, Record, and Serilog.
/// </summary>
public class SystemConfigValidatorRegistry
{
    private readonly Dictionary<string, ISystemConfigPropertyValidator> _validators;

    /// <summary>
    /// Creates a new registry with the default set of validators.
    /// </summary>
    public static SystemConfigValidatorRegistry CreateDefault()
    {
        var registry = new SystemConfigValidatorRegistry();

        // Register system config validators
        registry.Register(new WedaNodeValidator());
        registry.Register(new RecordValidator());

        return registry;
    }

    public SystemConfigValidatorRegistry()
    {
        _validators = new Dictionary<string, ISystemConfigPropertyValidator>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Registers a validator for a specific property.
    /// </summary>
    public void Register(ISystemConfigPropertyValidator validator)
    {
        _validators[validator.PropertyName] = validator;
    }

    /// <summary>
    /// Validates all properties in the system configuration update.
    /// </summary>
    public ConfigurationValidationResult ValidateAll(SystemConfigValidationContext context)
    {
        foreach (var validator in _validators.Values)
        {
            var result = validator.Validate(context);
            if (!result.IsValid)
                return result;
        }

        return ConfigurationValidationResult.Success;
    }

    /// <summary>
    /// Gets all registered property names.
    /// </summary>
    public IEnumerable<string> RegisteredProperties => _validators.Keys;
}
