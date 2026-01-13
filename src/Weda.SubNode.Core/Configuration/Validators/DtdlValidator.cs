using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;

namespace Weda.SubNode.Core.Configuration.Validators;

/// <summary>
/// Validates DTDL configuration consistency.
/// When AutoGenEnabled is false, DtdlPath and all Sensor.Dtmi must be specified.
/// </summary>
public class DtdlValidator : IConfigurationPropertyValidator
{
    public string PropertyName => "Dtdl";

    public ConfigurationValidationResult Validate(ConfigurationValidationContext context)
    {
        var desiredConfig = context.DesiredConfig;

        // If Dtdl section is not provided, skip validation (use existing config)
        if (desiredConfig.Dtdl == null)
            return ConfigurationValidationResult.Success;

        // If AutoGenEnabled is true, no further validation needed (DTDL will be auto-generated)
        if (desiredConfig.Dtdl.AutoGenEnabled)
            return ConfigurationValidationResult.Success;

        // AutoGenEnabled is false: validate required fields
        if (string.IsNullOrWhiteSpace(desiredConfig.Dtdl.DtdlPath))
        {
            return ConfigurationValidationResult.Failure(
                "Dtdl.DtdlPath is required when Dtdl.AutoGenEnabled is false");
        }

        // All sensors must have Dtmi specified
        if (desiredConfig.Sensors != null)
        {
            foreach (var sensor in desiredConfig.Sensors)
            {
                if (string.IsNullOrWhiteSpace(sensor.Dtmi))
                {
                    return ConfigurationValidationResult.Failure(
                        $"Sensor '{sensor.Name}' requires Dtmi when Dtdl.AutoGenEnabled is false");
                }
            }
        }

        return ConfigurationValidationResult.Success;
    }
}
