using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;

namespace Weda.SubNode.Core.Configuration.Validators.Device;

/// <summary>
/// Validates background task periods configuration.
/// Checks ReportHealth and ReportConfiguration period values.
/// </summary>
public class PeriodsValidator : IConfigurationPropertyValidator
{
    public string PropertyName => "Periods";

    public ConfigurationValidationResult Validate(ConfigurationValidationContext context)
    {
        var desiredConfig = context.DesiredConfig;
        var options = context.Options;

        // If periods validation is disabled, skip
        if (!options.ValidatePeriods)
            return ConfigurationValidationResult.Success;

        // If Periods section is not provided, skip validation (use existing config)
        if (desiredConfig.Periods == null)
            return ConfigurationValidationResult.Success;

        // Validate ReportHealth period
        if (desiredConfig.Periods.ReportHealth < 0)
        {
            return ConfigurationValidationResult.Failure(
                "ReportHealth period cannot be negative");
        }

        if (desiredConfig.Periods.ReportConfiguration < 0)
        {
            return ConfigurationValidationResult.Failure(
                $"ReportConfiguration period ({desiredConfig.Periods.ReportConfiguration}ms) " +
                "cannot be negative");
        }

        // Validate ReportConfiguration period range (default: 1 minute ~ 7 days)
        // Value of 0 means disabled, which is allowed
        if (desiredConfig.Periods.ReportConfiguration > 0)
        {
            if (desiredConfig.Periods.ReportConfiguration < options.ReportConfigurationMinMs)
            {
                return ConfigurationValidationResult.Failure(
                    $"ReportConfiguration period ({desiredConfig.Periods.ReportConfiguration}ms) " +
                    $"is below minimum ({options.ReportConfigurationMinMs}ms)");
            }

            if (desiredConfig.Periods.ReportConfiguration > options.ReportConfigurationMaxMs)
            {
                return ConfigurationValidationResult.Failure(
                    $"ReportConfiguration period ({desiredConfig.Periods.ReportConfiguration}ms) " +
                    $"exceeds maximum ({options.ReportConfigurationMaxMs}ms)");
            }
        }

        return ConfigurationValidationResult.Success;
    }
}
