using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;

namespace Weda.SubNode.Core.Configuration.Validators.Device;

/// <summary>
/// Validates sensor configurations.
/// Checks sensor names, intervals, thresholds, and sensor list consistency.
/// </summary>
public class SensorsValidator : IConfigurationPropertyValidator
{
    public string PropertyName => "Sensors";

    public ConfigurationValidationResult Validate(ConfigurationValidationContext context)
    {
        var desiredConfig = context.DesiredConfig;
        var currentConfig = context.CurrentConfig;
        var options = context.Options;

        // If Sensors section is not provided, skip validation
        if (desiredConfig.Sensors == null)
            return ConfigurationValidationResult.Success;

        foreach (var sensor in desiredConfig.Sensors)
        {
            // Validate sensor name (if enabled)
            if (options.ValidateSensors && string.IsNullOrEmpty(sensor.Name))
            {
                return ConfigurationValidationResult.Failure(
                    "Sensor name cannot be empty");
            }

            // Check for unknown sensors (if enabled)
            if (options.RejectUnknownSensors)
            {
                var existingSensor = currentConfig.Sensors.FirstOrDefault(s =>
                    s.Name.Equals(sensor.Name, StringComparison.OrdinalIgnoreCase));

                if (existingSensor == null)
                {
                    return ConfigurationValidationResult.Failure(
                        $"Unknown sensor '{sensor.Name}' in configuration update");
                }
            }

            // Validate sensor config values
            if (options.ValidateSensors && sensor.Config != null)
            {
                if (sensor.Config.Interval <= 0)
                {
                    return ConfigurationValidationResult.Failure(
                        $"Sensor '{sensor.Name}' interval must be greater than 0");
                }

                // Validate thresholds if provided (if enabled)
                if (options.ValidateThresholds && sensor.Config.Thresholds != null)
                {
                    var thresholdResult = ValidateThresholds(sensor.Name, sensor.Config.Thresholds);
                    if (!thresholdResult.IsValid)
                        return thresholdResult;
                }
            }
        }

        // Check if all sensors are required (if enabled)
        if (options.RequireAllSensors)
        {
            foreach (var existingSensor in currentConfig.Sensors)
            {
                var found = desiredConfig.Sensors.Any(s =>
                    s.Name.Equals(existingSensor.Name, StringComparison.OrdinalIgnoreCase));

                if (!found)
                {
                    return ConfigurationValidationResult.Failure(
                        $"Missing sensor '{existingSensor.Name}' in configuration update " +
                        $"(RequireAllSensors is enabled)");
                }
            }
        }

        return ConfigurationValidationResult.Success;
    }

    private static ConfigurationValidationResult ValidateThresholds(
        string sensorName,
        Abstractions.Cloud.Clients.DeviceManagement.Contracts.SubNodeThresholdsDto thresholds)
    {
        if (thresholds.UpperCritical.HasValue && thresholds.UpperWarning.HasValue &&
            thresholds.UpperCritical < thresholds.UpperWarning)
        {
            return ConfigurationValidationResult.Failure(
                $"Sensor '{sensorName}': UpperCritical must be >= UpperWarning");
        }

        if (thresholds.LowerWarning.HasValue && thresholds.LowerCritical.HasValue &&
            thresholds.LowerWarning < thresholds.LowerCritical)
        {
            return ConfigurationValidationResult.Failure(
                $"Sensor '{sensorName}': LowerWarning must be >= LowerCritical");
        }

        if (thresholds.UpperWarning.HasValue && thresholds.LowerWarning.HasValue &&
            thresholds.UpperWarning < thresholds.LowerWarning)
        {
            return ConfigurationValidationResult.Failure(
                $"Sensor '{sensorName}': UpperWarning must be >= LowerWarning");
        }

        return ConfigurationValidationResult.Success;
    }
}
