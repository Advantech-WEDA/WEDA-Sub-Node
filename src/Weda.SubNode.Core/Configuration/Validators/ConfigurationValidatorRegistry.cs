using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;
using Weda.SubNode.Core.Configuration.Validators.Device;

namespace Weda.SubNode.Core.Configuration.Validators;

/// <summary>
/// Registry for configuration property validators.
/// Uses a whitelist approach - only registered properties can be updated.
/// Unregistered properties will be rejected by the DefaultConfigurationValidator.
/// </summary>
public class ConfigurationValidatorRegistry
{
    private readonly Dictionary<string, IConfigurationPropertyValidator> _validators;
    private readonly DefaultConfigurationValidator _defaultValidator;

    /// <summary>
    /// Creates a new registry with the default set of validators.
    /// </summary>
    public static ConfigurationValidatorRegistry CreateDefault()
    {
        var registry = new ConfigurationValidatorRegistry();

        // Register known property validators (whitelist)
        // Note: Enabled is a boolean with default value, so it's always present
        // and applied directly in DeviceBase.ApplyDeviceConfigurationUpdateAsync
        registry.Register(new DtdlValidator());
        registry.Register(new PeriodsValidator());
        registry.Register(new SensorsValidator());
        registry.Register(new PipelineValidator());

        return registry;
    }

    public ConfigurationValidatorRegistry()
    {
        _validators = new Dictionary<string, IConfigurationPropertyValidator>(StringComparer.OrdinalIgnoreCase);
        _defaultValidator = new DefaultConfigurationValidator(() => _validators.Keys);
    }

    /// <summary>
    /// Registers a validator for a specific property.
    /// </summary>
    public void Register(IConfigurationPropertyValidator validator)
    {
        _validators[validator.PropertyName] = validator;
    }

    /// <summary>
    /// Gets the validator for a property, or the default validator if not found.
    /// </summary>
    public IConfigurationPropertyValidator GetValidator(string propertyName)
    {
        return _validators.TryGetValue(propertyName, out var validator)
            ? validator
            : _defaultValidator;
    }

    /// <summary>
    /// Validates all properties in the configuration update.
    /// First checks for unregistered properties, then validates each registered property.
    /// </summary>
    public ConfigurationValidationResult ValidateAll(ConfigurationValidationContext context)
    {
        // Step 1: Check for unregistered properties in desired config
        var unregisteredResult = ValidateNoUnregisteredProperties(context);
        if (!unregisteredResult.IsValid)
            return unregisteredResult;

        // Step 2: Validate each registered property
        foreach (var validator in _validators.Values)
        {
            var result = validator.Validate(context);
            if (!result.IsValid)
                return result;
        }

        return ConfigurationValidationResult.Success;
    }

    /// <summary>
    /// Checks if the desired config contains any properties that don't have registered validators.
    /// </summary>
    private ConfigurationValidationResult ValidateNoUnregisteredProperties(ConfigurationValidationContext context)
    {
        var desiredConfig = context.DesiredConfig;

        // Get list of properties that are set (non-null) in desired config
        var providedProperties = GetProvidedProperties(desiredConfig);

        // Check each provided property against registered validators
        foreach (var propertyName in providedProperties)
        {
            if (!_validators.ContainsKey(propertyName))
            {
                return ConfigurationValidationResult.Failure(
                    $"Property '{propertyName}' is not allowed to be updated. " +
                    $"Allowed properties: {string.Join(", ", _validators.Keys)}");
            }
        }

        return ConfigurationValidationResult.Success;
    }

    /// <summary>
    /// Gets the list of properties that are explicitly provided (non-null) in the desired config.
    /// </summary>
    private static List<string> GetProvidedProperties(
        Abstractions.Cloud.Clients.DeviceManagement.Contracts.SubNodeDeviceConfigDto desiredConfig)
    {
        var properties = new List<string>();

        // Check each property that can be updated
        if (desiredConfig.Dtdl != null)
            properties.Add("Dtdl");

        if (desiredConfig.Periods != null)
            properties.Add("Periods");

        if (desiredConfig.Sensors != null)
        {
            properties.Add("Sensors");

            // Pipeline is validated when Sensors are provided (pipelines are inside sensors)
            // Check if any sensor has pipeline configurations
            var hasPipeline = desiredConfig.Sensors.Any(s =>
                s.Report?.DspPipeline != null || s.Report?.TransformPipeline != null);

            if (hasPipeline)
                properties.Add("Pipeline");
        }

        // Note: DeviceCapabilities, Communication, Properties are read-only
        // They should not be updated via config-update

        return properties;
    }

    /// <summary>
    /// Gets all registered property names.
    /// </summary>
    public IEnumerable<string> RegisteredProperties => _validators.Keys;
}
