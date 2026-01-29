using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;

namespace Weda.SubNode.Core.Configuration.Validators.System;

/// <summary>
/// Validates WedaNode (NATS connection) configuration.
/// WedaNode settings cannot be changed at runtime - requires restart.
/// </summary>
public class WedaNodeValidator : ISystemConfigPropertyValidator
{
    public string PropertyName => "WedaNode";

    public ConfigurationValidationResult Validate(SystemConfigValidationContext context)
    {
        if (context.DesiredConfig.WedaNode != null)
        {
            return ConfigurationValidationResult.Failure(
                "WedaNode configuration cannot be changed at runtime. " +
                "NATS connection settings require a restart to take effect.");
        }

        return ConfigurationValidationResult.Success;
    }
}
